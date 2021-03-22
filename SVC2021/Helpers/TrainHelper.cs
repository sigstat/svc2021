using OfficeOpenXml;
using SigStat.Common;
using SigStat.Common.Algorithms.Distances;
using SigStat.Common.Helpers;
using SigStat.Common.Pipeline;
using SVC2021.Entities;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace SVC2021.Helpers
{
    class TrainHelper
    {
        public struct TrainingStatistics
        {
            public string Description { get; set; }
            public double Min { get; set; }
            public double Max { get; set; }
            public double Average { get; set; }
            public double Median { get; set; }
            public double Stdev { get; set; }
        }

        public struct TrainingComparisonData
        {
            public double Distance { get; set; }
            public double ExpectedPrediction { get; set; }
            public InputDevice InputDevice { get; set; }
        }


        static readonly IFormatProvider numberFormat = new CultureInfo("EN-US").NumberFormat;
        public static double Do1v1Comparision(Comparison1v1 comparison, ConditionalSequence pipeline, IDistance<double[][]> distanceFunction, List<FeatureDescriptor> features)
        {
            var refSig = comparison.ReferenceSignature;
            var testSig = comparison.QuestionedSignature;

            pipeline.Transform(refSig);
            pipeline.Transform(testSig);


            return distanceFunction.Calculate(refSig.GetAggregateFeature(features).ToArray(), testSig.GetAggregateFeature(features).ToArray());
        }

        public static void SaveTrainingStatistic(List<TrainingComparisonData> trainingData, List<TrainingStatistics> statistics, string filename)
        {
            using (var sw = new StreamWriter(filename, false, Encoding.ASCII))
            {
                sw.WriteLine("Description;Min;Max;Average;Median;Stdev");
                foreach (var stat in statistics)
                {
                    sw.WriteLine($"{stat.Description};{stat.Min};{stat.Max};{stat.Average};{stat.Median};{stat.Stdev}");
                }

                sw.WriteLine("-1");

                foreach (var data in trainingData)
                {
                    sw.WriteLine($"{data.Distance};{data.ExpectedPrediction};{data.InputDevice}");
                }
            }

        }

        public static void LoadTrainingStatistic(string filename, out List<TrainingStatistics> comparisionStatistics)
        {
            using (var sr = new StreamReader(filename, Encoding.ASCII, false))
            {
                comparisionStatistics = new List<TrainingStatistics>();
                var line = sr.ReadLine();
                line = sr.ReadLine();
                while (line != "-1")
                {
                    var lineParts = line.Split(";");
                    comparisionStatistics.Add(new TrainingStatistics()
                    {
                        Description = lineParts[0],
                        Min = Convert.ToDouble(lineParts[1]),
                        Max = Convert.ToDouble(lineParts[2]),
                        Average = Convert.ToDouble(lineParts[3]),
                        Median = Convert.ToDouble(lineParts[4]),
                        Stdev = Convert.ToDouble(lineParts[5])
                    });

                    line = sr.ReadLine();
                }

            }
        }
    }
}
