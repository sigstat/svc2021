using SigStat.Common;
using SigStat.Common.Algorithms.Distances;
using SigStat.Common.Helpers;
using SigStat.Common.Logging;
using SigStat.Common.Model;
using SigStat.Common.Pipeline;
using SVC2021.Classifiers;
using SVC2021.Entities;
using SVC2021.Helpers;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SVC2021
{

    public class Svc2021SolverVC
    {

        static string statFileName = "TrainingStat.csv";

        static Stopwatch sw = Stopwatch.StartNew();
        // Set MaxDegreeOfParallelism to 1 to debug on a single thread
        static ParallelOptions parallelOptions = new ParallelOptions() { MaxDegreeOfParallelism = 1 };

        static List<TrainHelper.TrainingStatistics> trainingComparisonStatistics = new List<TrainHelper.TrainingStatistics>();
        static double expectableDifference = 0.55;


        static List<FeatureDescriptor> fingerFeatures = new List<FeatureDescriptor>() { Features.X, Features.Y };
        static List<FeatureDescriptor> stylusFeatures = new List<FeatureDescriptor>() { Features.X, Features.Y, Features.Pressure };
        static IDistance<double[][]> distanceFunction = new DtwDistance();
        static ConditionalSequence fingerPipeline = new ConditionalSequence(Svc2021.IsPreprocessed)
        {
            Pipelines.Scale1X, Pipelines.Scale1Y,
            Pipelines.TranslateCogX, Pipelines.TranslateCogY
        };

        static ConditionalSequence stylusPipeline = new ConditionalSequence(Svc2021.IsPreprocessed)
        {
            Pipelines.Filter,
            Pipelines.Scale1X, Pipelines.Scale1Y, Pipelines.Scale1Pressure,
            Pipelines.TranslateCogX, Pipelines.TranslateCogY, Pipelines.TranslateCogPressure
        };


        public static void Solve(string dbPath, string comparisonsFile, bool isOnlyDtw, bool isTrainingReady)
        {
            SimpleConsoleLogger logger = new SimpleConsoleLogger();


            Debug("Loading signatures");
            var loader = new Svc2021Loader(dbPath, true) { Logger = logger };
            var db = new Database(loader.EnumerateSigners());
            Debug($"Found {db.AllSigners.SelectMany(s => s.Signatures).Count()} signatures from {db.AllSigners?.Count} signers");

            Debug("Loading comparison files");

            var comparisons = ComparisonHelper.LoadComparisons(comparisonsFile, db).ToList();
            Debug($"Found {comparisons.Count} comparisons");
            string fileBase = Path.GetFileNameWithoutExtension(comparisonsFile);

            ProgressHelper progress;

            if (!isTrainingReady)
            {
                Debug("Loading training comparison files)");
                var trainingComparisions = ComparisonHelper.LoadTrainingComparisonFiles(dbPath, db);
                Debug($"Found {trainingComparisions.Count} training comparisions");

                var trainingData = new List<TrainHelper.TrainingComparisonData>();


                progress = ProgressHelper.StartNew(trainingComparisions.Count, 3);

                foreach (var comparison in trainingComparisions)
                {
                    InputDevice inputDevice = 0;
                    if (comparison.ReferenceSignature.InputDevice != comparison.QuestionedSignature.InputDevice)
                        Debug($"Stylus-finger comparison detected");
                    else
                        inputDevice = comparison.ReferenceSignature.InputDevice;

                    var distance = TrainHelper.Do1v1Comparision
                        (
                            comparison,
                            inputDevice == InputDevice.Stylus ? stylusPipeline : fingerPipeline,
                            distanceFunction,
                            inputDevice == InputDevice.Stylus ? stylusFeatures : fingerFeatures
                        );

                    var timeDiff = TrainHelper.CalculateDifference(comparison, Features.T);
                    var stdDevXDiff = TrainHelper.CalculateDifference(comparison, Features.X);
                    var stdDevYDiff = TrainHelper.CalculateDifference(comparison, Features.Y);


                    trainingData.Add(new TrainHelper.TrainingComparisonData()
                    {
                        DtwDistance = distance,
                        ExpectedPrediction = comparison.ExpectedPrediction,
                        InputDevice = inputDevice,
                        DurationDifference = timeDiff,
                        StdXDifference = stdDevXDiff,
                        StdYDifference = stdDevYDiff
                    });
                    progress.IncrementValue();
                }


                //DTW distance
                var genuineStylusTrainingDistances = trainingData.Where(td => td.ExpectedPrediction == 0 & td.InputDevice == InputDevice.Stylus).Select(td => td.DtwDistance).ToList();
                var forgedStylusTrainingDistances = trainingData.Where(td => td.ExpectedPrediction == 1 & td.InputDevice == InputDevice.Stylus).Select(td => td.DtwDistance).ToList();

                var genuineFingerTrainingDistances = trainingData.Where(td => td.ExpectedPrediction == 0 & td.InputDevice == InputDevice.Finger).Select(td => td.DtwDistance).ToList();
                var forgedFingerTrainingDistances = trainingData.Where(td => td.ExpectedPrediction == 1 & td.InputDevice == InputDevice.Finger).Select(td => td.DtwDistance).ToList();

                trainingComparisonStatistics.AddRange(TrainHelper.CalculateTrainingStatistics("", genuineStylusTrainingDistances, forgedStylusTrainingDistances, genuineFingerTrainingDistances, forgedFingerTrainingDistances));

                Debug($"Created {trainingData.Count} training data row: \n" +
                    $"Stylus: {genuineStylusTrainingDistances.Count()} genuine and {forgedStylusTrainingDistances.Count()} forged \n" +
                    $"Finger: {genuineFingerTrainingDistances.Count()} genuine and {forgedFingerTrainingDistances.Count()} forged ");


                // Duration
                genuineStylusTrainingDistances = trainingData.Where(td => td.ExpectedPrediction == 0 & td.InputDevice == InputDevice.Stylus).Select(td => td.DurationDifference).ToList();
                forgedStylusTrainingDistances = trainingData.Where(td => td.ExpectedPrediction == 1 & td.InputDevice == InputDevice.Stylus).Select(td => td.DurationDifference).ToList();

                genuineFingerTrainingDistances = trainingData.Where(td => td.ExpectedPrediction == 0 & td.InputDevice == InputDevice.Finger).Select(td => td.DurationDifference).ToList();
                forgedFingerTrainingDistances = trainingData.Where(td => td.ExpectedPrediction == 1 & td.InputDevice == InputDevice.Finger).Select(td => td.DurationDifference).ToList();

                trainingComparisonStatistics.AddRange(TrainHelper.CalculateTrainingStatistics("T", genuineStylusTrainingDistances, forgedStylusTrainingDistances, genuineFingerTrainingDistances, forgedFingerTrainingDistances));


                // X
                genuineStylusTrainingDistances = trainingData.Where(td => td.ExpectedPrediction == 0 & td.InputDevice == InputDevice.Stylus).Select(td => td.StdXDifference).ToList();
                forgedStylusTrainingDistances = trainingData.Where(td => td.ExpectedPrediction == 1 & td.InputDevice == InputDevice.Stylus).Select(td => td.StdXDifference).ToList();

                genuineFingerTrainingDistances = trainingData.Where(td => td.ExpectedPrediction == 0 & td.InputDevice == InputDevice.Finger).Select(td => td.StdXDifference).ToList();
                forgedFingerTrainingDistances = trainingData.Where(td => td.ExpectedPrediction == 1 & td.InputDevice == InputDevice.Finger).Select(td => td.StdXDifference).ToList();

                trainingComparisonStatistics.AddRange(TrainHelper.CalculateTrainingStatistics("X", genuineStylusTrainingDistances, forgedStylusTrainingDistances, genuineFingerTrainingDistances, forgedFingerTrainingDistances));


                // Y
                genuineStylusTrainingDistances = trainingData.Where(td => td.ExpectedPrediction == 0 & td.InputDevice == InputDevice.Stylus).Select(td => td.StdYDifference).ToList();
                forgedStylusTrainingDistances = trainingData.Where(td => td.ExpectedPrediction == 1 & td.InputDevice == InputDevice.Stylus).Select(td => td.StdYDifference).ToList();

                genuineFingerTrainingDistances = trainingData.Where(td => td.ExpectedPrediction == 0 & td.InputDevice == InputDevice.Finger).Select(td => td.StdYDifference).ToList();
                forgedFingerTrainingDistances = trainingData.Where(td => td.ExpectedPrediction == 1 & td.InputDevice == InputDevice.Finger).Select(td => td.StdYDifference).ToList();

                trainingComparisonStatistics.AddRange(TrainHelper.CalculateTrainingStatistics("Y", genuineStylusTrainingDistances, forgedStylusTrainingDistances, genuineFingerTrainingDistances, forgedFingerTrainingDistances));


                TrainHelper.SaveTrainingStatistic(trainingData, trainingComparisonStatistics, statFileName);

                Debug($"Verifiers trained");
            }

            else
            {
                Debug($"Loading training statistics");

                TrainHelper.LoadTrainingStatistic(statFileName, out trainingComparisonStatistics);

                Debug($"Training statistics loaded");
            }

            progress = ProgressHelper.StartNew(comparisons.Count, 3);
            Parallel.ForEach(comparisons, parallelOptions, comparison =>
            {
                comparison.Prediction = 1 - Test(comparison, isOnlyDtw);
                progress.IncrementValue();
            });

            Debug($"Predictions ready");


            ComparisonHelper.SavePredictions(comparisons, fileBase + "_predictions.txt");
            ComparisonHelper.SaveComparisons(comparisons, fileBase + "_results.xlsx");

            Debug($"Predictions saved");


            progress = ProgressHelper.StartNew(1000, 3);
            var results = new ConcurrentBag<BenchmarkResult>();
            Parallel.For(0, 1000, parallelOptions, i =>
            {
                BenchmarkResult benchmark = new BenchmarkResult();
                benchmark.Threshold = ((double)i) / 1000;
                foreach (var comparison in comparisons)
                {
                    if (comparison.ExpectedPrediction == 1)
                    {
                        benchmark.ForgeryCount++;
                        if (comparison.Prediction < benchmark.Threshold) benchmark.FalseAcceptance++;
                    }
                    else
                    {
                        benchmark.GenuineCount++;
                        if (comparison.Prediction >= benchmark.Threshold) benchmark.FalseRejection++;
                    }
                }
                results.Add(benchmark);
                progress.IncrementValue();
            });

            ComparisonHelper.SaveBenchmarkResults(results.OrderBy(r => r.Threshold), fileBase + "_results.xlsx");


            Debug($"Ready");
        }

        static void Debug(string msg)
        {
            var mem = Process.GetCurrentProcess().PagedMemorySize64;
            Console.WriteLine($"{msg} (Time: {sw.Elapsed}, Memory: {mem:n0})");
        }


        private static double Test(Comparison1v1 comparison, bool onlyDtw)
        {
            if (onlyDtw)
                return TestByDtw(comparison);
            else
            {
                var dtwDecision = TestByDtw(comparison);
                var timeDecision = TestByTime(comparison);
                var xDevDecision = TestByFeatureDev(comparison, Features.X);
                var yDevDecision = TestByFeatureDev(comparison, Features.Y);


                return (5 * dtwDecision + 2 * timeDecision + xDevDecision + yDevDecision) / 9;
                //return (timeDecision + xDevDecision + yDevDecision) / 3;
            }
        }
        private static double TestByTime(Comparison1v1 comparison)
        {
            var refTime = GlobalFeatureExtractor.CalculateDuration(comparison.ReferenceSignature);
            var testTime = GlobalFeatureExtractor.CalculateDuration(comparison.QuestionedSignature);

            var diff = (Math.Abs(refTime - testTime) / refTime);

            if (diff > expectableDifference) return 0;

            return ((-1) / expectableDifference) * diff + 1;
        }

        private static double TestByFeatureDev(Comparison1v1 comparison, FeatureDescriptor<List<double>> feature)
        {
            var refDev = GlobalFeatureExtractor.CalculateStandardDeviationOfFeature(comparison.ReferenceSignature, feature);
            var testDev = GlobalFeatureExtractor.CalculateStandardDeviationOfFeature(comparison.QuestionedSignature, feature);

            var diff = (Math.Abs(refDev - testDev) / refDev);

            if (diff > expectableDifference) return 0;

            return ((-1) / expectableDifference) * diff + 1;
        }

        private static double TestByDtw(Comparison1v1 comparison, string feature = "")
        {
            if (comparison.ReferenceSignature.InputDevice != comparison.QuestionedSignature.InputDevice)
            {
                Debug($"Stylus-finger comparison detected");
                return -1;
            }
            else
            {
                var inputDevice = comparison.ReferenceSignature.InputDevice;

                var genuineComparisonStat = trainingComparisonStatistics.First(tcs => tcs.Description == "Genuine" + inputDevice + "ComparisonStat" + feature);
                var forgedComparisonStat = trainingComparisonStatistics.First(tcs => tcs.Description == "Forged" + inputDevice + "ComparisonStat" + feature);
                var distance = TrainHelper.Do1v1Comparision
                                       (
                                           comparison,
                                           inputDevice == InputDevice.Stylus ? stylusPipeline : fingerPipeline,
                                           distanceFunction,
                                           inputDevice == InputDevice.Stylus ? stylusFeatures : fingerFeatures
                                       );

                return CalculateDecisionByDtwDistance(distance, genuineComparisonStat, forgedComparisonStat);
            }

        }

        private static double CalculateDecisionByDtwDistance(double distance, TrainHelper.TrainingStatistics genuineComparisonStat, TrainHelper.TrainingStatistics forgedComparisonStat)
        {
            if (distance < genuineComparisonStat.Min)
                return 1;
            if (distance > forgedComparisonStat.Median)
                return 0;

            return (forgedComparisonStat.Median - distance) / (forgedComparisonStat.Median - genuineComparisonStat.Min);
        }

        private static double Test2(Comparison1v1 comparison, bool onlyDtw)
        {
            if (onlyDtw)
                return TestByDtw(comparison);
            else
            {
                var dtwDecision = TestByDtw(comparison);
                //var timeDecision = TestByDtw(comparison, "T");
                var xDevDecision = TestByDtw(comparison, "X");
                var yDevDecision = TestByDtw(comparison, "Y");

                return (2 * dtwDecision /*+ timeDecision*/ + xDevDecision + yDevDecision) / 4;
            }
        }
    }
}
