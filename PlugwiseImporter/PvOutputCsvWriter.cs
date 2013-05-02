using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace PlugwiseImporter
{
    public class PvOutputCsvWriter : IUploadMethod
    {
        private string _filename;
        public void Push(IEnumerable<YieldAggregate> values)
        {
            if (string.IsNullOrEmpty(_filename))
            {
                Console.WriteLine("No csvfilename, not using CSV output.");
                return;
            }

            File.WriteAllLines(_filename, values.Select(
                v => string.Format("{0},{1}",
                    v.Date.ToString(@"dd\/MM\/yy"),
                    (v.Yield * 1000).ToString(System.Globalization.CultureInfo.InvariantCulture)
                    )));
        }


        public bool TryParse(string arg)
        {
            return Program.TryParse(arg, "csvfilename", ref _filename, "CSV output file to use with PVOutput.org manual bulk uploading. Disabled when missing.");
        }
    }

}
