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

        static TrainHelper.TrainingStatistics genuineComparisonStat = new TrainHelper.TrainingStatistics();
        static TrainHelper.TrainingStatistics forgedComparisonStat = new TrainHelper.TrainingStatistics();
        static List<FeatureDescriptor> features = new List<FeatureDescriptor>() { Features.X, Features.Y, Features.Pressure };
        static IDistance<double[][]> distanceFunction = new DtwDistance();
        static ConditionalSequence pipeline = new ConditionalSequence(Svc2021.IsPreprocessed) { Pipelines.FilterScale1TranslateCog };


        public static void Solve(string dbPath, string comparisonsFile)
        {
            SimpleConsoleLogger logger = new SimpleConsoleLogger();


            Debug("Loading signatures");
            var loader = new Svc2021Loader(dbPath, true) { Logger = logger };
            var db = new Database(loader.EnumerateSigners());
            Debug($"Found {db.AllSigners.SelectMany(s => s.Signatures).Count()} signatures from {db.AllSigners?.Count} signers");

            Debug("Loading training comparison files)");
            var trainingComparisions = ComparisonHelper.LoadTrainingComparisonFiles(dbPath, db);
            Debug($"Found {trainingComparisions.Count} training comparisions");

            Debug("Loading comparison files");

            var comparisons = ComparisonHelper.LoadComparisons(comparisonsFile, db).ToList();
            Debug($"Found {comparisons.Count} comparisons");



            // Option1: Decisions must be made exclusively on comparison signature pairs, no other information can be used
            // Option2: Decision can take advantage of all data in the comparison set

            var trainingData = new List<TrainHelper.TrainingComparisonData>();


            var progress = ProgressHelper.StartNew(trainingComparisions.Count, 3);

            foreach (var comparison in trainingComparisions)
            {
                var distance = TrainHelper.Do1v1Comparision(comparison, pipeline, distanceFunction, features);

                trainingData.Add(new TrainHelper.TrainingComparisonData() { Distance = distance, ExpectedPrediction = comparison.ExpectedPrediction });
                progress.IncrementValue();
            }

            var genuineTrainingDistances = trainingData.Where(td => td.ExpectedPrediction == 0).Select(td => td.Distance).ToList();
            var forgedTrainingDistances = trainingData.Where(td => td.ExpectedPrediction == 1).Select(td => td.Distance).ToList();
            Debug($"Created {trainingData.Count} training data row: {genuineTrainingDistances.Count()} genuine and {forgedTrainingDistances.Count()} forged");

            genuineComparisonStat = new TrainHelper.TrainingStatistics()
            {
                Min = genuineTrainingDistances.Min(),
                Max = genuineTrainingDistances.Max(),
                Average = genuineTrainingDistances.Average(),
                Median = genuineTrainingDistances.Median(),
                Stdev = genuineTrainingDistances.StdDiviation()
            };

            forgedComparisonStat = new TrainHelper.TrainingStatistics()
            {
                Min = forgedTrainingDistances.Min(),
                Max = forgedTrainingDistances.Max(),
                Average = forgedTrainingDistances.Average(),
                Median = forgedTrainingDistances.Median(),
                Stdev = forgedTrainingDistances.StdDiviation()
            };

            string fileBase = Path.GetFileNameWithoutExtension(comparisonsFile);
            TrainHelper.SaveTrainingStatistic(trainingData, genuineComparisonStat, forgedComparisonStat, fileBase + "TrainingStat.txt");


            Debug($"Verifiers trained");

            progress = ProgressHelper.StartNew(comparisons.Count, 3);
            Parallel.ForEach(comparisons, parallelOptions, comparison =>
            {
                comparison.Prediction = 1 - Test(comparison);
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

        static double Test(Comparison1v1 comparison)
        {
            var distance = TrainHelper.Do1v1Comparision(comparison, pipeline, distanceFunction, features);

            if (distance < genuineComparisonStat.Min)
                return 1;
            if (distance > forgedComparisonStat.Min)
                return 0;

            return (forgedComparisonStat.Min - distance) / (forgedComparisonStat.Min - genuineComparisonStat.Median);
        }
    }
}
