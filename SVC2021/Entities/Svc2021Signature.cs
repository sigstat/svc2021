using SigStat.Common;
using System;
using System.Collections.Generic;
using System.Text;

namespace SVC2021.Entities
{
    /// <summary>
    /// Strongly typed wrapper for representing SVC2021 Signatures
    /// </summary>
    public class Svc2021Signature : Signature
    {
        public string FileName { get => GetFeature(Svc2021.FileName); set => SetFeature(Svc2021.FileName, value); }
        public DB DB { get => GetFeature(Svc2021.DB); set => SetFeature(Svc2021.DB, value); }
        public Split Split { get => GetFeature(Svc2021.Split); set => SetFeature(Svc2021.Split, value); }
        public InputDevice InputDevice { get => GetFeature(Svc2021.InputDevice); set => SetFeature(Svc2021.InputDevice, value); }

        public List<int> X { get => GetFeature(Svc2021.X); set => SetFeature(Svc2021.X, value); }
        public List<int> Y { get => GetFeature(Svc2021.Y); set => SetFeature(Svc2021.Y, value); }
        public List<double> Pressure { get => GetFeature(Svc2021.Pressure); set => SetFeature(Svc2021.Pressure, value); }
        public List<long> T { get => GetFeature(Svc2021.T); set => SetFeature(Svc2021.T, value); }

        public bool IsPreprocessed { get => GetFeature(Svc2021.IsPreprocessed); set => SetFeature(Svc2021.IsPreprocessed, value); }

        public Svc2021Signature()
        {
            IsPreprocessed = false;
        }

    }
}
