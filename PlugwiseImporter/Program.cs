using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data.SqlClient;
using System.IO;
using System.Data.OleDb;
using System.Net;
using System.Reflection;

namespace PlugwiseImporter
{
    class Program
    {
        private static string _plugwiseAppliances;
        private static IUploadMethod[] _plugins;
        private static Dictonary<string, string> _helptext = new Dictionary<string, string>();

        static void Main(string[] args)
        {
            _plugins = new IUploadMethod[]
            {
                new SonnenErtragUploader(),
                new PvOutputApiUploader(),
            };
            try
            {
                var days = 14;
                var to = DateTime.Now;
                var from = to.AddDays(-days);
                string unused = string.Empty;
                foreach (var arg in args)
                {
                    if (TryParse(arg, "list", ListAppliances)) continue;
                    if (TryParse(arg, "days", ref days))
                    {
                        from = to.AddDays(-days);
                        continue;
                    }
                    if (TryParse(arg, "from", ref from)) continue;
                    if (TryParse(arg, "to", ref to)) continue;

                    foreach (var plugin in _plugins)
                    {
                        if (plugin.TryParse(arg)) continue;
                    }
                    // fallthrough: only when argument is not parsed
                    throw new ArgumentException("Unknown argument: {0}", arg);
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

        private static void ListAppliances()
        {

            var dbPath = GetPlugwiseDatabase();
            Console.WriteLine("Loading Plugwise data from {0}", dbPath);

            string dbConnString = @"Provider=Microsoft.ACE.OLEDB.12.0;Data Source='" + dbPath + "';Persist Security Info=False;";
            using (var connection = new OleDbConnection(dbConnString))
            using (var db = new PlugwiseDataContext(connection))
            {
                var allapps = (from app in db.Appliances select app);
                foreach (var app in allapps)
                    Console.WriteLine("{0}\t=\t{1}", app.Name, app.ID);
            }
        }

        private static void DoImport(DateTime from, DateTime to)
        {
            IList<YieldAggregate> applianceLog;
            var appliances = _plugwiseAppliances.Split(',').Select(s => int.Parse(s));

            applianceLog = GetPlugwiseYield(from, to, appliances);
            Console.WriteLine("Result: {0} days, {1} kWh",
                                applianceLog.Count,
                                applianceLog.Sum(log => log.Yield));

            foreach (var item in applianceLog)
            {
                Console.WriteLine("{0} \t{1}", item.Date, item.Yield);
            }

            foreach (var plugin in _plugins)
            {
            }
        }

        public static bool TryParse(string arg, string option, Action a, string helptext = "")
        {
            _helptext[option] = string.Format("{0}\t{1}", option, helptext);
            if (arg == option)
            {
                a();
                return true;
            }
            return false;
        }

        public static bool TryParse<T>(string arg, string option, ref T value, string helptext = "")
        {
            var type = typeof(T);
            _helptext[option] = string.Format("{0}=<{1}>\t{2}", option, type.Name, helptext);
            if (arg.StartsWith(option))
            {
                var val = arg.Split('=');
                if (val.Length != 2)
                    throw new ArgumentException(string.Format("Expecting {0}=<{2}>, no value given", option, type.Name));
                if (val[0] != option) return false;
                try
                {
                    T result = (T)Convert.ChangeType(val[1], type);
                    value = result; return true;
                }
                catch (Exception)
                {
                    throw new ArgumentException(string.Format("Expecting {0}=<{2}>, could not parse {1}", option, val[1], type.Name));
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
        private static IList<YieldAggregate> GetPlugwiseYield(DateTime from, DateTime to, IEnumerable<int> applianceIds)
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
                List<Appliance_Log> latest;
                if (!applianceIds.Any())
                    latest = LoadAllData(db);
                else
                    latest = LoadApplianceData(db, applianceIds);

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

        private static List<Appliance_Log> LoadApplianceData(PlugwiseDataContext db, IEnumerable<int> appliances)
        {
            // Two-part query to work around linq-to-Access limitations
            var allapps = (from app in db.Appliances select app)
                .ToDictionary(app => app.ID);

            var apps = appliances.Select(id => allapps[id]);

            Console.WriteLine("Found {0}", string.Join(";", apps.Select(a => a.Name)));

            var applogs = apps.SelectMany(app =>
                           (from log in db.Appliance_Logs
                            where log.ApplianceID == app.ID
                            select log)
                           ).ToList();
            return applogs;
        }

        private static List<Appliance_Log> LoadAllData(PlugwiseDataContext db)
        {

            var latest = (from log in db.Appliance_Logs
                          select log).ToList();
            return latest;
        }

        /// <summary>
        /// Returns a FileInfo object describing the expected plugwise database file.
        /// Does not check readability/existence.
        /// </summary>
        /// <returns>the expected plugwise database</returns>
        private static FileInfo GetPlugwiseDatabase()
        {
            var _plugwisepath = "";
            if (string.IsNullOrEmpty(_plugwisepath))
                _plugwisepath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                @"..\Local\Plugwise\Source\DB\PlugwiseData.mdb");
            return new FileInfo(_plugwisepath);
        }

    }

    public interface IUploadMethod
    {
        void Push(IEnumerable<YieldAggregate> values);

        bool TryParse(string arg);
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


        public bool TryParse(string arg)
        {
            return false;
        }
    }


}
