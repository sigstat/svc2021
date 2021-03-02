using System;
using System.Diagnostics;
using System.Linq;
using SigStat.Common.Loaders;
using SVC2021.Entities;
using SVC2021.Helpers;

namespace SVC2021
{
    class Program
    {
        static Stopwatch sw = Stopwatch.StartNew();

        static void Main(string[] args)
        {
            Debug("Loading signatures");

            //Please set this environment variable to point to DeepSignDB.zip at your computer.
            var dbPath = Environment.GetEnvironmentVariable("SVC2021");
            var loader = new Svc2021Loader(dbPath, true); 
            var db = new Database(loader.EnumerateSigners());
            Debug($"Found {db.AllSigners.SelectMany(s=>s.Signatures).Count()} signatures from {db.AllSigners?.Count} signers");

            Debug("Loading comparison files");

            var comparisons = ComparisonHelper.LoadComparisons("Data\\Validation\\Task1_comparisons.txt", db).ToList();
            Debug($"Found {comparisons.Count} comparisons" );

            // and now comes the hard lifting...
        }

        static void Debug(string msg)
        {
            var mem = Process.GetCurrentProcess().PagedMemorySize64;
            Console.WriteLine($"{msg} (Time: {sw.Elapsed}, Memory: {mem:n0})");
        }
    }
}
