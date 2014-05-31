using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;

namespace PlugwiseImporter.Tests
{
    [TestFixture]
    public class YieldAggregateTests
    {
        /// <summary>
        /// Tests power calculation
        /// </summary>
        [Test]
        public void AveragePower()
        {
            var undertest = new YieldAggregate(DateTime.Now, yield: 1, duration: TimeSpan.FromHours(1));
            Assert.AreEqual(1000, undertest.AveragePower, 1e-10, "1kWh in 1 hour = 1kW");

            undertest = new YieldAggregate(DateTime.Now, yield: 2, duration : TimeSpan.FromHours(1) );
            Assert.AreEqual(2000, undertest.AveragePower, 1e-10, "2kWh in 1 hour = 2kW");

            undertest = new YieldAggregate(DateTime.Now, yield:  1, duration : TimeSpan.FromHours(0.5) );
            Assert.AreEqual(2000, undertest.AveragePower, 1e-10, "1kWh in 30 minutes = 2kW");

            undertest = new YieldAggregate(DateTime.Now, yield: 1 / 60.0, duration : TimeSpan.FromMinutes(1) );
            Assert.AreEqual(1000, undertest.AveragePower, 1e-10, "1/60kWh in 1 minutes = 1kW");

        }
    }
}
