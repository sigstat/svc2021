using SigStat.Common;
using System;
using System.Collections.Generic;
using System.Text;

namespace SVC2021.Entities
{
    class Comparison1v1
    {
        public string ReferenceSignatureFile { get; set; }
        public Signature ReferenceSignature { get; set; }
        public string QuestionedSignatureFile { get; set; }
        public Signature QuestionedSignature { get; set; }

        public double Decision { get; set; }
    }
}
