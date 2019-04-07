using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using log4net;

namespace ClusterClient.Clients
{
    public class RoundRobinWithLimitedReplicasClusterClient : ClusterClientBase
    {
        public RoundRobinWithLimitedReplicasClusterClient(string[] replicaAddresses)
            : base(replicaAddresses)
        {
        }

        protected override ILog Log => LogManager.GetLogger(typeof(RandomClusterClient));

        private (Task<string> Value, Guid Id) ProcessSingleRequestAsync(string url)
        {
            var webRequest = CreateRequest(url);
            Log.InfoFormat("Processing {0}", webRequest.RequestUri);
            var id = Guid.NewGuid();
            webRequest.Headers["Id"] = id.ToString();

            return (ProcessRequestAsync(webRequest), id);
        }

        private async Task KillSingleRequestAsync(string url, Guid id)
        {
            var webRequest = CreateRequest(url);
            Log.InfoFormat("Processing {0}", webRequest.RequestUri);

            webRequest.Headers["Id"] = id.ToString();
            webRequest.Headers["kill"] = "true";

            await ProcessRequestAsync(webRequest);
        }

        private List<string> GetReplicasToExecute(int count = 10)
        {
            const double coefficient = (double) 4 / 5;
            if (UriStatistics.Count <= coefficient * count)
                return ReplicaAddresses
                    .Shuffle()
                    .Take(count)
                    .Shuffle()
                    .ToList();

            var bestReplicas = UriStatistics
                .OrderByDescending(x => x.Value)
                .Take((int)(coefficient * count))
                .Select(x => x.Key)
                .ToList();

            return bestReplicas
                .Concat(ReplicaAddresses
                    .Shuffle()
                    .Take(count - bestReplicas.Count))
                .Shuffle()
                .ToList();
        }

        public override async Task<string> ProcessRequestAsync(string query, TimeSpan timeout)
        {
            var replicas = GetReplicasToExecute();

            var tasks = new ConcurrentBag<((Task<string> Value, Guid Id) Task, string Uri, Stopwatch Sw)>();


            foreach (var uri in replicas.Shuffle())
            {
                var sw = Stopwatch.StartNew();
                tasks.Add((ProcessSingleRequestAsync(
                        uri + "?query=" + query),
                    uri,
                    sw));

                await Task.WhenAny(Task.WhenAny(tasks.Select(x => x.Task.Value)),
                    Task.Delay(TimeSpan.FromMilliseconds(timeout.TotalMilliseconds / replicas.Count / 2)));

                foreach (var ((task, _), l_uri, _) in tasks)
                    if (task.IsCompleted && task.Status == TaskStatus.RanToCompletion)
                    {
                        UriStatistics[l_uri] = sw.Elapsed;
                        sw.Stop();

                        foreach (var _task in tasks)
                            KillSingleRequestAsync(_task.Uri, _task.Task.Id);

                        return await task;
                    }
            }

            foreach (var _task in tasks)
                KillSingleRequestAsync(_task.Uri, _task.Task.Id);

            throw new TimeoutException();
        }
    }
}