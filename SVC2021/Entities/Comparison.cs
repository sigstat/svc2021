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
        [Display(Name = "Reference file")]
        public string ReferenceSignatureFile { get; private set; }
        [ExcelIgnore]
        public Svc2021Signature ReferenceSignature { get; set; }
        [Display(Name = "Reference signer")]
        public string ReferenceSigner { get; private set; }
        [Display(Name = "Reference input")]
        public InputDevice ReferenceInput { get; private set; }

        [Display(Name = "Questioned file")]
        public string QuestionedSignatureFile { get; private set; }
        [ExcelIgnore]
        public Svc2021Signature QuestionedSignature { get; set; }
        [Display(Name = "Questioned signer")]
        public string QuestionedSigner { get; private set; }
        [Display(Name = "Questioned input")]
        public InputDevice QuestionedInput { get; private set; }

        [Display(Name = "Questioned origin")]
        public string Origin { get; private set; }

        [Display(Name = "Prediction")]
        public double Prediction { get; set; }

        [Display(Name = "Expected prediction")]
        public double ExpectedPrediction { get; private set; }

        [Display(Name = "Distance")]
        public double Distance { get; set; }
        [Display(Name = "Genuine threshold")]
        public double GenuineThreshold { get; set; }
        [Display(Name = "Forgery threshold")]
        public double ForgeryThreshold { get; set; }

        public Comparison1v1(string referenceSignatureFile, string questionedSignatureFile)
        {
            ReferenceSignatureFile = referenceSignatureFile;
            QuestionedSignatureFile = questionedSignatureFile;

            var rf = new SignatureFile(referenceSignatureFile);
            var qf = new SignatureFile(questionedSignatureFile);
            if (rf.SignerID != qf.SignerID)
                Origin = "Random";
            else if (qf.Origin == SigStat.Common.Origin.Forged)
                Origin = "Forged";
            else
                Origin = "Genuine";

            ExpectedPrediction = Origin == "Genuine" ? 0 : 1;

            ReferenceSigner = rf.SignerID;
            ReferenceInput = rf.InputDevice;

            QuestionedSigner = qf.SignerID;
            QuestionedInput = qf.InputDevice;
        }
    }
}
