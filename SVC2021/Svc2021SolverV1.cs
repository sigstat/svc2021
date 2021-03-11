using SigStat.Common;
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
    public static class Svc2021SolverV1
    {
        static Stopwatch sw = Stopwatch.StartNew();
        // Set MaxDegreeOfParallelism to 1 to debug on a single thread
        static ParallelOptions parallelOptions = new ParallelOptions() { MaxDegreeOfParallelism = -1 };

        public static void Solve(string dbPath, string comparisonsFile)
        {
            SimpleConsoleLogger logger = new SimpleConsoleLogger();
            string fileBase = Path.GetFileNameWithoutExtension(comparisonsFile);
            string predictionsFile = fileBase + DateTime.Now.ToString("yyyyMMdd_hhmm") + "_predictions.txt";
            string resultsFile = fileBase + DateTime.Now.ToString("yyyyMMdd_hhmm") + "_results.xlsx";

            Debug("Loading signatures");
            var loader = new Svc2021Loader(dbPath, true) { Logger = logger };
            var db = new Database(loader.EnumerateSigners());
            Debug($"Found {db.AllSigners.SelectMany(s => s.Signatures).Count()} signatures from {db.AllSigners?.Count} signers");

            Debug("Loading comparison files");

            var comparisons = ComparisonHelper.LoadComparisons(comparisonsFile, db).ToList();
            Debug($"Found {comparisons.Count} comparisons");



            // Option1: Decisions must be made exclusively on comparison signature pairs, no other information can be used
            // Option2: Decision can take advantage of all data in the comparison set

            var referenceSigners = new List<Signer>();

            foreach (var signatureGroup in comparisons.Select(s => s.ReferenceSignature).GroupBy(s => s.Signer.ID + "_" + s.InputDevice))
            {
                var signer = new Signer() { ID = signatureGroup.Key };
                signer.Signatures.AddRange(signatureGroup);
                foreach (var signature in signatureGroup)
                {
                    signature.Signer = signer;
                }
                referenceSigners.Add(signer);
            }
            Debug($"Created {referenceSigners.Count} reference signers");

            var verifiersBySignature = new ConcurrentDictionary<string, Verifier>();

            var progress = ProgressHelper.StartNew(referenceSigners.Count, 3);
            Parallel.ForEach(referenceSigners, parallelOptions, signer =>
            {
                Verifier verifier = new Verifier()
                {
                    Pipeline = new ConditionalSequence(Svc2021.IsPreprocessed) {
                        Pipelines.Filter,
                        Pipelines.Scale1X, Pipelines.Scale1Y, Pipelines.Scale1Pressure,
                        Pipelines.TranslateCogX, Pipelines.TranslateCogY, Pipelines.TranslateCogPressure

                        },
                    Classifier = new DtwMinMaxClassifier() { Features = { Features.X, Features.Y, Features.Pressure } }
                };
                verifier.Train(signer.Signatures);
                foreach (var signature in signer.Signatures)
                {
                    verifiersBySignature[signature.ID] = verifier;
                }
                progress.IncrementValue();
            });

            Debug($"Verifiers trained");

            progress = ProgressHelper.StartNew(comparisons.Count, 3);
            Parallel.ForEach(comparisons, parallelOptions, comparison =>
            {
                comparison.Prediction = 1 - verifiersBySignature[comparison.ReferenceSignature.ID].Test(comparison.QuestionedSignature);
                progress.IncrementValue();
            });

            Debug($"Predictions ready");

            ComparisonHelper.SavePredictions(comparisons, predictionsFile);
            ComparisonHelper.SaveComparisons(comparisons, resultsFile);

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

            ComparisonHelper.SaveBenchmarkResults(results.OrderBy(r => r.Threshold), resultsFile);


            Debug($"Ready");
        }

        static void Debug(string msg)
        {
            var mem = Process.GetCurrentProcess().PagedMemorySize64;
            Console.WriteLine($"{msg} (Time: {sw.Elapsed}, Memory: {mem:n0})");
        }
    }
}
