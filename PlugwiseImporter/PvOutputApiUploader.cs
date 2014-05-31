using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.IO;
using System.Collections.Specialized;
using System.Threading;

namespace PlugwiseImporter
{

    public class PvOutputApiUploader : IUploadMethod
    {
        int _outputSystemId = -1;
        string _apiKey;

        private bool MandatoryInfoMissing()
        {
            if (_outputSystemId <= 0)
            {
                Console.WriteLine("No PVOutput.org SystemId, not updating PVOutput.");
                return true;
            }
            Utils.AskIfNullOrEmpty("API Key:", ref _apiKey);
            Console.WriteLine("Uploading yield for SystemId... {0}", _outputSystemId);
            return false;
        }


        public void Push(IEnumerable<YieldAggregate> applianceLog)
        {
            if (MandatoryInfoMissing()) return;
            // Adds per-day totals
            var uri = new Uri(@"http://pvoutput.org/service/r2/addoutput.jsp");

            var delay = false;
            foreach (var log in applianceLog)
            {
                if (delay) { Thread.Sleep(TimeSpan.FromSeconds(10)); /* as recommended by PVOutput.org*/ }
                delay = true;

                var data = string.Format("{0:yyyyMMdd},{1}", log.Date, Math.Round(log.Yield * 1000).ToString(System.Globalization.CultureInfo.InvariantCulture));
                var values = new NameValueCollection();
                values.Add("data", data.ToString());
                UploadPVOutputValues(uri, values);
            }
        }

        public bool TryParse(string arg)
        {
            return Program.TryParse(arg, "pvsystemid", ref _outputSystemId, "PVOutput.org System Id, when missing PVOutput uploading is disabled.")
                || Program.TryParse(arg, "pvapikey", ref _apiKey, "PVOutput.org API Key, default: ask");
        }

        public void PushIntraday(IEnumerable<YieldAggregate> applianceLog)
        {
            if (MandatoryInfoMissing()) return;
            // Adds intraday totals
            var uri = new Uri(@"http://pvoutput.org/service/r2/addbatchstatus.jsp");

            // PVOutput supports batches of 30 
            var batches = applianceLog.Batch(30);

            var delay = false;
            foreach (var batch in batches)
            {
                var batchlist = batch.ToArray();
                if (delay) { Thread.Sleep(TimeSpan.FromSeconds(10)); /* as recommended by PVOutput.org*/ }
                delay = true;

                var logstrings = (from log in batchlist
                                  select string.Format("{0:yyyyMMdd},{0:HH:mm},-1,{1}",
                                  log.Date,
                                  Math.Round(log.Yield * 1000 * 60 / 5)) // Translate kWh/5min to watts
                                  );
                var data = string.Join(";", logstrings);
                var values = new NameValueCollection();
                values.Add("data", data);
                UploadPVOutputValues(uri, values);

                Properties.Settings.Default.LastIntraDay = batchlist.Last().Date;
                Properties.Settings.Default.Save();
            }

        }

        private void UploadPVOutputValues(Uri uri, NameValueCollection values)
        {
            using (WebClient client = new WebClient())
            {
                client.Headers.Add("X-Pvoutput-Apikey", _apiKey);
                client.Headers.Add("X-Pvoutput-SystemId", _outputSystemId.ToString(System.Globalization.CultureInfo.InvariantCulture));

                var response = Encoding.ASCII.GetString(client.UploadValues(uri, values));
                Console.WriteLine("Response: {0}", response);
                File.WriteAllText("pvoutputresponse.html", response);
            }
        }
    }
}
