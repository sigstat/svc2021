using SigStat.Common;
using SigStat.Common.Logging;
using SigStat.Common.Model;
using SigStat.Common.Pipeline;
using SigStat.Common.Transforms;
using SixLabors.ImageSharp;
using SVC2021.Classifiers;
using SVC2021.Entities;
using SVC2021.Helpers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static SVC2021.Svc2021Loader;
using SVC2021.Helpers;

namespace SVC2021
{
    static class Experiments
    {
        public static void TestSigner(string comparisonsFile)
        {
            var reportLogger = new ReportInformationLogger();
            var consoleLogger = new SimpleConsoleLogger();
            var logger = new CompositeLogger() { Loggers = { consoleLogger, reportLogger } };

            //string signerID = "0236";
            string signerID = "0241";
            //string signerID = "0232";
            Svc2021Loader loader = new Svc2021Loader(Program.DbPath, true) { Logger = logger };
            var comparisons = ComparisonHelper.LoadComparisons(comparisonsFile)
                .Where(s => s.ReferenceSigner == signerID && s.ReferenceInput == InputDevice.Stylus).ToList();

            var signatureIds = comparisons.Select(c => c.ReferenceSignatureFile)
                .Concat(comparisons.Select(c => c.QuestionedSignatureFile))
                .Distinct().ToArray();

            var signatures = loader.LoadSignatures(signatureIds)
                .ToDictionary(s => s.ID, s => s);

            Console.WriteLine($"Generating images");

            var refSignatures = comparisons
                .Select(s => s.ReferenceSignatureFile).Distinct()
                .Select(s => signatures[s]).ToList();

            foreach (var sig in signatures.Values)
            {
                //if (sig.Signer.ID != signerID)
                //    continue;
                string prefix = refSignatures.Contains(sig) ? "REF_" : "";
                string filename = signerID + "\\" + prefix + Path.ChangeExtension(Path.GetFileName(sig.ID), ".png");
                if (File.Exists(filename))
                    continue;
                //sig.SaveImage(filename);

            }

            Console.WriteLine($"{refSignatures.Count} references, {comparisons.Count} comparisons");

            Verifier verifier = new Verifier()
            {
                Pipeline = new ConditionalSequence(Svc2021.IsPreprocessed) {
                        Pipelines.Filter,
                        //Pipelines.Rotation,
                        Pipelines.Scale1X, Pipelines.Scale1Y, Pipelines.Scale1Pressure,
                        Pipelines.TranslateCogX, Pipelines.TranslateCogY, Pipelines.TranslateCogPressure

                        },
                Classifier = new DtwMinMaxClassifier() { Features = { Features.X, Features.Y, Features.Pressure } },
                Logger = logger
            
            };
            verifier.Train(refSignatures.Cast<Signature>().ToList());

            Parallel.ForEach(comparisons, Program.ParallelOptions, comparison =>
            {
                comparison.Prediction = 1 - verifier.Test(signatures[comparison.QuestionedSignatureFile]);
            });

            var details = new Dictionary<string, ClassificationDetails>();
            foreach (var log in reportLogger.GetReportLogs().OfType<ClassificationDetails>())
            {
                details[log.SignerID + "_" + log.SignatureID] = log;
            };
            
            foreach (var comparison in comparisons)
            {
                var detailsRecord = details[comparison.ReferenceSigner + "_" + comparison.QuestionedSignatureFile];
                comparison.Add(Stats.Distance, detailsRecord.Distance);
                comparison.Add(Stats.GenuineThreshold,detailsRecord.GenuineThreshold);
                comparison.Add(Stats.ForgeryThreshold, detailsRecord.ForgeryThreshold);

            }


            string file = signerID + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + "_results.xlsx";
            ComparisonHelper.SaveComparisons(comparisons, file);

            var results = comparisons.GetBenchmarkResults();
            var model = LogAnalyzer.GetBenchmarkLogModel(reportLogger.GetReportLogs());
            var distanceMatrix = model.SignerResults[signerID].DistanceMatrix.ToArray();
            ComparisonHelper.WriteTable(distanceMatrix.Transpose(), "distances", file);
            Console.WriteLine(results.GetEer());


        }


        public static void TestLoader()
        {
            Svc2021Loader l = new Svc2021Loader(Program.DbPath, true) { Logger = new SimpleConsoleLogger() };
            //string file = "Evaluation\\stylus\\u0114_s_u1015s0001_sg0004.txt";
            //string file = "Evaluation\\stylus\\u0115_s_u1016s0001_sg0003.txt";

            string file = "Evaluation\\finger\\u0377_g_u139_s1_g1_b2_sign_w5_c_007.txt";


            var fi = new Svc2021Loader.SignatureFile(file);
            Svc2021Signature s = new Svc2021Signature() { Split = fi.Split, InputDevice = fi.InputDevice, DB = fi.DB };

            string localFile = Path.Combine(Program.SignaturesDirectory, Path.GetFileName(file));

            l.LoadSignature(s, localFile, true);
            Console.WriteLine(s.X.Count);


        }
    }
}
