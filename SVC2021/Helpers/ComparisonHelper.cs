using OfficeOpenXml;
using SigStat.Common.Helpers;
using SVC2021.Entities;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace SVC2021.Helpers
{
    static class ComparisonHelper
    {
        static readonly IFormatProvider numberFormat = new CultureInfo("EN-US").NumberFormat;

        public static IEnumerable<Comparison1v1> LoadComparisons(string fileName, Database db = null)
        {
            using (StreamReader sr = new StreamReader(fileName))
            {
                while (!sr.EndOfStream)
                {
                    var line = sr.ReadLine();
                    if (string.IsNullOrWhiteSpace(line))
                        continue;
                    string[] parts = line.Split(' ');
                    yield return new Comparison1v1(parts[0], parts[1])
                    {
                        // reference the signature objects if, a preloaded database is already available
                        ReferenceSignature = db?[parts[0].ToLower()],
                        QuestionedSignature = db?[parts[1].ToLower()]
                    };
                }
            }

        }

        public static List<Comparison1v1> LoadTrainingComparisonFiles(string databasePath, Database db = null)
        {
            var trainingComparisons = new List<Comparison1v1>();

            using (ZipArchive zip = ZipFile.OpenRead(databasePath))
            {
                var trainingComparisonsFiles = zip.Entries.Where(f => f.FullName.StartsWith("Comparison_Files") && f.Name.EndsWith("1vs1.txt")).Select(f => f.FullName);
                using (var progress = ProgressHelper.StartNew(trainingComparisonsFiles.Count(), 1))
                {
                    foreach (var fileName in trainingComparisonsFiles)
                    {
                        using (StreamReader sr = new StreamReader(zip.GetEntry(fileName).Open()))
                        {
                            string inputDevice = (fileName.Contains("/") ? fileName.Split('/') : fileName.Split('\\'))[1];

                            while (!sr.EndOfStream)
                            {
                                var line = sr.ReadLine();
                                if (string.IsNullOrWhiteSpace(line))
                                    continue;
                                string[] parts = line.Split(' ');
                                trainingComparisons.Add(new Comparison1v1()
                                {
                                    ReferenceSignatureFile = "Evaluation\\" + inputDevice + "\\" + parts[0],
                                    QuestionedSignatureFile = "Evaluation\\" + inputDevice + "\\" + parts[1],
                                    // reference the signature objects if, a preloaded database is already available
                                    ReferenceSignature = db?["Evaluation\\" + inputDevice + "\\" + parts[0].ToLower()],
                                    QuestionedSignature = db?["Evaluation\\" + inputDevice + "\\" + parts[1].ToLower()],
                                });
                            }

                        }
                        progress.Value++;
                    }
                }
            }
            return trainingComparisons;
        }

        public static void SavePredictions(this IEnumerable<Comparison1v1> comparisons, string filename)
        {
            using (var sw = new StreamWriter(filename, false, Encoding.ASCII))
            {
                foreach (var comparison in comparisons)
                {
                    sw.WriteLine(comparison.Prediction.ToString("n3", numberFormat));
                }
            }
        }

        public static void SaveComparisons(this IEnumerable<Comparison1v1> comparisons, string filename)
        {
            using (ExcelPackage package = new ExcelPackage(new FileInfo(filename)))
            {
                var sheet = package.Workbook.Worksheets.Add("Comparisons " + DateTime.Now);
                ExcelHelper.InsertTable(sheet, 1, 1, comparisons);
                package.Save();

            }
        }

        public static void SaveBenchmarkResults(this IEnumerable<BenchmarkResult> comparisons, string filename)
        {
            using (ExcelPackage package = new ExcelPackage(new FileInfo(filename)))
            {
                var sheet = package.Workbook.Worksheets.Add("Benchmark " + DateTime.Now);
                ExcelHelper.InsertTable(sheet, 1, 1, comparisons);
                package.Save();

            }
        }
    }
}
