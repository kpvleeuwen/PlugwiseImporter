using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Data.OleDb;

namespace PlugwiseImporter
{
    public class Loader : IDisposable
    {
        private PlugwiseDataContext _db;
        private OleDbConnection _connection;
        public Loader(FileInfo database)
            : this()
        {
            string dbConnString = @"Provider=Microsoft.ACE.OLEDB.12.0;Data Source='" + database + "';Persist Security Info=False;";
            _connection = new OleDbConnection(dbConnString);
            _db = new PlugwiseDataContext(_connection);
        }

        protected Loader() { }

        /// <summary>
        /// Queries the plugwise database for the yield in the given month.
        /// </summary>
        /// <param name="from">start point, inclusive</param>
        /// <param name="to">start point, inclusive</param>
        /// <returns></returns>
        public IList<YieldAggregate> GetPlugwiseYield(DateTime from, DateTime to, IEnumerable<int> applianceIds)
        {
            // Querying on a datetime fails somehow
            // As a workaround we list the complete table and use linq to objects for the filter
            // This presents some scalability issues and should be looked in to.
            IList<Appliance_Log> latest;
            if (!applianceIds.Any())
                latest = LoadAllData();
            else
                latest = LoadApplianceData(applianceIds);

            Console.WriteLine("Loading plugwise production data between {0} and {1}", from, to);

            var applianceLog = (from log in latest
                                where
                                  (log.LogDate >= @from) && (log.LogDate <= to) &&
                                  (log.Usage_offpeak + log.Usage_peak) < 0
                                group log by log.LogDate into logsbydate
                                orderby logsbydate.Key
                                where applianceIds.All(
                                    appliance => logsbydate.Any(log => log.ApplianceID == appliance))
                                select new YieldAggregate(
                                    date: logsbydate.Key,
                                    yield: -logsbydate.Sum(log => log.Usage_offpeak + log.Usage_peak),
                                    duration: TimeSpan.FromHours(1)
                                  ))
                                  .ToList();
            return applianceLog;

        }



        /// <summary>
        /// Queries the plugwise database for the yield in the given month.
        /// </summary>
        /// <param name="from">start point, inclusive</param>
        /// <param name="to">start point, inclusive</param>
        /// <returns></returns>
        public IList<YieldAggregate> Get5minPlugwiseYield(DateTime from, IEnumerable<int> applianceIds)
        {
            // Querying on a datetime fails somehow
            // As a workaround we list the complete table and use linq to objects for the filter
            // This presents some scalability issues and should be looked in to.
            IList<Minute_Log_5> latest;
            if (!applianceIds.Any())
                latest = LoadAll5minData();
            else
                latest = Load5minApplianceData(applianceIds);

            Console.WriteLine("Loading 5minute plugwise production data since {0}", from);

            var applianceLog = (from logline in latest
                                where logline.LogDate >= @from.AddHours(-1)
                                from log in Get5minParts(logline)
                                where log.Date >= @from && log.Yield > 0.0
                                group log by log.Date into logbydate
                                orderby logbydate.Key
                                where applianceIds.All(
                                  appliance => logbydate.Any(log => log.ApplianceID == appliance))

                                select new YieldAggregate(
                                    date: logbydate.Key,
                                    yield: logbydate.Sum(l => l.Yield),
                                    duration: logbydate.First().Duration)
                                ).ToList();
            return applianceLog;
        }


        private static IEnumerable<YieldAggregate> Get5minParts(Minute_Log_5 log)
        {
            Func<DateTime, double, YieldAggregate> factory =
                (moment, yield) => new YieldAggregate
                (
                    date: moment,
                    yield: yield,
                    applianceID: log.ApplianceID,
                    duration: TimeSpan.FromMinutes(5)
                );

            if (log.Usage_00 != null)
                yield return factory(log.LogDate.AddMinutes(00), -(double)log.Usage_00);
            if (log.Usage_05 != null)
                yield return factory(log.LogDate.AddMinutes(05), -(double)log.Usage_05);
            if (log.Usage_10 != null)
                yield return factory(log.LogDate.AddMinutes(10), -(double)log.Usage_10);
            if (log.Usage_15 != null)
                yield return factory(log.LogDate.AddMinutes(15), -(double)log.Usage_15);
            if (log.Usage_20 != null)
                yield return factory(log.LogDate.AddMinutes(20), -(double)log.Usage_20);
            if (log.Usage_25 != null)
                yield return factory(log.LogDate.AddMinutes(25), -(double)log.Usage_25);
            if (log.Usage_30 != null)
                yield return factory(log.LogDate.AddMinutes(30), -(double)log.Usage_30);
            if (log.Usage_35 != null)
                yield return factory(log.LogDate.AddMinutes(35), -(double)log.Usage_35);
            if (log.Usage_40 != null)
                yield return factory(log.LogDate.AddMinutes(40), -(double)log.Usage_40);
            if (log.Usage_45 != null)
                yield return factory(log.LogDate.AddMinutes(45), -(double)log.Usage_45);
            if (log.Usage_50 != null)
                yield return factory(log.LogDate.AddMinutes(50), -(double)log.Usage_50);
            if (log.Usage_55 != null)
                yield return factory(log.LogDate.AddMinutes(55), -(double)log.Usage_55);
        }

        protected virtual IList<Appliance_Log> LoadApplianceData(IEnumerable<int> appliances)
        {
            // Two-part query to work around linq-to-Access limitations
            var allapps = (from app in _db.Appliances select app)
                .ToDictionary(app => app.ID);

            var apps = appliances.Select(id => allapps[id]);

            Console.WriteLine("Found {0}", string.Join(";", apps.Select(a => a.Name)));

            var applogs = apps.SelectMany(app =>
                           (from log in _db.Appliance_Logs
                            where log.ApplianceID == app.ID
                            select log)
                           ).ToList();
            return applogs;
        }

        protected virtual IList<Minute_Log_5> Load5minApplianceData(IEnumerable<int> appliances)
        {
            // Two-part query to work around linq-to-Access limitations
            var allapps = (from app in _db.Appliances select app)
                .ToDictionary(app => app.ID);

            var apps = appliances.Select(id => allapps[id]);

            Console.WriteLine("Found {0}", string.Join(";", apps.Select(a => a.Name)));

            var applogs = apps.SelectMany(app =>
                           (from log in _db.Minute_Log_5s
                            where log.ApplianceID == app.ID
                            select log)
                           ).ToList();
            return applogs;
        }

        protected virtual IList<Appliance_Log> LoadAllData()
        {
            var latest = (from log in _db.Appliance_Logs
                          select log).ToList();
            return latest;
        }

        protected virtual IList<Minute_Log_5> LoadAll5minData()
        {
            var latest = (from log in _db.Minute_Log_5s
                          select log).ToList();
            return latest;
        }


        public void Dispose()
        {
            using (_connection)
            using (_db)
            { }
        }
    }
}
