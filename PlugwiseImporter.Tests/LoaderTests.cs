﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;

namespace PlugwiseImporter.Tests
{
    [TestFixture]
    public class LoaderTests
    {
        /// <summary>
        /// Tests that timestamps which have missing values are not skipped when we have no specific appliances.
        /// </summary>
        [Test]
        public void InComplete5MinDataIsReported()
        {
            var undertest = new TestLoader();
            var date = new DateTime(2014, 05, 14);
            undertest.Minute_Log_5s = new[]{
                new Minute_Log_5 { ApplianceID = 1, LogDate = date, Usage_00 = -1,   Usage_05 = -1 },
                new Minute_Log_5 { ApplianceID = 2, LogDate = date, Usage_00 = null, Usage_05 = -2, Usage_10 = -2 },
            };

            var result = undertest.Get5minPlugwiseYield(date, new int[0] /*No specific appliances*/);
            var expected = new[] { 
                new YieldAggregate (date.AddMinutes(00), 1, TimeSpan.FromMinutes(5)) , 
                new YieldAggregate (date.AddMinutes(05), 3, TimeSpan.FromMinutes(5)) , 
                new YieldAggregate (date.AddMinutes(10), 2, TimeSpan.FromMinutes(5))
            };
            CollectionAssert.AreEquivalent(expected, result);
        }

        /// <summary>
        /// Tests that timestamps which have missing values are skipped
        /// 
        /// The idea is that the plug is temporarily unreachable and will fill all missing values later.
        /// This prevents uploading data for a single inverter where multiple are needed.
        /// </summary>
        [Test]
        public void OnlyComplete5MinDataIsReported()
        {
            var undertest = new TestLoader();
            var date = new DateTime(2014, 05, 14);
            undertest.Minute_Log_5s = new[]{
                new Minute_Log_5 { ApplianceID = 1, LogDate = date, Usage_00 = -1,   Usage_05 = -1 },
                new Minute_Log_5 { ApplianceID = 2, LogDate = date, Usage_00 = null, Usage_05 = -2, Usage_10 = -2 },
            };

            var result = undertest.Get5minPlugwiseYield(date, new[] { 1, 2 } /*specific appliances*/);
            // We expect a single result using just Usage_05 since that is complete.
            var expected = new[] { 
                new YieldAggregate(date.AddMinutes(5),3, TimeSpan.FromMinutes(5))
            };
            CollectionAssert.AreEquivalent(expected, result);
        }

        /// <summary>
        /// Test mock without database
        /// </summary>
        public class TestLoader : Loader
        {
            public IList<Minute_Log_5> Minute_Log_5s { get; set; }

            public IList<Appliance_Log> Applicance_Logs { get; set; }

            protected override IList<Minute_Log_5> Load5minApplianceData(IEnumerable<int> appliances)
            {
                var appids = new HashSet<int>(appliances);
                return Minute_Log_5s.Where(a => appids.Contains(a.ApplianceID)).ToList();
            }

            protected override IList<Minute_Log_5> LoadAll5minData()
            {
                return Minute_Log_5s;
            }

            protected override IList<Appliance_Log> LoadAllData()
            {
                return Applicance_Logs;
            }

            protected override IList<Appliance_Log> LoadApplianceData(IEnumerable<int> appliances)
            {
                var appids = new HashSet<int>(appliances);
                return Applicance_Logs.Where(a => appids.Contains(a.ApplianceID)).ToList();
            }
        }
    }
}
