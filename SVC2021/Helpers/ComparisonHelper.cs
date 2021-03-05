using SVC2021.Entities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SVC2021.Helpers
{
    static class ComparisonHelper
    {
        public static IEnumerable<Comparison1v1> LoadComparisons(string fileName, Database db = null)
        {
            using (StreamReader sr = new StreamReader(fileName))
            {
                while (!sr.EndOfStream)
                {
                    var line = sr.ReadLine();
                    if (string.IsNullOrWhiteSpace(line))
                        continue;
                    string [] parts = line.Split(' ');
                    yield return new Comparison1v1()
                    {
                        ReferenceSignatureFile = parts[0],
                        QuestionedSignatureFile = parts[1],
                        // reference the signature objects if, a preloaded database is already available
                        ReferenceSignature = db?[parts[0].ToLower()],
                        QuestionedSignature = db?[parts[1].ToLower()],
                    };
                }
            }
            
        }
    }
}
