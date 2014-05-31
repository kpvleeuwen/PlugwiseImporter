using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;

namespace PlugwiseImporter.Tests
{
    static class Program
    {
        public static void Main(string[] args)
        {
            new LoaderTests().OnlyComplete5MinDataIsReported();
        }
    }
}
