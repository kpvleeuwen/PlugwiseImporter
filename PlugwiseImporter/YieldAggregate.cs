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
        /// Date of the generation, the time part is meaningless.
        /// </summary>
        public DateTime Date { get; set; }
    }
}
