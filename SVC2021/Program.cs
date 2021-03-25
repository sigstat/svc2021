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

        public static string Svc2021EvalDbPath = GetSvc2021EvalDbPath();

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


        public static string DevelopFingerApi = "";
        public static string DevelopStylusApi = "http://dd52f64f-e407-4d17-afb6-564a25a3b762.westeurope.azurecontainer.io/score";
        public static string DeepSignStylusApi = "http://f6aadbcb-f769-4d62-81ce-f6949adfac69.westeurope.azurecontainer.io/score";
        public static string DeepSignFingerApi = "http://3cbb9e16-6247-44e8-9d47-2b2bc7082920.westeurope.azurecontainer.io/score";

        public static string[] SkipColumnsStylus = new[] { "ExpectedResult" };
        public static string[] SkipColumnsFinger = new[] { "ExpectedResult", "stdevP1", "stdevP2", "diffP" };
        static void Main(string[] args)
        {
            System1();
            System2();
            System3();

        }


        /// <summary>
        /// 4v1 classification - The system finds the 3 nearest neighborst of each reference signature
        /// using DTW algorithm. These neighbors are then used together with the base signature to train
        /// a classifier, which again, is used to evaluate the questioned siganture.
        /// </summary>
        private static void System1()
        {
            // Find 3 nearest neghbors of reference signatures
            SvcNeighborFile1 = Experiments.GroupCompetitionSigners(Svc2021EvalDbPath, SvcComparisonsFile1);
            SvcNeighborFile2 = Experiments.GroupCompetitionSigners(Svc2021EvalDbPath, SvcComparisonsFile2);
            SvcNeighborFile3 = Experiments.GroupCompetitionSigners(Svc2021EvalDbPath, SvcComparisonsFile3);
            // Use nearest neighbors together with references to calculate distances
            Svc2021SolverVM.Solve(Svc2021EvalDbPath, SvcComparisonsFile1, SvcNeighborFile1, 5);
            Svc2021SolverVM.Solve(Svc2021EvalDbPath, SvcComparisonsFile2, SvcNeighborFile2, 5);
            Svc2021SolverVM.Solve(Svc2021EvalDbPath, SvcComparisonsFile3, SvcNeighborFile3, 5);
        }

        /// <summary>
        /// 1v1 classification - Calculate global statistics on dataset and use them to perform classification
        /// Features: DTW, stdev(X), stdev(Y), stdev(P), duration, point count
        /// </summary>
        private static void System2()
        {
            Svc2021SolverVC.Solve(Svc2021EvalDbPath, SvcComparisonsFile1, false, false);
            Svc2021SolverVC.Solve(Svc2021EvalDbPath, SvcComparisonsFile2, false, false);
            Svc2021SolverVC.Solve(Svc2021EvalDbPath, SvcComparisonsFile3, false, false);

        }

        /// <summary>
        /// 1v1 classification - Use deep learning to train a classifier and perform the labeling of questioned signatures
        /// Features: DTW, stdev(X), stdev(Y), stdev(P), duration, point count
        /// </summary>
        private static void System3()
        {
            // Build inputs for model training
            GenerateMLTrainingSet(Split.Development | Split.Evaluation, InputDevice.Finger); // ~140.000 rows
            GenerateMLTrainingSet(Split.Development | Split.Evaluation, InputDevice.Stylus); // ~600.000 rows

            // Manual step: Azure Automated Machine Learning was used
            // to train a model for finger and another model for stylus signatures.
            // Public endpoints were created and stored in DeepSignStylusApi, DeepSignFingerApi for the model.
            // The models used are also included in the GitHub repository.

            var trainingFile1 = Svc2021SolverV2.Solve(Svc2021EvalDbPath, SvcComparisonsFile1, false);
            AzureHelper.GetPredictions(trainingFile1, $"Task1_prediction.txt", DeepSignStylusApi, SkipColumnsStylus);

            var trainingFile2 = Svc2021SolverV2.Solve(Svc2021EvalDbPath, SvcComparisonsFile2, false);
            AzureHelper.GetPredictions(trainingFile2, $"Task2_prediction.txt", DeepSignFingerApi, SkipColumnsFinger);

            // For task3, the solver will group the signature pairs by input device and
            // use Azure helper directly to perform the classification 
            Svc2021SolverV2.Solve(Svc2021EvalDbPath, SvcComparisonsFile3, true);

        }

        private static void GenerateMLTrainingSet(Split split, InputDevice device)
        {
            Console.WriteLine("*******************");
            Console.WriteLine("SPLIT: " + split);
            Console.WriteLine("DEVICE: " + device);
            Experiments.GenerateTrainingComparisons(DeepSignDbPath, split, device);
            Svc2021SolverV2.Solve(DeepSignDbPath, $"{split}_{device}_comparisons.txt", false);
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
