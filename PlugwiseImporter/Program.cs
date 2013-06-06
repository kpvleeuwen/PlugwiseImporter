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
        private static HashSet<string> _parsedArguments = new HashSet<string>();
        private static DateTime _to;
        private static int _days;

        private static bool _verbose;

        static void Main(string[] args)
        {
            if (!args.Any())
                args = new[] { "help" };
            _plugins = new IUploadMethod[]
            {
                new SonnenErtragUploader(),
                new PvOutputApiUploader(),
                new PvOutputCsvWriter(),
                new jSunnyReportsCsvWriter(),
            };
            try
            {
                _days = 14;
                _to = DateTime.Now.Date.AddDays(1);
                ParseCommandline(args);

                var from = _to.AddDays(-_days);
                DoImport(from, _to);
            }
            catch (Exception e)
            {
                if (_verbose)
                    Console.Error.WriteLine(e);
                else
                    Console.Error.WriteLine(e.Message);

                // Swallow exceptions because it confuses users
                // when Windows throws an error report at them (sorry, Microsoft). 
                Environment.Exit(1); // nonzero error code for scripting support
            }
        }

        private static void ParseCommandline(string[] args)
        {
            foreach (var arg in args)
            {
                if (string.IsNullOrWhiteSpace(arg)) continue; // for example escaped newlines in batch files

                if (TryParse(arg, "list", ListAppliances, "Lists all appliances with ID in the plugwise database")) continue;
                if (TryParse(arg, "appliances", ref _plugwiseAppliances, "Comma-separated list of applianceIDs to use, default: all production")) continue;
                if (TryParse(arg, "days", ref _days, string.Format("Number of days to load, default: {0}", _days))) continue;
                if (TryParse(arg, "to", ref _to, "Last day to load, defaults to today")) continue;
                if (TryParse(arg, "verbose", () => { _verbose = true; }, "Give detailed error messages")) continue;

                if (_plugins.Any(p => p.TryParse(arg))) continue;

                // add help last so all args have been read
                if (TryParse(arg, "help", ShowHelp, "Displays this summary of supported arguments")) Environment.Exit(2);
                // fallthrough: only when argument is not parsed
                throw new ArgumentException(string.Format("Unknown argument: {0}. Try {1} help", arg, Path.GetFileName(Assembly.GetExecutingAssembly().Location)));
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
                    Console.WriteLine("{0} =\t{1}", app.Name.PadRight(15, ' '), app.ID);
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
                Console.WriteLine("{0} \t{1}", item.Date.ToString("d"), item.Yield);
            }

            foreach (var plugin in _plugins)
            {
                plugin.Push(applianceLog);
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
                    value = result;
                    if (_parsedArguments.Contains(option))
                        throw new ArgumentException(string.Format("Option {0} can be specified only once", option));
                    _parsedArguments.Add(option);
                    return true;
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
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                @"Plugwise\Source\DB\PlugwiseData.mdb");
            return new FileInfo(_plugwisepath);
        }

    }

    public interface IUploadMethod
    {
        void Push(IEnumerable<YieldAggregate> values);

        bool TryParse(string arg);
    }


}
