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

        public static void SaveTrainingStatistic(List<TrainingComparisonData> trainingData, TrainingStatistics genuineStatistics, TrainingStatistics forgedStatistics, string filename)
        {
            using (var sw = new StreamWriter(filename, false, Encoding.ASCII))
            {
                sw.WriteLine($"Genuine;{genuineStatistics.Min};{genuineStatistics.Max};{genuineStatistics.Average};{genuineStatistics.Median};{genuineStatistics.Stdev}");
                sw.WriteLine($"Forged;{forgedStatistics.Min};{forgedStatistics.Max};{forgedStatistics.Average};{forgedStatistics.Median};{forgedStatistics.Stdev}");

                foreach (var data in trainingData)
                {
                    sw.WriteLine($"{data.Distance};{data.ExpectedPrediction}");
                }
            }

        }

        public static void LoadTrainingStatistic(string filename, out TrainingStatistics genuineComparisonStat, out TrainingStatistics forgedComparisonStat)
        {
            using (var sr = new StreamReader(filename, Encoding.ASCII, false))
            {
                var genuineLineParts = sr.ReadLine().Split(";");

                genuineComparisonStat = new TrainHelper.TrainingStatistics()
                {
                    Min = Convert.ToDouble(genuineLineParts[1]),
                    Max = Convert.ToDouble(genuineLineParts[2]),
                    Average = Convert.ToDouble(genuineLineParts[3]),
                    Median = Convert.ToDouble(genuineLineParts[4]),
                    Stdev = Convert.ToDouble(genuineLineParts[5])
                };

                var forgedLineParts = sr.ReadLine().Split(";");

                forgedComparisonStat = new TrainHelper.TrainingStatistics()
                {
                    Min = Convert.ToDouble(forgedLineParts[1]),
                    Max = Convert.ToDouble(forgedLineParts[2]),
                    Average = Convert.ToDouble(forgedLineParts[3]),
                    Median = Convert.ToDouble(forgedLineParts[4]),
                    Stdev = Convert.ToDouble(forgedLineParts[5])
                };

            }
        }
    }
}
