using SigStat.Common;
using SigStat.Common.Helpers.Excel;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;
using static SVC2021.Svc2021Loader;

namespace SVC2021.Entities
{
    class Comparison1v1
    {
        [Display(Name ="Reference file")]
        public string ReferenceSignatureFile { get; set; }
        [ExcelIgnore]
        public Svc2021Signature ReferenceSignature { get; set; }
        [Display(Name = "Reference signer")]
        public string ReferenceSigner { get { return new SignatureFile(ReferenceSignatureFile).SignerID; } }
        [Display(Name = "Reference input")]
        public InputDevice ReferenceInput { get { return new SignatureFile(ReferenceSignatureFile).InputDevice; } }

        [Display(Name = "Questioned file")]
        public string QuestionedSignatureFile { get; set; }
        [ExcelIgnore]
        public Svc2021Signature QuestionedSignature { get; set; }
        [Display(Name = "Questioned signer")]
        public string QuestionedSigner { get { return new SignatureFile(QuestionedSignatureFile).SignerID; } }
        [Display(Name = "Questioned input")]
        public InputDevice QuestionedInput { get { return new SignatureFile(QuestionedSignatureFile).InputDevice; } }

        [Display(Name = "Questioned origin")]
        public string Origin
        {
            get
            {
                var rf = new SignatureFile(ReferenceSignatureFile);
                var qf = new SignatureFile(QuestionedSignatureFile);
                if (rf.SignerID != qf.SignerID)
                    return "Random";
                else if (qf.Origin == SigStat.Common.Origin.Forged)
                    return "Forged";
                else
                    return "Genuine";

            }
        }

        [Display(Name = "Prediction")]
        public double Prediction { get; set; }

        [Display(Name = "Expected prediction")]
        public double ExpectedPrediction
        {
            get { return Origin == "Genuine" ? 0 : 1; }
        }
    }
}
