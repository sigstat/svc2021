using SigStat.Common;
using SigStat.Common.Algorithms.Distances;
using SigStat.Common.Helpers;
using SigStat.Common.Logging;
using SigStat.Common.Model;
using SigStat.Common.Pipeline;
using SigStat.Common.Transforms;
using SixLabors.ImageSharp;
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
using static SVC2021.Svc2021Loader;

namespace SVC2021
{
    static class Experiments
    {
        static Random rnd = new Random();

        public static void GenerateTrainingComparisons(string dbPath)
        {
            File.WriteAllLines("finger_comparisons.txt", EnumerateComparisons(dbPath, InputDevice.Finger).Distinct());
            File.WriteAllLines("stylus_comparisons.txt", EnumerateComparisons(dbPath, InputDevice.Stylus).Distinct());


        }
        public static IEnumerable<string> EnumerateComparisons(string dbPath, InputDevice input)
        {
            Console.WriteLine("Generating comparisons for "+input);
            int randomCount = 40;
            int genuineCount = 20;
            int forgeryCount = 20;

            var logger = new SimpleConsoleLogger();
            Svc2021Loader loader = new Svc2021Loader(dbPath, true) { Logger = logger };
            var signers = loader.EnumerateSigners().ToList();
            foreach (var signer in signers)
            {
                signer.Signatures.RemoveAll(s => s.GetFeature(Svc2021.InputDevice) != input);
            }

            signers = signers.Where(s => s.Signatures.Count > 0).ToList();
            Console.WriteLine("Found "+signers.Count+" signers");

            var allSignatures = signers.SelectMany(s => s.Signatures).ToList();
            var step = allSignatures.Count / randomCount;

            Console.WriteLine("Found "+allSignatures.Count +" signatures");

            foreach (var signer in signers)
            {
                var genuineSignatures = signer.Signatures.Where(s => s.Origin == Origin.Genuine).ToList();
                var forgedSignatures = signer.Signatures.Where(s => s.Origin == Origin.Forged).ToList();
                var randomForgeries = Enumerable.Range(0, randomCount).Select(i=>allSignatures[i* step + rnd.Next(step)]);

                genuineSignatures.LimitRandomly(genuineCount);
                forgedSignatures.LimitRandomly(forgeryCount);
                for (int i = 0; i < genuineSignatures.Count; i++)
                {
                    for (int j = i+1; j < genuineSignatures.Count; j++)
                    {
                        yield return genuineSignatures[i].ID+" "+genuineSignatures[j].ID;
                    }
                }
                for (int i = 0; i < genuineSignatures.Count; i++)
                {
                    for (int j = 0; j < forgedSignatures.Count; j++)
                    {
                        yield return genuineSignatures[i].ID + " " + forgedSignatures[j].ID;
                    }
                }

                foreach (var sig1 in genuineSignatures)
                {
                    foreach (var sig2 in randomForgeries)
                    {
                        if (sig2.Signer.ID == sig1.Signer.ID) continue;
                        yield return sig1.ID + " " + sig2.ID;
                    }
                }

               
            }

        }
        public static void TestSigner(string dbPath, string comparisonsFile)
        {
            var reportLogger = new ReportInformationLogger();
            var consoleLogger = new SimpleConsoleLogger();
            var logger = new CompositeLogger() { Loggers = { consoleLogger, reportLogger } };

            //string signerID = "0236";
            string signerID = "0241";
            //string signerID = "0232";
            Svc2021Loader loader = new Svc2021Loader(dbPath, true) { Logger = logger };
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
                comparison.Add(Stats.GenuineThreshold, detailsRecord.GenuineThreshold);
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

        internal static void GroupCompetitionSigners(string dbPath, string comparisonFile)
        {
            string outputFile = Path.GetFileNameWithoutExtension(comparisonFile) + "_neighbors.txt";
            string matrixFile = Path.GetFileNameWithoutExtension(comparisonFile) + "_matrix.xlsx";
            if (File.Exists(matrixFile)) File.Delete(matrixFile);

            Svc2021Loader loader = new Svc2021Loader(dbPath, true) { Logger = new SimpleConsoleLogger() };
            var signers = loader.EnumerateSigners().ToList();
            var signatures = signers.ToDictionary(s => s.ID, s => (Svc2021Signature)s.Signatures[0]);
            Console.WriteLine($"Found {signers.Count} signers");

            var comparisons = ComparisonHelper.LoadComparisons(comparisonFile).ToList();
            Console.WriteLine($"Found {comparisons.Count} comparisons with {comparisons.Select(c => c.ReferenceSignatureFile).Distinct().Count()} references");

            var distinctReferenceSignatures = comparisons
                .Select(s => s.ReferenceSignatureFile)
                .Distinct()
                .Select(f => signatures[f])
                .ToList();


            //var distances = new ConcurrentDictionary<Tuple<string, string>, double>();
            var stylusPipeline = new ConditionalSequence(Svc2021.IsPreprocessed) {
                        Pipelines.Filter,
                        Pipelines.Scale1X, Pipelines.Scale1Y, Pipelines.Scale1Pressure,
                        Pipelines.TranslateCogX, Pipelines.TranslateCogY, Pipelines.TranslateCogPressure
                        };

            var fingerPipeline = new ConditionalSequence(Svc2021.IsPreprocessed) {
                        Pipelines.Scale1X, Pipelines.Scale1Y,
                        Pipelines.TranslateCogX, Pipelines.TranslateCogY
                        };

            Console.WriteLine("Preprocessing...");
            foreach (var signature in distinctReferenceSignatures)
            {
                if (signature.InputDevice == InputDevice.Finger)
                    fingerPipeline.Transform(signature);
                else if (signature.InputDevice == InputDevice.Stylus)
                    stylusPipeline.Transform(signature);
                else
                    throw new NotSupportedException();
            }

            Console.WriteLine("Creating feature vectors");
            List<FeatureDescriptor> features = new List<FeatureDescriptor>() { Features.X, Features.Y};
            var signatureFeatures = distinctReferenceSignatures.Select(s => new { s.ID, Values = s.GetAggregateFeature(features).ToArray() }).ToList();


            Console.WriteLine("Calculating distances...");

            var dtw = new DtwDistance();
            var progress = ProgressHelper.StartNew(signatureFeatures.Count * signatureFeatures.Count, 5);

            var distances = new double[signatureFeatures.Count, signatureFeatures.Count];
            Parallel.For(0, signatureFeatures.Count, i =>
            {
                for (int j = 0; j < signatureFeatures.Count; j++)
                {
                    progress.IncrementValue();

                    var sigI = signatureFeatures[i];
                    var sigJ = signatureFeatures[j];

                    if (distances[j, i] != 0)
                    {
                        distances[i, j] = distances[j, i];
                    }
                    else if (i == j)
                    {
                        distances[i, j] = 0;
                    }
                    // We only compare similar signatures here
               //     else if (distinctReferenceSignatures[i].InputDevice != distinctReferenceSignatures[j].InputDevice)
                //    {
               //         distances[i, j] = 0;
               //     }
                    else
                    {
                        distances[i, j] = dtw.Calculate(sigI.Values, sigJ.Values);
                    }
                }
            });

            Console.WriteLine("Calculating nearest neighbors...");

            var nearestNeighbors = new Dictionary<string, List<Tuple<string,double>>>();

            for (int i = 0; i < signatureFeatures.Count; i++)
            {
                var nn = Enumerable.Range(0, signatureFeatures.Count).Select(j => new { J = j, Distance = distances[i, j] })
                    .Where(t => t.J != i)
                    .OrderBy(d => d.Distance)
                    .Take(3)
                    .Select(t=> new Tuple<string,double>(signatureFeatures[t.J].ID, t.Distance ))
                    .ToList();
                nearestNeighbors[signatureFeatures[i].ID] = nn;
            }

            Console.WriteLine("Writing nearest neighbors...");
            var lines = nearestNeighbors.Select(kvp => $"{kvp.Key} {string.Join(' ', kvp.Value.Select(v => v.Item1))} {string.Join(' ', kvp.Value.Select(v => v.Item2.ToString(ComparisonHelper.NumberFormat)))}");
            File.WriteAllLines(outputFile, lines);

            Console.WriteLine("Writing distances...");
            object[,] distanceTable = new object[signatureFeatures.Count + 1, signatureFeatures.Count + 1];
            for (int i = 0; i < signatureFeatures.Count; i++)
            {
                distanceTable[i, 0] = signatureFeatures[i].ID;
                distanceTable[0, i] = signatureFeatures[i].ID;
            }
            for (int i = 0; i < signatureFeatures.Count; i++)
            {
                for (int j = 0; j < signatureFeatures.Count; j++)
                {
                    distanceTable[i + 1, j + 1] = distances[i, j];
                }
            }

            ComparisonHelper.WriteTable(distanceTable, "distances", matrixFile);



            Console.WriteLine("Ready");

        }

        public static void LoadAndGroupSigners(string dbPath, string comparisonFile, string neighborsFile)
        {
            // The loader now works with the evaluation database
            Svc2021Loader loader = new Svc2021Loader(dbPath, true) { Logger = new SimpleConsoleLogger() };
            // It will create a separate signer for each of the signatures, with the same ID as the signature
            // As a nice addition, the InputMethod is also filled out during the loading process
            var signers = loader.EnumerateSigners().ToList();

            // A dictionary may be created for gaining quick access to signatures
            var signatures = signers.ToDictionary(s => s.ID, s => (Svc2021Signature)s.Signatures[0]);
            Console.WriteLine($"Found {signers.Count} signers");

            // The comparison loader now works with the evaluation comparisons
            var comparisons = ComparisonHelper.LoadComparisons(comparisonFile).ToList();
            Console.WriteLine($"Found {comparisons.Count} comparisons with {comparisons.Select(c => c.ReferenceSignatureFile).Distinct().Count()} references");



            // Neighborhoods contain the 3 nearest neighbors of each signature.
            // Note, that neighborhoods are not symmetric! Each signature has its own neighborhood 
            // Also, nearest neighbors are always signatures with the same input method as the primary signature

            // The nearest neighbors of all reference signatures from Tasks 1-3 have been precalculated for future use
            var neighborhoods = ComparisonHelper.LoadNeighborhoods(neighborsFile);

            // This would be one way to create signers based on the neighborhoods. Note that in this case
            // a signle signature may be assigned to multiple signers
            var groupedSigners = new List<Signer>();
            foreach (var neighborhood in neighborhoods)
            {
                var signer = new Signer() { ID = neighborhood.PrimarySignatureId };
                signer.Signatures.Add(signatures[neighborhood.PrimarySignatureId]);
                signer.Signatures.AddRange(neighborhood.NeighborSignatureIds.Select(id=>signatures[id]));

                groupedSigners.Add(signer);
            }

            Console.WriteLine($"{groupedSigners.Count} signers have been created.");

        }

        public static void TestLoader(string dbPath)
        {
            Svc2021Loader l = new Svc2021Loader(dbPath, true) { Logger = new SimpleConsoleLogger() };
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
