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

        static void Main(string[] args)
        {
            //Please set the SVC2021 environment variable to point to DeepSignDB.zip at your computer e.g.: C:\Databases\DeepSignDB.zip
            var dbPath = Environment.GetEnvironmentVariable("SVC2021");
            var comparisonsFile = "Data\\Validation\\Task1_comparisons.txt";

            Svc2021SolverVC.Solve(dbPath, comparisonsFile);
        }

 
    }
}
