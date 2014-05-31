using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PlugwiseImporter
{
    public class YieldAggregate
    {
        /// <summary>
        /// Generated energy in kWh
        /// </summary>
        public double Yield { get; set; }

        /// <summary>
        /// Date of the generation
        /// </summary>
        public DateTime Date { get; set; }

        /// <summary>
        /// The timespan the yield was produced in
        /// </summary>
        public TimeSpan Duration { get; set; }

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
        /// Identifier of the appliance, 0 if not available
        /// </summary>
        public int ApplianceID { get; set; }
    }
}
