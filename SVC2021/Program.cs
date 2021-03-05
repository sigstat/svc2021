using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using SigStat.Common;
using SigStat.Common.Loaders;
using SigStat.Common.Logging;
using SigStat.Common.Model;
using SigStat.Common.Pipeline;
using SigStat.Common.PipelineItems.Transforms.Preprocessing;
using SVC2021.Entities;
using SVC2021.Helpers;

namespace SVC2021
{
    class Program
    {
        static Stopwatch sw = Stopwatch.StartNew();

        static void Main(string[] args)
        {
            SimpleConsoleLogger logger = new SimpleConsoleLogger();

            Debug("Loading signatures");

            //Please set the SVC2021 environment variable to point to DeepSignDB.zip at your computer e.g.: C:\Databases\DeepSignDB.zip
            var dbPath = Environment.GetEnvironmentVariable("SVC2021");
            var loader = new Svc2021Loader(dbPath, true) { Logger = logger };
            var db = new Database(loader.EnumerateSigners());
            Debug($"Found {db.AllSigners.SelectMany(s => s.Signatures).Count()} signatures from {db.AllSigners?.Count} signers");

            Debug("Loading comparison files");

            var comparisons = ComparisonHelper.LoadComparisons("Data\\Validation\\Task1_comparisons.txt", db).ToList();
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

            var verifiersBySignature = new Dictionary<string, Verifier>();

            foreach (var signer in referenceSigners)
            {
                // TODO: Add classifier
                Verifier verifier = new Verifier()
                {
                    Pipeline = new ConditionalSequence(Svc2021.IsPreprocessed) {
                            Pipelines.FilterScale1TranslateCog
                        },
                    Classifier = null
                };
                //verifier.Train(signer.Signatures);
                foreach (var signature in signer.Signatures)
                {
                    verifiersBySignature[signature.ID] = verifier;
                }
            }

            Debug($"Verifiers trained");


            foreach (var comparison in comparisons)
            {
                //comparison.Prediction = 1 - verifiersBySignature[comparison.ReferenceSignature.ID].Test(comparison.QuestionedSignature);
            }

            Debug($"Predictions ready");

            // EER


            //many2one
            // DTW distances between references ==> max, min ==> Verification  avg(dtw)<min 0, avg(dtw)=max 0.5
            //one2one
            // Training set ==>global min, max
            // DTW distance dtw<min 0, dtw>max+(max-min) 1

            // What's the relation between stylus and finger based datasets
            // - do we need to campare them? (check sample ocmparison files and competition task descriptions)
            // - if yes, then what accuracies can we expect, when doing the comparisons in any of the two ways


            Debug($"Ready");
        }

        static void Debug(string msg)
        {
            var mem = Process.GetCurrentProcess().PagedMemorySize64;
            Console.WriteLine($"{msg} (Time: {sw.Elapsed}, Memory: {mem:n0})");
        }
    }
}
