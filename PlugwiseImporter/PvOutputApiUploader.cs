using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.IO;
using System.Collections.Specialized;

namespace PlugwiseImporter
{

    public class PvOutputApiUploader : IUploadMethod
    {
        string _outputSystemId;
        string _apiKey;

        public void Push(IEnumerable<YieldAggregate> applianceLog)
        {
            if (string.IsNullOrEmpty(_outputSystemId))
            {
                Console.WriteLine("No PVOutput.org SystemId, not updating PVOutput.");
                return;
            }
            // Adds per-day totals
            var uri = new Uri(@"http://pvoutput.org/service/r2/addoutput.jsp");


            Utils.AskIfNullOrEmpty("API Key:", ref _apiKey);

            Console.WriteLine("Uploading yield for SystemId... {0}", _outputSystemId);

            foreach (var log in applianceLog)
            {
                var data = string.Format("{0:yyyyMMdd},{1}", log.Date, Math.Round(log.Yield * 1000).ToString(System.Globalization.CultureInfo.InvariantCulture));
                var values = new NameValueCollection();
                values.Add("data", data.ToString());
                using (WebClient client = new WebClient())
                {
                    client.Headers.Add("X-Pvoutput-Apikey", _apiKey);
                    client.Headers.Add("X-Pvoutput-SystemId", _outputSystemId);

                    var response = Encoding.ASCII.GetString(client.UploadValues(uri, values));
                    Console.WriteLine("Data: {0} Response: {1}", data, response);
                    File.WriteAllText("response.html", response);
                }
            }
        }

        public bool TryParse(string arg)
        {
            return Program.TryParse(arg, "pvsystemid", ref _outputSystemId, "PVOutput.org System Id")
                || Program.TryParse(arg, "pvapikey", ref _apiKey, "PVOutput.org API Key");
        }
    }
}
