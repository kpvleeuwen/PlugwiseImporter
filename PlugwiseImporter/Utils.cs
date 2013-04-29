using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PlugwiseImporter
{

    public static class Utils
    {
        public static void AskIfNullOrEmpty(string prompt, ref string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                Console.WriteLine(prompt);
                value = Console.ReadLine();
            }
        }
    }
}
