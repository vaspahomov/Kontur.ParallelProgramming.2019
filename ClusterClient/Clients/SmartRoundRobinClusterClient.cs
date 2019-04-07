using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using log4net;

namespace ClusterClient.Clients
{
    public class SmartRoundRobinClusterClient : ClusterClientBase
    {
        private readonly Stopwatch sw;

        public SmartRoundRobinClusterClient(string[] replicaAddresses)
            : base(replicaAddresses)
        {
            sw = Stopwatch.StartNew();
        }

        protected override ILog Log => LogManager.GetLogger(typeof(RandomClusterClient));

        private (bool, Task<string>) ProcessSingleRequestAsync(string url)
        {
            try
            {
                var webRequest = CreateRequest(url);
                Log.InfoFormat("Processing {0}", webRequest.RequestUri);
                return (true, ProcessRequestAsync(webRequest));
            }
            catch (Exception e)
            {
                return (false, default);
            }
        }


        public override async Task<string> ProcessRequestAsync(string query, TimeSpan timeout)
        {
            var tasks = new ConcurrentBag<(bool Success, Task<string> Value)>();

            var replicas = ReplicaAddresses
                .Except(UriStatistics.Keys)
                .Concat(UriStatistics
                    .OrderByDescending(x => x.Value)
                    .Select(x => x.Key))
                .ToList();

            try
            {
                foreach (var uri in replicas)
                {
                    tasks.Add(ProcessSingleRequestAsync(
                        uri + "?query=" + query));

                    await Task.WhenAny(Task.WhenAny(tasks.Select(x => x.Value)),
                        Task.Delay(
                            TimeSpan.FromMilliseconds(timeout.TotalMilliseconds / replicas.Count / 2)));

                    foreach (var task in tasks)
                        if (task.Value.IsCompleted && task.Value.Status == TaskStatus.RanToCompletion)
                            return await task.Value;

                    if (sw.Elapsed > timeout)
                        throw new TimeoutException();
                }
            }
            catch (Exception)
            {
            }

            throw new TimeoutException();
        }
    }
}