using SigStat.Common;
using System;
using System.Collections.Generic;
using System.Text;

namespace SVC2021.Entities
{
    class Comparison1v1
    {
        public string ReferenceSignatureFile { get; set; }
        public Svc2021Signature ReferenceSignature { get; set; }
        public string QuestionedSignatureFile { get; set; }
        public Svc2021Signature QuestionedSignature { get; set; }

        public double Prediction { get; set; }
    }
}
