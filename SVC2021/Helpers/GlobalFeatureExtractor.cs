using SigStat.Common;
using SVC2021.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SVC2021.Helpers
{
    class GlobalFeatureExtractor
    {
        public static double CalculateDuration(Svc2021Signature signature)
        {
            var timeStamps = signature.GetFeature(Features.T);

            double total = timeStamps.Last() - timeStamps.First();
            signature.SetFeature("Duration", total);
            return total;
        }



        public static double CalculateStandardDeviationOfFeature(Svc2021Signature signature, FeatureDescriptor<List<double>> feature)
        {
            var featureValues = signature.GetFeature(feature);

            double mean = featureValues.Average();
            double sum = 0;

            foreach (double val in featureValues)
            {
                sum += (val - mean) * (val - mean);
            }

            double stdDeviation = Math.Sqrt(sum / featureValues.Count);

            signature.SetFeature(FeatureDescriptor.Get<List<double>>("GlobalStdDeviationOf" + feature.Name), stdDeviation);

            return stdDeviation;
        }
    }
}
