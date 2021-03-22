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



        static Stopwatch sw = Stopwatch.StartNew();
        // Set MaxDegreeOfParallelism to 1 to debug on a single thread
        static ParallelOptions parallelOptions = new ParallelOptions() { MaxDegreeOfParallelism = 1 };

        static List<TrainHelper.TrainingStatistics> trainingComparisonStatistics = new List<TrainHelper.TrainingStatistics>();
        static double expectableDifference = 0.25;
        static List<FeatureDescriptor> features = new List<FeatureDescriptor>() { Features.X, Features.Y, Features.Pressure };
        static IDistance<double[][]> distanceFunction = new DtwDistance();
        static ConditionalSequence pipeline = new ConditionalSequence(Svc2021.IsPreprocessed) { Pipelines.FilterScale1TranslateCogXYP };


        public static void Solve(string dbPath, string comparisonsFile, bool isGroupedByInputDevice, bool isOnlyDtw, bool isTrainingReady = false)
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

                // Option1: Decisions must be made exclusively on comparison signature pairs, no other information can be used
                // Option2: Decision can take advantage of all data in the comparison set

                var trainingData = new List<TrainHelper.TrainingComparisonData>();


                progress = ProgressHelper.StartNew(trainingComparisions.Count, 3);

                foreach (var comparison in trainingComparisions)
                {
                    var distance = TrainHelper.Do1v1Comparision(comparison, pipeline, distanceFunction, features);

                    InputDevice inputDevice = 0;
                    if (comparison.ReferenceInput != comparison.QuestionedInput)
                        Debug($"Stylus-finger comparison detected");
                    else
                        inputDevice = comparison.ReferenceInput;

                    trainingData.Add(new TrainHelper.TrainingComparisonData()
                    {
                        Distance = distance,
                        ExpectedPrediction = comparison.ExpectedPrediction,
                        InputDevice = inputDevice
                    }); ;
                    progress.IncrementValue();
                }



                if (!isGroupedByInputDevice)
                {
                    var genuineTrainingDistances = trainingData.Where(td => td.ExpectedPrediction == 0).Select(td => td.Distance).ToList();
                    var forgedTrainingDistances = trainingData.Where(td => td.ExpectedPrediction == 1).Select(td => td.Distance).ToList();
                    Debug($"Created {trainingData.Count} training data row: {genuineTrainingDistances.Count()} genuine and {forgedTrainingDistances.Count()} forged");

                    trainingComparisonStatistics.Add(new TrainHelper.TrainingStatistics()
                    {
                        Description = "GenuineComparisonStat",
                        Min = genuineTrainingDistances.Min(),
                        Max = genuineTrainingDistances.Max(),
                        Average = genuineTrainingDistances.Average(),
                        Median = genuineTrainingDistances.Median(),
                        Stdev = genuineTrainingDistances.StdDiviation()
                    });

                    trainingComparisonStatistics.Add(new TrainHelper.TrainingStatistics()
                    {
                        Description = "ForgedComparisonStat",
                        Min = forgedTrainingDistances.Min(),
                        Max = forgedTrainingDistances.Max(),
                        Average = forgedTrainingDistances.Average(),
                        Median = forgedTrainingDistances.Median(),
                        Stdev = forgedTrainingDistances.StdDiviation()
                    });


                }
                else
                {
                    var genuineStylusTrainingDistances = trainingData.Where(td => td.ExpectedPrediction == 0 & td.InputDevice == InputDevice.Stylus).Select(td => td.Distance).ToList();
                    var forgedStylusTrainingDistances = trainingData.Where(td => td.ExpectedPrediction == 1 & td.InputDevice == InputDevice.Stylus).Select(td => td.Distance).ToList();

                    var genuineFingerTrainingDistances = trainingData.Where(td => td.ExpectedPrediction == 0 & td.InputDevice == InputDevice.Finger).Select(td => td.Distance).ToList();
                    var forgedFingerTrainingDistances = trainingData.Where(td => td.ExpectedPrediction == 1 & td.InputDevice == InputDevice.Finger).Select(td => td.Distance).ToList();

                    Debug($"Created {trainingData.Count} training data row: \n" +
                        $"Stylus: {genuineStylusTrainingDistances.Count()} genuine and {forgedStylusTrainingDistances.Count()} forged \n" +
                        $"Finger: {genuineFingerTrainingDistances.Count()} genuine and {forgedFingerTrainingDistances.Count()} forged ");

                    trainingComparisonStatistics.Add(new TrainHelper.TrainingStatistics()
                    {
                        Description = "GenuineStylusComparisonStat",
                        Min = genuineStylusTrainingDistances.Min(),
                        Max = genuineStylusTrainingDistances.Max(),
                        Average = genuineStylusTrainingDistances.Average(),
                        Median = genuineStylusTrainingDistances.Median(),
                        Stdev = genuineStylusTrainingDistances.StdDiviation()
                    });

                    trainingComparisonStatistics.Add(new TrainHelper.TrainingStatistics()
                    {
                        Description = "ForgedStylusComparisonStat",
                        Min = forgedStylusTrainingDistances.Min(),
                        Max = forgedStylusTrainingDistances.Max(),
                        Average = forgedStylusTrainingDistances.Average(),
                        Median = forgedStylusTrainingDistances.Median(),
                        Stdev = forgedStylusTrainingDistances.StdDiviation()
                    });

                    trainingComparisonStatistics.Add(new TrainHelper.TrainingStatistics()
                    {
                        Description = "GenuineFingerComparisonStat",
                        Min = genuineFingerTrainingDistances.Min(),
                        Max = genuineFingerTrainingDistances.Max(),
                        Average = genuineFingerTrainingDistances.Average(),
                        Median = genuineFingerTrainingDistances.Median(),
                        Stdev = genuineFingerTrainingDistances.StdDiviation()
                    });

                    trainingComparisonStatistics.Add(new TrainHelper.TrainingStatistics()
                    {
                        Description = "ForgedFingerComparisonStat",
                        Min = forgedFingerTrainingDistances.Min(),
                        Max = forgedFingerTrainingDistances.Max(),
                        Average = forgedFingerTrainingDistances.Average(),
                        Median = forgedFingerTrainingDistances.Median(),
                        Stdev = forgedFingerTrainingDistances.StdDiviation()
                    });

                }

                TrainHelper.SaveTrainingStatistic(trainingData, trainingComparisonStatistics, fileBase + "TrainingStat.csv");

                Debug($"Verifiers trained");
            }

            else
            {
                Debug($"Loading training statistics");

                // txt filename !!! TASK1 --> without rotation, TASK2-->with rotation normalization
                TrainHelper.LoadTrainingStatistic(fileBase + "TrainingStat.csv", out trainingComparisonStatistics);

                Debug($"Training statistics loaded");
            }

            progress = ProgressHelper.StartNew(comparisons.Count, 3);
            Parallel.ForEach(comparisons, parallelOptions, comparison =>
            {
                comparison.Prediction = 1 - Test(comparison, isGroupedByInputDevice, isOnlyDtw);
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


        private static double Test(Comparison1v1 comparison, bool isGroupedByInputDevice, bool onlyDtw)
        {
            if (onlyDtw)
                return TestByDtw(comparison, isGroupedByInputDevice);
            else
            {
                var dtwDecision = TestByDtw(comparison, isGroupedByInputDevice);
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
            var testTime =  GlobalFeatureExtractor.CalculateDuration(comparison.QuestionedSignature);

            var diff = (Math.Abs(refTime - testTime) / refTime);

            if (diff > expectableDifference) return 0;

            return ((-1) / expectableDifference) * diff + 1;
        } 
        
        private static double TestByFeatureDev(Comparison1v1 comparison, FeatureDescriptor<List<double>> feature)
        {
            var refDev = GlobalFeatureExtractor.CalculateStandardDeviationOfFeature(comparison.ReferenceSignature, feature);
            var testDev =  GlobalFeatureExtractor.CalculateStandardDeviationOfFeature(comparison.QuestionedSignature, feature);

            var diff = (Math.Abs(refDev - testDev) / refDev);

            if (diff > expectableDifference) return 0;

            return ((-1) / expectableDifference) * diff + 1;
        }

        private static double TestByDtw(Comparison1v1 comparison, bool isGroupedByInputDecive)
        {
            if (!isGroupedByInputDecive)
            {
                var genuineComparisonStat = trainingComparisonStatistics.First(tcs => tcs.Description == "GenuineComparisionStat");
                var forgedComparisonStat = trainingComparisonStatistics.First(tcs => tcs.Description == "ForgedComparisionStat");
                var distance = TrainHelper.Do1v1Comparision(comparison, pipeline, distanceFunction, features);

                return CalculateDecisionByDtwDistance(distance, genuineComparisonStat, forgedComparisonStat);
            }
            else
            {
                if (comparison.ReferenceInput != comparison.QuestionedInput)
                {
                    Debug($"Stylus-finger comparison detected");
                    return -1;
                }
                else
                {
                    var genuineComparisonStat = trainingComparisonStatistics.First(tcs => tcs.Description == "Genuine" + comparison.ReferenceInput + "ComparisonStat");
                    var forgedComparisonStat = trainingComparisonStatistics.First(tcs => tcs.Description == "Forged" + comparison.ReferenceInput + "ComparisonStat");
                    var distance = TrainHelper.Do1v1Comparision(comparison, pipeline, distanceFunction, features);

                    return CalculateDecisionByDtwDistance(distance, genuineComparisonStat, forgedComparisonStat);
                }
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
    }
}
