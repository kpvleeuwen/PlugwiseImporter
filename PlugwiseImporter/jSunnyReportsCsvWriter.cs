using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace PlugwiseImporter
{
    public class jSunnyReportsCsvWriter : IUploadMethod
    {
        private string _outputpath;
        public void Push(IEnumerable<YieldAggregate> values)
        {
            if (string.IsNullOrEmpty(_outputpath))
            {
                Console.WriteLine("no jSunnyReports path, not writing CSV outputs.");
                return;
            }
            foreach (var day in values)
            {
                var filename = string.Format("{0:yyyyMMdd}.csv", day.Date);
                File.WriteAllText(Path.Combine(_outputpath,filename), string.Format("{0:yyyy-MM-dd} 12:00:00;{1}",
                       day.Date,
                       Math.Round(day.Yield * 1000).ToString(System.Globalization.CultureInfo.InvariantCulture)
                       ));
            }
        }

        public bool TryParse(string arg)
        {
            return Program.TryParse(arg, "jsrdir", ref _outputpath, "Import directory for jSunnyReports. This will create a CSV file for every day with the daily yield. Disabled when missing.");
        }
    }

}
