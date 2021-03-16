using SigStat.Common;
using SigStat.Common.Pipeline;
using SVC2021.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SVC2021.Pipeline
{
    public class ResetPressureForFingerInput : PipelineBase, ITransformation
    {
        public void Transform(Signature signature)
        {
            var sig = signature as Svc2021Signature;
            if (sig == null)
                return;

            if (sig.InputDevice == InputDevice.Finger)
            {
                for (int i = 0; i < sig.Pressure.Count; i++)
                {
                    sig.Pressure[i] = 0;
                    sig.Svc_Pressure[i] = 0;
                }
            }
        }
    }
}
