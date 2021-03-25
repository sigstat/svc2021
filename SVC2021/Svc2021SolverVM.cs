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
    public static class Svc2021SolverVM
    {
        static Stopwatch sw = Stopwatch.StartNew();


        public static void Solve(string dbPath, string comparisonFile, string neighborsFile, double scaling)
        {
            SimpleConsoleLogger logger = new SimpleConsoleLogger();
            string fileBase = Path.GetFileNameWithoutExtension(comparisonFile);
            string predictionsFile = fileBase + DateTime.Now.ToString("yyyyMMdd_HHmm") + "_predictions.txt";
            string resultsFile = fileBase + DateTime.Now.ToString("yyyyMMdd_HHmm") + "_results.xlsx";


            Debug("Loading signatures");
            // The loader now works with the evaluation database
            Svc2021Loader loader = new Svc2021Loader(dbPath, true) { Logger = new SimpleConsoleLogger() };
            // It will create a separate signer for each of the signatures, with the same ID as the signature
            // As a nice addition, the InputMethod is also filled out during the loading process
            var signers = loader.EnumerateSigners().ToList();
            var db = new Database(loader.EnumerateSigners());

            // A dictionary may be created for gaining quick access to signatures
            var signatures = signers.ToDictionary(s => s.ID, s => (Svc2021Signature)s.Signatures[0]);
            Console.WriteLine($"Found {signers.Count} signers");

            // The comparison loader now works with the evaluation comparisons
            var comparisons = ComparisonHelper.LoadComparisons(comparisonFile, db).ToList();
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
                signer.Signatures.AddRange(neighborhood.NeighborSignatureIds.Select(id => signatures[id]));

                groupedSigners.Add(signer);
            }

            Console.WriteLine($"{groupedSigners.Count} signers have been created.");

            var verifiersBySignature = new ConcurrentDictionary<string, Verifier>();

            var progress = ProgressHelper.StartNew(groupedSigners.Count, 3);
            Parallel.ForEach(groupedSigners, Program.ParallelOptions, signer =>
            {
                Verifier verifier = new Verifier()
                {
                    Pipeline = new ConditionalSequence(Svc2021.IsPreprocessed) {
                        Pipelines.Filter2,
                        Pipelines.SvcScale1X, Pipelines.SvcScale1Y, Pipelines.SvcScale1Pressure,
                        Pipelines.TranslateCogX, Pipelines.TranslateCogY, Pipelines.TranslateCogPressure

                        },
                    Classifier = new DtwNeighborsClassifier() { scale = scaling, Features = { Features.X, Features.Y, Features.Pressure } }
                };
                verifier.Train(signer.Signatures);

                //signer and signature have the same ID
                verifiersBySignature[signer.ID] = verifier;
                progress.IncrementValue();
            });

            Debug($"Verifiers trained");

            progress = ProgressHelper.StartNew(comparisons.Count, 3);
            Parallel.ForEach(comparisons, Program.ParallelOptions, comparison =>
            {
                comparison.Prediction = 1 - verifiersBySignature[comparison.ReferenceSignature.ID].Test(comparison.QuestionedSignature);
                progress.IncrementValue();
            });

            Debug($"Predictions ready");

            ComparisonHelper.SavePredictions(comparisons, predictionsFile);
            ComparisonHelper.SaveComparisons(comparisons, resultsFile);

            Debug($"Predictions saved");


            progress = ProgressHelper.StartNew(1000, 3);

            var benchmarkResults = ComparisonHelper.GetBenchmarkResults(comparisons);
            ComparisonHelper.SaveBenchmarkResults(benchmarkResults, resultsFile);


            Debug($"Ready");
        }

        static void Debug(string msg)
        {
            var mem = Process.GetCurrentProcess().PagedMemorySize64;
            Console.WriteLine($"{msg} (Time: {sw.Elapsed}, Memory: {mem:n0})");
        }
    }
}
