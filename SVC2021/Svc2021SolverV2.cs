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
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SVC2021
{
    public static class Svc2021SolverV2
    {
        class CDLSComparer : IEqualityComparer<ClassifierDistanceLogState>
        {
            public bool Equals([AllowNull] ClassifierDistanceLogState x, [AllowNull] ClassifierDistanceLogState y)
            {
                return x.Signature1Id == y.Signature1Id && x.Signature2Id == y.Signature2Id;
            }

            public int GetHashCode([DisallowNull] ClassifierDistanceLogState obj)
            {
                return (obj.Signature1Id + ":" + obj.Signature2Id).GetHashCode();
            }
        }
        static Stopwatch sw = Stopwatch.StartNew();

        public static void Solve(string dbPath, string comparisonsFile)
        {
            // Option1: Decisions must be made exclusively on comparison signature pairs, no other information can be used

            var reportLogger = new ReportInformationLogger();
            var consoleLogger = new SimpleConsoleLogger();
            var logger = new CompositeLogger() { Loggers = { consoleLogger, reportLogger } };


            string fileBase = Path.GetFileNameWithoutExtension(comparisonsFile);
            string predictionsFile = fileBase + DateTime.Now.ToString("yyyyMMdd_HHmm") + "_predictions.txt";
            string resultsFile = fileBase + DateTime.Now.ToString("yyyyMMdd_HHmm") + "_results.xlsx";

            Debug("Loading signatures");
            var loader = new Svc2021Loader(dbPath, true) { Logger = logger };
            var db = new Database(loader.EnumerateSigners());
            Debug($"Found {db.AllSigners.SelectMany(s => s.Signatures).Count()} signatures from {db.AllSigners?.Count} signers");

            Debug("Loading comparison files");

            var comparisons = ComparisonHelper.LoadComparisons(comparisonsFile, db).ToList();
            Debug($"Found {comparisons.Count} comparisons");


            var referenceSignatures = comparisons.Select(s => s.ReferenceSignature).Distinct().ToList();

            Debug($"Found {referenceSignatures.Count} reference signatures");

            var verifiersBySignature = new ConcurrentDictionary<string, Verifier>();

            var progress = ProgressHelper.StartNew(referenceSignatures.Count, 10);
            Parallel.ForEach(referenceSignatures, Program.ParallelOptions, signature =>
            {
                Verifier verifier = new Verifier()
                {
                    Pipeline = new ConditionalSequence(Svc2021.IsPreprocessed) {
                        Pipelines.Filter,
                        Pipelines.Scale1X, Pipelines.Scale1Y, Pipelines.Scale1Pressure,
                        Pipelines.TranslateCogX, Pipelines.TranslateCogY, Pipelines.TranslateCogPressure

                        },
                    Classifier = new Dtw1v1Classifier(40, 50, 60) { Features = { Features.X, Features.Y, Features.Pressure } },
                    Logger = logger

                };
                verifier.Train(new List<Signature>(1) { signature });
                verifiersBySignature[signature.ID] = verifier;
                progress.IncrementValue();
            });

            Debug($"Verifiers trained");

            progress = ProgressHelper.StartNew(comparisons.Count, 10);
            Parallel.ForEach(comparisons, Program.ParallelOptions, comparison =>
            {
                comparison.Prediction = 1 - verifiersBySignature[comparison.ReferenceSignature.ID].Test(comparison.QuestionedSignature);
                progress.IncrementValue();
            });

            Debug($"Predictions ready");

            //var model = LogAnalyzer.GetBenchmarkLogModel(reportLogger.GetReportLogs());
            var distances = reportLogger.GetReportLogs().OfType<ClassifierDistanceLogState>().Distinct(new CDLSComparer())
                .ToDictionary(d => d.Signature1Id + "_" + d.Signature2Id, d => d.Distance);


            foreach (var comparison in comparisons)
            {
                var distance = distances[comparison.ReferenceSignatureFile + "_" + comparison.QuestionedSignatureFile];
                comparison.Add(Stats.Distance, distance);
                comparison.Add("r_dx", comparison.ReferenceSignature.X.StdDiviation());
                comparison.Add("r_dy", comparison.ReferenceSignature.Y.StdDiviation());
                comparison.Add("r_dp", comparison.ReferenceSignature.Pressure.StdDiviation());
                comparison.Add("r_cnt", comparison.ReferenceSignature.X.Count);
                comparison.Add("r_t", comparison.ReferenceSignature.T[^1] - comparison.ReferenceSignature.T.First());


                comparison.Add("q_dx", comparison.QuestionedSignature.X.StdDiviation());
                comparison.Add("q_dy", comparison.QuestionedSignature.Y.StdDiviation());
                comparison.Add("q_dp", comparison.QuestionedSignature.Pressure.StdDiviation());
                comparison.Add("q_cnt", comparison.QuestionedSignature.X.Count);
                comparison.Add("q_t", comparison.QuestionedSignature.T[^1] - comparison.QuestionedSignature.T.First());



            }
            ComparisonHelper.SavePredictions(comparisons, predictionsFile);
            ComparisonHelper.SaveComparisons(comparisons, resultsFile);

            Debug($"Predictions saved");

            var results = comparisons.GetBenchmarkResults();
            ComparisonHelper.SaveBenchmarkResults(results, resultsFile);

            Console.WriteLine(results.GetEer());

            Debug($"Ready");
        }

        static void Debug(string msg)
        {
            var mem = Process.GetCurrentProcess().PagedMemorySize64;
            Console.WriteLine($"{msg} (Time: {sw.Elapsed}, Memory: {mem:n0})");
        }
    }
}
