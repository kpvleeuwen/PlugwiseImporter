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
        private static string _plugwiseAppliances = string.Empty; // empty means 'All negative (Production) values found'
        private static IUploadMethod[] _plugins;
        private static Dictionary<string, string> _helptext = new Dictionary<string, string>();
        private static DateTime _from;
        private static DateTime _to;
        private static int _days;

        static void Main(string[] args)
        {
            if (!args.Any())
                args = new[] { "help" };
            _plugins = new IUploadMethod[]
            {
                new SonnenErtragUploader(),
                new PvOutputApiUploader(),
            };
            try
            {
                _days = 14;
                _to = DateTime.Now;
                _from = _to.AddDays(-_days);
                ParseCommandline(args);

                DoImport(_from, _to);
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e);
                // Swallow exceptions because it confuses users
                // when Windows throws an error report at them (sorry, Microsoft). 
                Environment.Exit(1); // nonzero error code for scripting support
            }
        }

        private static void ParseCommandline(string[] args)
        {
            foreach (var arg in args)
            {
                if (TryParse(arg, "list", ListAppliances, "Lists all appliances with ID in the plugwise database")) continue;
                if (TryParse(arg, "days", ref _days, "Number of days to load"))
                {
                    _from = _to.AddDays(-_days);
                    continue;
                }
                if (TryParse(arg, "from", ref _from, "First date to load")) continue;
                if (TryParse(arg, "to", ref _to, "Last day to load (not with 'days' option)")) continue;

                foreach (var plugin in _plugins)
                {
                    if (plugin.TryParse(arg)) continue;
                }
                // add help last
                if (TryParse(arg, "help", ShowHelp, "Displays this summary of supported arguments")) continue;
                // fallthrough: only when argument is not parsed
                throw new ArgumentException("Unknown argument: {0}. Try {1} help", arg);
            }
        }

        private static void ShowHelp()
        {
            Console.WriteLine("Commandline summary:");
            foreach (var helpvalue in _helptext.Values)
            {
                Console.WriteLine(helpvalue);
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
            var appliances = string.IsNullOrEmpty(_plugwiseAppliances) ? new int[] { } : _plugwiseAppliances.Split(',').Select(s => int.Parse(s));

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
            _helptext[option] = string.Format("{0} {1}", option.PadRight(20, ' '), helptext);
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
            var description = string.Format("{0}=<{1}>", option, type.Name).PadRight(20, ' ');
            _helptext[option] = string.Format("{0} {1}", description, helptext);
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
        private string _filename;
        public void Push(IEnumerable<YieldAggregate> values)
        {
            if (string.IsNullOrEmpty(_filename))
            {
                Console.WriteLine("No csvfilename, not using CSV output.");
            }
            File.WriteAllLines(_filename, values.Select(
                v => string.Format("{0},{1}",
                    v.Date.ToString(@"dd\/MM\/yy"),
                    (v.Yield * 1000).ToString(System.Globalization.CultureInfo.InvariantCulture)
                    )));
        }


        public bool TryParse(string arg)
        {
            return Program.TryParse(arg, "csvfilename", ref _filename, "CSV output file tu use with PVOutput.org manual bulk uploading");
        }
    }


}
