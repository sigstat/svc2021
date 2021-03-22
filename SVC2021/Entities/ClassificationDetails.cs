using SigStat.Common.Logging;
using System;
using System.Collections.Generic;
using System.Text;

namespace SVC2021.Entities
{
    class ClassificationDetails: SigStatLogState
    {
        public double Distance { get; internal set; }
        public double GenuineThreshold { get; internal set; }
        public double ForgeryThreshold { get; internal set; }
        public string SignatureID { get; internal set; }
        public string SignerID { get; internal set; }
    }
}
