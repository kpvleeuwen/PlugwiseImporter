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

namespace PlugwiseImporter
{
    class Program
    {
        static void Main(string[] args)
        {
            var month = DateTime.Now.Month;
            var year = DateTime.Now.Year;
            foreach (var arg in args)
            {
                if (TryParse(arg, "month", ref month)) continue;
                if (TryParse(arg, "year", ref year)) continue;
            }
            IList<YieldAggregate> applianceLog;
            applianceLog = GetPlugwiseYield(month, year);
            Console.WriteLine("Result: {0} days, {1} kWh",
                                applianceLog.Count,
                                applianceLog.Sum(log => log.Yield));

            foreach (var item in applianceLog)
            {
                Console.WriteLine("{0} \t{1}", item.Date, item.Yield);
            }
            var credentials = GetCredentials();

            var logincookie = GetLoginSession(credentials);
            UploadHistory(applianceLog, logincookie);
            Console.WriteLine();
            Console.WriteLine("Press any key to continue...");
            Console.ReadKey();

        }

        private static NetworkCredential GetCredentials()
        {
            var user = Settings.Default.Username;
            if (string.IsNullOrEmpty(user))
            {
                Console.WriteLine("Username:");
                user = Console.ReadLine();
            }
            var password = Settings.Default.Password;
            if (string.IsNullOrEmpty(password))
            {
                Console.WriteLine("Password:");
                password = Console.ReadLine();
            }
            var credentials = new NetworkCredential(user, password);
            return credentials;
        }

        private static bool TryParse(string arg, string option, ref int value)
        {
            if (arg.Contains(option))
            {
                var val = arg.Split('=');
                if (val.Length != 2)
                    throw new ArgumentException(string.Format("Expecting {0}=<int>, no value given", option));
                if (!int.TryParse(val[1], out value))
                    throw new ArgumentException(string.Format("Expecting {0}=<int>, could not parse {1}", option, val[1]));
                return true;
            }
            return false;
        }

        /// <summary>
        /// Queries the plugwise database for the yield in the given month.
        /// </summary>
        /// <param name="month"></param>
        /// <param name="year"></param>
        /// <returns></returns>
        private static IList<YieldAggregate> GetPlugwiseYield(int month, int year)
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

                var latest = (from log in db.Appliance_Logs
                              select log).ToList();

                Console.WriteLine("Loading plugwise production data for year={0} month={1}", year, month);

                var applianceLog = (from log in latest
                                    where log.LogDate.Month == month && log.LogDate.Year == year
                                     && (log.Usage_offpeak + log.Usage_peak) < 0
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
            var path = Settings.Default.PlugwiseDatabasePath;
            if (!string.IsNullOrEmpty(path))
                return new FileInfo(path);
            else return new FileInfo(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                @"..\Local\Plugwise\Source\DB\PlugwiseData.mdb"));
        }

        private static void UploadHistory(IEnumerable<YieldAggregate> applianceLog, WebHeaderCollection logincookie)
        {
            var uri = new Uri(Settings.Default.InsertUri);

            var values = new NameValueCollection();

            Console.WriteLine("Uploading yield for FacilityId {0}", Settings.Default.FacilityId);

            foreach (var log in applianceLog)
            {
                var dateformatted = log.Date.ToString("yyyy-MM-dd");
                values.Add(string.Format("yield[{0}]", dateformatted), log.Yield.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture));
                values.Add(string.Format("is_auto_update[{0}]", dateformatted), "1");
            }

            values.Add("year", applianceLog.First().Date.Year.ToString(System.Globalization.CultureInfo.InvariantCulture));
            values.Add("save", "Save");
            values.Add("pb_id", Settings.Default.FacilityId.ToString(System.Globalization.CultureInfo.InvariantCulture));
            values.Add("order", "asc");

            values.Add("month", applianceLog.First().Date.Month.ToString(System.Globalization.CultureInfo.InvariantCulture));

            using (WebClient client = new WebClient())
            {
                client.Headers.Add(logincookie);
                var response = Encoding.ASCII.GetString(client.UploadValues(uri, values));
                Console.WriteLine("Success: {0}", response.Contains("Data saved!"));
                File.WriteAllText("response.html", response);
            }
        }

        /// <summary>
        /// Logs in and returns the login cookie.
        /// Throws when login is not successful.
        /// </summary>
        /// <returns></returns>
        private static WebHeaderCollection GetLoginSession(NetworkCredential credentials)
        {
            Console.WriteLine("Logging in as {0}", credentials.UserName);
            var uri = new Uri(Settings.Default.LoginUri);
            NameValueCollection logindetails = new NameValueCollection
            {
                { "user", credentials.UserName},
                { "password", credentials.Password},
                { "submit", "Login" },
            };

            using (WebClient client = new WebClient())
            {
                client.Credentials = credentials;
                var response = Encoding.ASCII.GetString(client.UploadValues(uri, logindetails));
                Console.WriteLine("Login result: {0}", response);
                var result = new WebHeaderCollection();
                result.Add(HttpRequestHeader.Cookie, client.ResponseHeaders[HttpResponseHeader.SetCookie]);
                return result;
            }
        }
    }
}
/*
 Request URL:
    http://www.solar-yield.eu/plant/insertdatadaily
  
  
    Request Method:
    POST
  
  
    Status Code:
    HTTP/1.1 200 OK
  



  
    Request Headers
    20:44:43.000
  
  User-Agent:Mozilla/5.0 (Windows NT 6.1; rv:21.0) Gecko/20100101 Firefox/21.0
 * Referer:http://www.solar-yield.eu/plant/insertdatadaily
 * Host:www.solar-yield.eu
 * Connection:keep-alive
 * Accept-Language:en-gb,en;q=0.5
 * Accept-Encoding:gzip, deflate
 * Accept:text/html,application/xhtml+xml,application/xml;q=0.9,**;q=0.8

  
    Sent Cookie
    sun_login:6209dab34e5e0c5d8f102a2b048bcf35
 * PHPSESSID:6d0d876fc1998877686acad297710f9f
 * __utmz:46453029.1366650945.1.1.utmcsr=sonnenertrag.eu|utmccn=(referral)|utmcmd=referral|utmcct=/
 * __utmc:46453029
 * __utmb:46453029.9.10.1366653408
 * __utma:46453029.533701124.1366650945.1366650945.1366653407.2
  

  
  
    Sent Form Data
    yield[2013-04-22]:
 * yield[2013-04-21]:7.10
 * yield[2013-04-20]:7.39
 * yield[2013-04-19]:5.03
 * yield[2013-04-18]:7.43
 * yield[2013-04-17]:3.56
 * yield[2013-04-16]:2.70
 * yield[2013-04-15]:4.28
 * yield[2013-04-14]:5.09
 * yield[2013-04-13]:4.85
 * yield[2013-04-12]:2.19
 * yield[2013-04-11]:0.86
 * yield[2013-04-10]:1.62
 * yield[2013-04-09]:2.17
 * yield[2013-04-08]:4.52
 * yield[2013-04-07]:6.13
 * yield[2013-04-06]:5.46
 * yield[2013-04-05]:3.08
 * yield[2013-04-04]:2.27
 * yield[2013-04-03]:3.41
 * yield[2013-04-02]:6.96
 * yield[2013-04-01]:5.66
 * year:2013
 * save:Save
 * pb_id:20456
 * order:asc
 * month:4
 */