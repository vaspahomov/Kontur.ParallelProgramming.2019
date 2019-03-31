using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace CustomPartitioner
{
    internal class FancyLogsPartitioner : Partitioner<string>
    {
        public FancyLogsPartitioner(string filePath)
        {
            if (!File.Exists(filePath))
            {
                Console.WriteLine($"File {filePath} not found");
                Environment.Exit(1);
            }

            var queueCapacity = 1 * 1024;
            queue = new BlockingCollection<List<string>>(new ConcurrentQueue<List<string>>(), queueCapacity);

            Task.Run(() =>
            {
                var listOfLines = new List<string>();

                var buffer = new List<string>();

                using (var fileEnumerator = File.ReadLines(filePath).GetEnumerator())
                {
                    while (fileEnumerator.MoveNext())
                    {
                        var currentLine = fileEnumerator.Current;

                        var isStartOfStackTrace = !currentLine.StartsWith("2015");

                        if (isStartOfStackTrace)
                            listOfLines.Add(currentLine);
                        else
                        {
                            buffer.Add(listOfLines.Count == 1 ? listOfLines[0] : string.Join("\n", listOfLines));
                            listOfLines.Clear();
                            listOfLines.Add(currentLine);
                        }

                        if (buffer.Count > 1024)
                        {
                            queue.Add(buffer);
                            buffer = new List<string>();
                        }
                    }
                }

                if (listOfLines.Count > 0)
                    buffer.Add(string.Join("\n", listOfLines));

                if (buffer.Count > 0)
                    queue.Add(buffer);

                queue.CompleteAdding();
            });
        }

        public override IList<IEnumerator<string>> GetPartitions(int partitionCount)
        {
            return Enumerable.Range(0, partitionCount).Select(_ => GetEnumerator()).ToList();
        }

        private IEnumerator<string> GetEnumerator()
        {
            while (true)
            {
                if (queue.IsCompleted)
                    yield break;

                if (queue.TryTake(out var items, 1000))
                    foreach (var item in items)
                        yield return item;
            }
        }

        private readonly BlockingCollection<List<string>> queue;
    }
}
