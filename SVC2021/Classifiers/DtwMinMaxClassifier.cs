using SigStat.Common.Algorithms;
using SigStat.Common.Helpers;
using SigStat.Common.Pipeline;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using SigStat.Common.Helpers.Serialization;
using SigStat.Common.Logging;
using SigStat.Common;
using SigStat.Common.Algorithms.Distances;
using SVC2021.Entities;

namespace SVC2021.Classifiers
{

    /// <summary>
    /// Represents a trained model for <see cref="DtwMinMaxClassifier"/>
    /// </summary>
    public class DtwMinMaxSignerModel : ISignerModel
    {

        /// <inheritdoc/>
        public string SignerID { get; set; }

        /// <summary>
        /// A list a of genuine signatures used for training
        /// </summary>
        public List<KeyValuePair<string, double[][]>> ReferenceSignatures { get; set; }
        public double GenuineThreshold;
        public double ForgeryThreshold;
        public double CountMinThreshold;
        public double CountMaxThreshold;


        ///// <summary>
        ///// DTW distance matrix of the genuine signatures
        ///// </summary>
        //public DistanceMatrix<string, string, double> DistanceMatrix;


    }
    /// <summary>
    /// Classifies Signatures with the DTW algorithm.
    /// </summary>
    [JsonObject(MemberSerialization.OptOut)]
    public class DtwMinMaxClassifier : PipelineBase, IClassifier
    {
        /// <summary>
        /// The function used to calculate the distance between two data points during DTW calculation
        /// </summary>
        [JsonConverter(typeof(DistanceFunctionJsonConverter))]
        public IDistance<double[][]> DistanceFunction { get; set; } = new DtwDistance();

        /// <summary>
        /// Gets or sets the features to consider during distance calculation
        /// </summary>
        [Input]
        public List<FeatureDescriptor> Features { get; set; } = new List<FeatureDescriptor>();


        /// <inheridoc/>
        public ISignerModel Train(List<Signature> signatures)
        {
            if (signatures == null || signatures.Count == 0)
                throw new ArgumentException("Argument 'signatures' can not be null or an empty list", nameof(signatures));
            var signerID = signatures[0].Signer?.ID;
            var references = signatures.Select(s => new { s.ID, Values = s.GetAggregateFeature(Features).ToArray() }).ToList();
            var distanceMatrix = new DistanceMatrix<string, string, double>();
            foreach (var i in references)
            {
                foreach (var j in references)
                {
                    if (distanceMatrix.ContainsKey(j.ID, i.ID))
                    {
                        distanceMatrix[i.ID, j.ID] = distanceMatrix[j.ID, i.ID];
                    }
                    else if (i == j)
                    {
                        distanceMatrix[i.ID, j.ID] = 0;
                    }
                    else
                    {
                        var distance = DistanceFunction.Calculate(i.Values, j.Values);
                        distanceMatrix[i.ID, j.ID] = distance;
                        this.LogTrace(new ClassifierDistanceLogState(signerID, signerID, i.ID, j.ID, distance));
                    }
                }
            }

            var distances = distanceMatrix.GetValues().Where(v => v != 0);
            var min = distances.Min();
            var max = distances.Max();
            var countMin = references.Min(r => r.Values.Length);
            var countMax = references.Min(r => r.Values.Length);


            return new DtwMinMaxSignerModel
            {
                SignerID = signerID,
                ReferenceSignatures = references.Select(g => new KeyValuePair<string, double[][]>(g.ID, g.Values)).ToList(),
                //DistanceMatrix = distanceMatrix,
                GenuineThreshold = min/10,
                ForgeryThreshold = max + (max - min)*10,
                CountMinThreshold = min * 0.8,
                CountMaxThreshold = max * 1.5,
            };
        }

        /// <inheritdoc/>
        public double Test(ISignerModel model, Signature signature)
        {
            var dtwModel = (DtwMinMaxSignerModel)model;
            var testSignature = signature.GetAggregateFeature(Features).ToArray();

            var distances = new List<double>();
            foreach (var reference in dtwModel.ReferenceSignatures)
            {
                var d = DistanceFunction.Calculate(reference.Value, testSignature);
                distances.Add(d);
                //dtwModel.DistanceMatrix[signature.ID, reference.Key] = d;
                this.LogTrace(new ClassifierDistanceLogState(model.SignerID, signature?.Signer?.ID, reference.Key, signature.ID, d));
            }
            var avgDistance = distances.Average();

            this.LogTrace(new ClassificationDetails()
            {
                SignerID = model.SignerID,
                SignatureID = signature.ID,
                Distance = avgDistance,
                GenuineThreshold = dtwModel.GenuineThreshold,
                ForgeryThreshold = dtwModel.ForgeryThreshold

            });


            //if (avgDistance < dtwModel.CountMinThreshold || avgDistance > dtwModel.CountMaxThreshold)
                //return 0;

            if (avgDistance < dtwModel.GenuineThreshold)
                return 1;
            if (avgDistance > dtwModel.ForgeryThreshold)
                return 0;




            return (dtwModel.ForgeryThreshold - avgDistance) / (dtwModel.ForgeryThreshold - dtwModel.GenuineThreshold);
        }
    }
}
