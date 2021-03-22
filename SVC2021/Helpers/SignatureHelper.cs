using SigStat.Common;
using SigStat.Common.Transforms;
using SixLabors.ImageSharp;
using SVC2021.Entities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace SVC2021.Helpers
{
    static class SignatureHelper
    {
        public static void SaveImage(this Svc2021Signature signature, string filename)
        {
            Console.WriteLine("Saving "+Path.GetFileName(filename));
            RealisticImageGenerator2 generator = new RealisticImageGenerator2(600, 400)
            {
                X = Features.X,
                Y = Features.Y,
                Pressure = Features.Pressure,
                OutputImage = Features.Image
            };


            string directory = Path.GetDirectoryName(filename);
            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            // Unfortunately, these are expected by the RealisticImageGenerator
            signature.SetFeature(Features.PenDown, signature.Pressure.Select(p => p > 0).ToList());
            signature.SetFeature(Features.Azimuth, signature.X.Select(s => 0d).ToList());
            signature.SetFeature(Features.Altitude, signature.X.Select(s => 0d).ToList());
            generator.Transform(signature);

            signature.Image.Save(filename);
        }
    }
}
