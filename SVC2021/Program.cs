using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OfficeOpenXml;
using SigStat.Common;
using SigStat.Common.Helpers;
using SigStat.Common.Loaders;
using SigStat.Common.Logging;
using SigStat.Common.Model;
using SigStat.Common.Pipeline;
using SigStat.Common.PipelineItems.Transforms.Preprocessing;
using SVC2021.Classifiers;
using SVC2021.Entities;
using SVC2021.Helpers;

namespace SVC2021
{
    class Program
    { 

        public static string DbPath = GetDbPath();
        public static string SignaturesDirectory = "Data\\Signatures";


        public static string ComparisonsFile1 = "Data\\Validation\\Task1_comparisons.txt";
        public static string ComparisonsFile2 = "Data\\Validation\\Task2_comparisons.txt";
        public static string ComparisonsFile3 = "Data\\Validation\\Task3_comparisons.txt";

        // Set MaxDegreeOfParallelism to 1 to debug on a single thread, set to -1 to use all cores
        public static readonly ParallelOptions ParallelOptions = new ParallelOptions() { MaxDegreeOfParallelism = -1 };



        static void Main(string[] args)
        {
            //Experiments.TestSigner(ComparisonsFile1);

            Svc2021SolverV1.Solve(DbPath, ComparisonsFile1);

        }


        private static string GetDbPath()
        {
            string path = Environment.GetEnvironmentVariable("SVC2021");
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                throw new Exception(@"Please set the SVC2021 environment variable to point to DeepSignDB.zip at your computer e.g.: C:\Databases\DeepSignDB.zip");

            return path;
        }

    }
}
