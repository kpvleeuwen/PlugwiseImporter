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
        /// Identifier of the appliance, 0 if not available
        /// </summary>
        public int ApplianceID { get; set; }
    }
}
