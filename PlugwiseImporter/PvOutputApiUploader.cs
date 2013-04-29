using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.IO;
using PlugwiseImporter.Properties;
using System.Collections.Specialized;

namespace PlugwiseImporter
{

    public class PvOutputApiUploader : IUploadMethod
    {
        Settings s = Settings.Default;

        public void Push(IEnumerable<YieldAggregate> applianceLog)
        {
            // Adds per-day totals
            var uri = new Uri(@"http://pvoutput.org/service/r2/addoutput.jsp");

            var outputSystemId = s.PvOutputSystemId;
            Utils.AskIfNullOrEmpty("Output system Id:", ref outputSystemId);
            var apiKey = s.PvOutputApiKey;
            Utils.AskIfNullOrEmpty("API Key:", ref apiKey);

            Console.WriteLine("Uploading yield for SystemId... {0}", outputSystemId);

            foreach (var log in applianceLog)
            {
                var data = string.Format("{0:yyyyMMdd},{1}", log.Date, Math.Round(log.Yield * 1000).ToString(System.Globalization.CultureInfo.InvariantCulture));
                var values = new NameValueCollection();
                values.Add("data", data.ToString());
                using (WebClient client = new WebClient())
                {
                    client.Headers.Add("X-Pvoutput-Apikey", apiKey);
                    client.Headers.Add("X-Pvoutput-SystemId", outputSystemId);

                    var response = Encoding.ASCII.GetString(client.UploadValues(uri, values));
                    Console.WriteLine("Data: {0} Response: {1}", data, response);
                    File.WriteAllText("response.html", response);
                }
            }
        }
    }
}
