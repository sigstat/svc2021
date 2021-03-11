using SigStat.Common;
using SigStat.Common.Logging;
using SVC2021.Entities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SVC2021
{
    static class Experiments
    {
        public static void TestLoader()
        {
            Svc2021Loader l = new Svc2021Loader(Program.DbPath, true) { Logger = new SimpleConsoleLogger()};
            //string file = "Evaluation\\stylus\\u0114_s_u1015s0001_sg0004.txt";
            //string file = "Evaluation\\stylus\\u0115_s_u1016s0001_sg0003.txt";

            string file = "Evaluation\\finger\\u0377_g_u139_s1_g1_b2_sign_w5_c_007.txt";


            var fi = new Svc2021Loader.SignatureFile(file);
            Svc2021Signature s = new Svc2021Signature() { Split = fi.Split, InputDevice = fi.InputDevice, DB= fi.DB };

            string localFile = Path.Combine(Program.SignaturesDirectory, Path.GetFileName(file));

            l.LoadSignature(s, localFile, true);
            Console.WriteLine(s.X.Count);


        }
    }
}
