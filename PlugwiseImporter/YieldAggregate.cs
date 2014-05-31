using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PlugwiseImporter
{
    public class YieldAggregate
    {
        public YieldAggregate(DateTime date, double yield, TimeSpan duration, int? applianceID = null)
        {
            Date = date;
            Yield = yield;
            Duration = duration;
            ApplianceID = applianceID;
        }
        /// <summary>
        /// Generated energy in kWh
        /// </summary>
        public double Yield { get; private set; }

        /// <summary>
        /// Date of the generation
        /// </summary>
        public DateTime Date { get; private set; }

        /// <summary>
        /// The timespan the yield was produced in
        /// </summary>
        public TimeSpan Duration { get; private set; }

        /// <summary>
        /// Power in watts
        /// </summary>
        public double AveragePower
        {
            get
            {
                return 1000 * Yield / Duration.TotalHours;
            }
        }
        /// <summary>
        /// Identifier of the appliance, null if not available
        /// </summary>
        public int? ApplianceID { get; private set; }

        public override string ToString()
        {
            return string.Format("{0} Date {1} Yield {2} Duration {3}", ApplianceID, Date, Yield, Duration);
        }

        public override bool Equals(object obj)
        {
            if (obj == null) return false;
            return ToString() == obj.ToString();
        }

        public override int GetHashCode()
        {
            return ToString().GetHashCode();
        }
    }
}
