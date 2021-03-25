// This code requires the Nuget package Microsoft.AspNet.WebApi.Client to be installed.
// Instructions for doing this in Visual Studio:
// Tools -> Nuget Package Manager -> Package Manager Console
// Install-Package Newtonsoft.Json

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;


namespace SVC2021.Helpers
{
    class AzureHelper
    {

        const int batchMaxSize = 1000;

        public static void GetPredictions(string inputCsvFile, string predictionFile, string apiAddress, string [] skipColumns)
        {
            //ExpectedResult;stdevX1;stdevY1;stdevP1;count1;duration1;stdevX2;stdevY2;stdevP2;count2;duration2;diffDTW;diffX;diffY;diffP;diffCount;diffDuration
            using var sr = new StreamReader(inputCsvFile);
            var allHeaders = sr.ReadLine().Split(';');//skip first column (ExpectedResult)
            var skipIndexes = skipColumns.Select(c => allHeaders.IndexOf(c)).ToArray();
            var headers = allHeaders.Skip(skipIndexes);

            using var sw = new StreamWriter(predictionFile);

            while (!sr.EndOfStream)
            {
                var batch = new List<Dictionary<string, string>>();
                int batchcnt = 0;
                while (!sr.EndOfStream && batchcnt < batchMaxSize)
                {
                    var line = sr.ReadLine().Split(';').Skip(skipIndexes);//skip first column (ExpectedResult)
                    batchcnt++;
                    batch.Add(headers.Zip(line, (h, l) => new { h, l }).ToDictionary(item => item.h, item => item.l));
                }

                IEnumerable<int> batchPredictions;
                try
                {
                    batchPredictions = InvokeRequestResponseService(batch, apiAddress).Result;
                }
                catch (AggregateException)
                {//returns -1 where prediction fails
                    batchPredictions = Enumerable.Repeat(-1, batch.Count);
                }
                foreach (var pred in batchPredictions)
                    sw.WriteLine(pred);
            }

            sw.Flush();
            sw.Close();
        }

        public static async Task<IEnumerable<int>> InvokeRequestResponseService(List<Dictionary<string, string>> batch, string apiAddress)
        {
            var handler = new HttpClientHandler()
            {
                ClientCertificateOptions = ClientCertificateOption.Manual,
                ServerCertificateCustomValidationCallback =
                        (httpRequestMessage, cert, cetChain, policyErrors) => { return true; }
            };
            using (var client = new HttpClient(handler))
            {
                var scoreRequest = new Dictionary<string, List<Dictionary<string, string>>>()
                {
                    {"data", batch},
                };

                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "placeholderforapikey");
                client.BaseAddress = new Uri(apiAddress);

                // WARNING: The 'await' statement below can result in a deadlock
                // if you are calling this code from the UI thread of an ASP.Net application.
                // One way to address this would be to call ConfigureAwait(false)
                // so that the execution does not attempt to resume on the original context.
                // For instance, replace code such as:
                //      result = await DoSomeTask()
                // with the following:
                //      result = await DoSomeTask().ConfigureAwait(false)

                var requestString = JsonConvert.SerializeObject(scoreRequest);
                var content = new StringContent(requestString);

                content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

                HttpResponseMessage response = await client.PostAsync("", content);

                if (response.IsSuccessStatusCode)
                {
                    Console.Write('.');
                    string result = await response.Content.ReadAsStringAsync();
                    result = result.TrimStart('\"').TrimEnd('\"').Replace("\\", "");
                    var resultDict = JsonConvert.DeserializeObject<Dictionary<string, IEnumerable<int>>>(result)["result"].ToList();
                    return resultDict;
                }
                else
                {
                    Console.WriteLine(string.Format("The request failed with status code: {0}", response.StatusCode));

                    // Print the headers - they include the requert ID and the timestamp,
                    // which are useful for debugging the failure
                    Console.WriteLine(response.Headers.ToString());

                    string responseContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine(responseContent);

                    throw new HttpRequestException(response.StatusCode.ToString());
                }
            }
        }
    }
}