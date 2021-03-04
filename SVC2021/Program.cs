using System;
using System.Diagnostics;
using System.Linq;
using SigStat.Common.Loaders;
using SigStat.Common.Logging;
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
            var loader = new Svc2021Loader(dbPath, true) { Logger = logger};
            var db = new Database(loader.EnumerateSigners());
            Debug($"Found {db.AllSigners.SelectMany(s=>s.Signatures).Count()} signatures from {db.AllSigners?.Count} signers");

            Debug("Loading comparison files");

            var comparisons = ComparisonHelper.LoadComparisons("Data\\Validation\\Task1_comparisons.txt", db).ToList();
            Debug($"Found {comparisons.Count} comparisons" );
            Console.WriteLine("signers: "+comparisons.Select(s=>s.ReferenceSignature.Signer.ID).Count());
            var groups = comparisons.Select(s=>s.ReferenceSignature).Distinct()
                .GroupBy(s => new Tuple<string, InputDevice>(s.Signer.ID, s.InputDevice));
            Console.WriteLine("groups: " +groups.Count()+ " min: "+ groups.Min(g=>g.Count()) + "max: "+ groups.Max(g => g.Count()));
            var gg = comparisons.Select(s => s.ReferenceSignature.ID).Distinct();
            var gq = comparisons.Select(s => s.QuestionedSignature.ID).Distinct();
            Console.WriteLine($"gg: {gg.Count()} gq: {gq.Count()} intersect: {gg.Intersect(gq).Count()}");
            // and now comes the hard lifting...
        }

        static void Debug(string msg)
        {
            var mem = Process.GetCurrentProcess().PagedMemorySize64;
            Console.WriteLine($"{msg} (Time: {sw.Elapsed}, Memory: {mem:n0})");
        }
    }
}
