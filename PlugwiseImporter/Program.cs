using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data.SqlClient;
using System.IO;
using System.Data.OleDb;
using System.Net;
using System.Collections.Specialized;
using PlugwiseImporter.Properties;
using System.Reflection;

namespace PlugwiseImporter
{
    class Program
    {
        static Settings s = Settings.Default;

        static void Main(string[] args)
        {
            try
            {
                var days = 14;
                var to = DateTime.Now;
                var from = to.AddDays(-days);
                string unused = string.Empty;
                foreach (var arg in args)
                {
                    if (TryParse(arg, "days", ref days))
                    {
                        from = to.AddDays(-days);
                        continue;
                    }
                    if (TryParse(arg, "from", ref from)) continue;
                    if (TryParse(arg, "to", ref to)) continue;
                    if (TryParse(arg, "dumpconfig", ref unused))
                    {
                        DumpSettingStats();
                    }
                }

                DoImport(from, to);
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e);
                Console.ReadLine();
                // Swallow exceptions because it confuses users
                // when Windows throws an error report at them (sorry, Microsoft). 
            }
        }

        // Debugging 
        private static void DumpSettingStats()
        {

            var exe = Assembly.GetExecutingAssembly();
            var configfile = new FileInfo(exe.Location + ".config");
            // Try to open for read since the setting framework does that too
            Console.WriteLine("Dumping config contents");
            using (var stream = configfile.OpenText())
            {
                Console.WriteLine(stream.ReadToEnd());
            }
        }

        private static void DoImport(DateTime from, DateTime to)
        {
            IList<YieldAggregate> applianceLog;
            applianceLog = GetPlugwiseYield(from, to);
            Console.WriteLine("Result: {0} days, {1} kWh",
                                applianceLog.Count,
                                applianceLog.Sum(log => log.Yield));

            foreach (var item in applianceLog)
            {
                Console.WriteLine("{0} \t{1}", item.Date, item.Yield);
            }

            if (!string.IsNullOrWhiteSpace(s.PvOutputApiKey))
                new PvOutputApiUploader().Push(applianceLog);

            if (!string.IsNullOrWhiteSpace(s.InsertUri))
                new SonnenErtragUploader().Push(applianceLog);
        }

        private static bool TryParse<T>(string arg, string option, ref T value)
        {
            if (arg.Contains(option))
            {
                var val = arg.Split('=');
                if (val.Length != 2)
                    throw new ArgumentException(string.Format("Expecting {0}=<int>, no value given", option));
                try
                {
                    T result = (T)Convert.ChangeType(val[1], typeof(T));
                    value = result; return true;
                }
                catch (Exception)
                {
                    throw new ArgumentException(string.Format("Expecting {0}=<{2}>, could not parse {1}", option, val[1], typeof(T).Name));
                }
            }
            return false;
        }

        /// <summary>
        /// Queries the plugwise database for the yield in the given month.
        /// </summary>
        /// <param name="from">start point, inclusive</param>
        /// <param name="to">start point, inclusive</param>
        /// <returns></returns>
        private static IList<YieldAggregate> GetPlugwiseYield(DateTime from, DateTime to)
        {
            from = from.Date;
            to = to.Date.AddDays(1);
            var dbPath = GetPlugwiseDatabase();
            Console.WriteLine("Loading Plugwise data from {0}", dbPath);

            string dbConnString = @"Provider=Microsoft.ACE.OLEDB.12.0;Data Source='" + dbPath + "';Persist Security Info=False;";
            using (var connection = new OleDbConnection(dbConnString))
            using (var db = new PlugwiseDataContext(connection))
            {
                // Querying on a datetime fails somehow
                // As a workaround we list the complete table and use linq to objects for the filter
                // This presents some scalability issues and should be looked in to.

                var latest = (from log in db.Appliance_Logs
                              select log).ToList();

                Console.WriteLine("Loading plugwise production data between {0} and {1}", from, to);

                var applianceLog = (from log in latest
                                    where
                                      (log.LogDate >= @from) && (log.LogDate <= to) &&
                                      (log.Usage_offpeak + log.Usage_peak) < 0
                                    group log by log.LogDate into logsbydate
                                    orderby logsbydate.Key
                                    select new YieldAggregate
                                    {
                                        Date = logsbydate.Key,
                                        Yield = -logsbydate.Sum(log => log.Usage_offpeak + log.Usage_peak)
                                    })
                                      .ToList();
                return applianceLog;
            }
        }

        /// <summary>
        /// Returns a FileInfo object describing the expected plugwise database file.
        /// Does not check readability/existence.
        /// </summary>
        /// <returns>the expected plugwise database</returns>
        private static FileInfo GetPlugwiseDatabase()
        {
            var path = s.PlugwiseDatabasePath;
            if (!string.IsNullOrEmpty(path))
                return new FileInfo(path);
            else return new FileInfo(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                @"..\Local\Plugwise\Source\DB\PlugwiseData.mdb"));
        }

    }

    public interface IUploadMethod
    {
        void Push(IEnumerable<YieldAggregate> values);
    }

    public class PvOutputCsvWriter : IUploadMethod
    {
        public void Push(IEnumerable<YieldAggregate> values)
        {
            File.WriteAllLines("output.csv", values.Select(
                v => string.Format("{0},{1}",
                    v.Date.ToString(@"dd\/MM\/yy"),
                    (v.Yield * 1000).ToString(System.Globalization.CultureInfo.InvariantCulture)
                    )));
        }
    }


}
