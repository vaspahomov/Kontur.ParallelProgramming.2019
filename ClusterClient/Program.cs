using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ClusterClient.Clients;
using Fclp;
using log4net;
using log4net.Config;

namespace ClusterClient
{
    internal class Program
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(Program));

        private static void Main(string[] args)
        {
            XmlConfigurator.Configure();

            if (!TryGetReplicaAddresses(args, out var replicaAddresses))
                return;

            try
            {
                var clients = new ClusterClientBase[]
                {
                    new RoundRobinWithLimitedReplicasClusterClient(replicaAddresses),
                    new RoundRobinClusterClient(replicaAddresses),
                    new SmartRoundRobinClusterClient(replicaAddresses),
                    new ParallelOnAllClusterClient(replicaAddresses), 
                    new RandomClusterClient(replicaAddresses),
                };
                var queries = new[]
                {
                    "От", "топота", "копыт", "пыль", "по", "полю", "летит", "На", "дворе", "трава", "на", "траве",
                    "дрова"
                };
//                var queries = Enumerable.Range(1,500).Select(x=>x.ToString());
                foreach (var client in clients)
                {
                    Console.WriteLine("Testing {0} started", client.GetType());
                    Task.WaitAll(queries.Select(
                        async query =>
                        {
                            var timer = Stopwatch.StartNew();
                            try
                            {
                                await client.ProcessRequestAsync(query, TimeSpan.FromSeconds(10));

                                Console.WriteLine("Processed query \"{0}\" in {1} ms", query,
                                    timer.ElapsedMilliseconds);
                            }
                            catch (TimeoutException)
                            {
                                Console.WriteLine("Query \"{0}\" timeout ({1} ms)", query, timer.ElapsedMilliseconds);
                            }
                        }).ToArray());
                    Console.WriteLine("Testing {0} finished", client.GetType());
                }
            }
            catch (Exception e)
            {
//                Console.WriteLine(e);
                Log.Fatal(e);
            }
        }

        private static bool TryGetReplicaAddresses(string[] args, out string[] replicaAddresses)
        {
            var argumentsParser = new FluentCommandLineParser();
            string[] result = { };

            argumentsParser.Setup<string>(CaseType.CaseInsensitive, "f", "file")
                .WithDescription("Path to the file with replica addresses")
                .Callback(fileName => result = File.ReadAllLines(fileName))
                .Required();

            argumentsParser.SetupHelp("?", "h", "help")
                .Callback(text => Console.WriteLine(text));

            var parsingResult = argumentsParser.Parse(args);

            if (parsingResult.HasErrors)
            {
                argumentsParser.HelpOption.ShowHelp(argumentsParser.Options);
                replicaAddresses = null;
                return false;
            }

            replicaAddresses = result;
            return !parsingResult.HasErrors;
        }
    }
}