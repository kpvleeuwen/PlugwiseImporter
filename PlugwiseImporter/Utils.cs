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

        public static IEnumerable<IEnumerable<T>> Batch<T>(this IEnumerable<T> items, int batchsize)
        {
            return items.Zip(Enumerable.Range(0, int.MaxValue), Tuple.Create)
              .GroupBy(t => t.Item2 / batchsize, t => t.Item1);
        }
    }
}
