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

namespace SVC2021
{
    static class Experiments
    {
        public static void TestSigner(string comparisonsFile)
        {
            string signerID = "0236";
            Svc2021Loader loader = new Svc2021Loader(Program.DbPath, true) { Logger = new SimpleConsoleLogger() };
            var comparisons = ComparisonHelper.LoadComparisons(comparisonsFile)
                .Where(s => s.ReferenceSigner == signerID && s.ReferenceInput == InputDevice.Stylus).ToList();

            var signatureIds = comparisons.Select(c => c.ReferenceSignatureFile)
                .Concat(comparisons.Select(c => c.QuestionedSignatureFile))
                .Distinct().ToArray();

            var signatures = loader.LoadSignatures(signatureIds)
                .ToDictionary(s => s.ID, s => s);

            Console.WriteLine($"{signatures} signatures");

            var refSignatures = comparisons
                .Select(s => s.ReferenceSignatureFile).Distinct()
                .Select(s => signatures[s]).ToList();

            RealisticImageGenerator2 generator = new RealisticImageGenerator2(600, 400)
            {
                X = Features.X,
                Y = Features.Y,
                Pressure = Features.Pressure,
                OutputImage = Features.Image
            };

            if (!Directory.Exists("Images"))
                Directory.CreateDirectory("Images");

            foreach (var sig in signatures.Values)
            {
                if (sig.Signer.ID != signerID)
                    continue;
                // Unfortunately, these are expected by the RealisticImageGenerator
                sig.SetFeature(Features.PenDown, sig.Pressure.Select(p => p > 0).ToList());
                sig.SetFeature(Features.Azimuth, sig.X.Select(s => 0d).ToList());
                sig.SetFeature(Features.Altitude, sig.X.Select(s => 0d).ToList());
                generator.Transform(sig);

                sig.Image.Save("Images\\" + Path.ChangeExtension(Path.GetFileName(sig.ID), ".png"));
            }

            Console.WriteLine($"{refSignatures.Count} references, {comparisons.Count} comparisons");

            Verifier verifier = new Verifier()
            {
                Pipeline = new ConditionalSequence(Svc2021.IsPreprocessed) {
                        Pipelines.Filter,
                        Pipelines.Scale1X, Pipelines.Scale1Y, Pipelines.Scale1Pressure,
                        Pipelines.TranslateCogX, Pipelines.TranslateCogY, Pipelines.TranslateCogPressure

                        },
                Classifier = new DtwMinMaxClassifier() { Features = { Features.X, Features.Y, Features.Pressure } }
            };
            verifier.Train(refSignatures.Cast<Signature>().ToList());

            Parallel.ForEach(comparisons, Program.ParallelOptions, comparison =>
            {
                comparison.Prediction = 1 - verifier.Test(signatures[comparison.QuestionedSignatureFile]);
            });

            string file = signerID + "_" + DateTime.Now.ToString("yyyyMMdd_hhmm") + "_results.xlsx";
            ComparisonHelper.SaveComparisons(comparisons, file);

            if (!Directory.Exists("Images"))
                Directory.CreateDirectory("Images");
            //RealisticImageGenerator generator = new RealisticImageGenerator(600, 400)
            //{ X = Features.X, Y = Features.Y, Pressure = Features.Pressure,
            // OutputImage = Features.Image};


          
            //Process.Start(file);

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
