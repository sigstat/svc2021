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

        public static string DeepSignDbPath = GetDeepSignDbPath();

        public static string Svc2021EvalDbPath =  GetSvc2021EvalDbPath();

        public static string SignaturesDirectory = "Data\\Signatures";


        public static string ComparisonsFile1 = "Data\\Validation\\Task1_comparisons.txt";
        public static string ComparisonsFile2 = "Data\\Validation\\Task2_comparisons.txt";
        public static string ComparisonsFile3 = "Data\\Validation\\Task3_comparisons.txt";

        public static string SvcComparisonsFile1 = "Data\\Competition\\SVC2021_Task1_comparisons.txt";
        public static string SvcComparisonsFile2 = "Data\\Competition\\SVC2021_Task2_comparisons.txt";
        public static string SvcComparisonsFile3 = "Data\\Competition\\SVC2021_Task3_comparisons.txt";

        public static string SvcNeighborFile1 = "Data\\Neighbors\\SVC2021_Task1_comparisons_neighbors.txt";
        public static string SvcNeighborFile2 = "Data\\Neighbors\\SVC2021_Task2_comparisons_neighbors.txt";
        public static string SvcNeighborFile3 = "Data\\Neighbors\\SVC2021_Task3_comparisons_neighbors.txt";

        // Set MaxDegreeOfParallelism to 1 to debug on a single thread, set to -1 to use all cores
        public static readonly ParallelOptions ParallelOptions = new ParallelOptions() { MaxDegreeOfParallelism = -1 };



        static void Main(string[] args)
        {

            //Experiments.GroupCompetitionSigners(Svc2021EvalDbPath, SvcComparisonsFile3);
          //  Experiments.LoadAndGroupSigners(Svc2021EvalDbPath, SvcComparisonsFile1, SvcNeighborFile1);

            Svc2021SolverVM.Solve(Svc2021EvalDbPath, SvcComparisonsFile1, SvcNeighborFile1, 5);
            Svc2021SolverVM.Solve(Svc2021EvalDbPath, SvcComparisonsFile2, SvcNeighborFile2, 5);
            Svc2021SolverVM.Solve(Svc2021EvalDbPath, SvcComparisonsFile3, SvcNeighborFile3, 5);

            //Svc2021SolverV2.Solve(DeepSignDbPath, ComparisonsFile1);
            //Svc2021SolverV2.Solve(DbPath, ComparisonsFile2);
            //Svc2021SolverV2.Solve(DbPath, ComparisonsFile3);

        }


        private static string GetDeepSignDbPath()
        {
            string path = Environment.GetEnvironmentVariable("SVC2021");
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                throw new Exception(@"Please set the SVC2021 environment variable to point to DeepSignDB.zip at your computer e.g.: C:\Databases\DeepSignDB.zip");

            return path;
        }

        private static string GetSvc2021EvalDbPath()
        {
            string path = Environment.GetEnvironmentVariable("SVC2021eval");
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                throw new Exception(@"Please set the SVC2021eval environment variable to point to SVC2021_EvalDB.zip at your computer e.g.: C:\Databases\SVC2021_EvalDB.zip");

            return path;
        }
    }
}
