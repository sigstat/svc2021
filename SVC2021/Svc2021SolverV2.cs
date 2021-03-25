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
            string trainingFile = fileBase + DateTime.Now.ToString("yyyyMMdd_HHmm") + "_training.csv";


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

            var stylusPipeline = new ConditionalSequence(Svc2021.IsPreprocessed)
            {
                        Pipelines.Filter,
                        Pipelines.Scale1X, Pipelines.Scale1Y, Pipelines.Scale1Pressure,
                        Pipelines.TranslateCogX, Pipelines.TranslateCogY, Pipelines.TranslateCogPressure
            };

            var fingerPipeline = new ConditionalSequence(Svc2021.IsPreprocessed)
            {
                        Pipelines.Scale1X, Pipelines.Scale1Y,
                        Pipelines.TranslateCogX, Pipelines.TranslateCogY
            };

            var stylusClassifier = new Dtw1v1Classifier(40, 50, 60) { Features = { Features.X, Features.Y, Features.Pressure } };
            var fingerClassifier = new Dtw1v1Classifier(40, 50, 60) { Features = { Features.X, Features.Y } };

            var progress = ProgressHelper.StartNew(referenceSignatures.Count, 10);
            Parallel.ForEach(referenceSignatures, Program.ParallelOptions, signature =>
            {
                Verifier verifier = new Verifier()
                {
                    Pipeline = signature.InputDevice == InputDevice.Stylus ? stylusPipeline : fingerPipeline,
                    Classifier = signature.InputDevice == InputDevice.Stylus ? stylusClassifier : fingerClassifier,
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

            var distances = reportLogger.GetReportLogs().OfType<ClassifierDistanceLogState>().Distinct(new CDLSComparer())
                .ToDictionary(d => d.Signature1Id + "_" + d.Signature2Id, d => d.Distance);


            foreach (var comparison in comparisons)
            {
                var distance = distances[comparison.ReferenceSignatureFile + "_" + comparison.QuestionedSignatureFile];

                var stdevX1 = comparison.ReferenceSignature.X.StdDiviation();
                var stdevY1 = comparison.ReferenceSignature.Y.StdDiviation();
                var stdevP1 = comparison.ReferenceSignature.Pressure.StdDiviation();
                var count1 = comparison.ReferenceSignature.X.Count;
                var duration1 = comparison.ReferenceSignature.T[^1] - comparison.ReferenceSignature.T[0];

                var stdevX2 = comparison.QuestionedSignature.X.StdDiviation();
                var stdevY2 = comparison.QuestionedSignature.Y.StdDiviation();
                var stdevP2 = comparison.QuestionedSignature.Pressure.StdDiviation();
                var count2 = comparison.QuestionedSignature.X.Count;
                var duration2 = comparison.QuestionedSignature.T[^1] - comparison.QuestionedSignature.T[0];

                comparison.Add("stdevX1", stdevX1);
                comparison.Add("stdevY1", stdevY1);
                comparison.Add("stdevP1", stdevP1);
                comparison.Add("count1", count1);
                comparison.Add("duration1", duration1);

                comparison.Add("stdevX2", stdevX2);
                comparison.Add("stdevY2", stdevY2);
                comparison.Add("stdevP2", stdevP2);
                comparison.Add("count2", count2);
                comparison.Add("duration2", duration2);

                comparison.Add("diffDTW", distance);
                comparison.Add("diffX", GetDifference(stdevX1, stdevX2));
                comparison.Add("diffY", GetDifference(stdevY1, stdevY2));
                comparison.Add("diffP", GetDifference(stdevP1, stdevP2));
                comparison.Add("diffCount", GetDifference(count1, count2));
                comparison.Add("diffDuration", GetDifference(duration1, duration2));

            }
            ComparisonHelper.SavePredictions(comparisons, predictionsFile);
            ComparisonHelper.SaveComparisons(comparisons, resultsFile);
            ComparisonHelper.SaveTrainingCsv(comparisons, trainingFile);

            Debug($"Predictions saved");

            var results = comparisons.GetBenchmarkResults();
            ComparisonHelper.SaveBenchmarkResults(results, resultsFile);

            Console.WriteLine(results.GetEer());

            Debug($"Ready");
        }

        static double GetDifference(double d1, double d2)
        {
            return Math.Abs(d1 - d2) / d1;
        }

        static void Debug(string msg)
        {
            var mem = Process.GetCurrentProcess().PagedMemorySize64;
            Console.WriteLine($"{msg} (Time: {sw.Elapsed}, Memory: {mem:n0})");
        }
    }
}
