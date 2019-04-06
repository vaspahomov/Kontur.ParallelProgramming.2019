using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using log4net;

namespace ClusterClient.Clients
{
    public class RoundRobinWithLimitedReplicasClusterClient : ClusterClientBase
    {
        private int requestCounter;
        public RoundRobinWithLimitedReplicasClusterClient(string[] replicaAddresses)
            : base(replicaAddresses)
        {
        }

        protected override ILog Log => LogManager.GetLogger(typeof(RandomClusterClient));

        private (Task<string> Value, int Id) ProcessSingleRequestAsync(string url)
        {
                var webRequest = CreateRequest(url);
                Log.InfoFormat("Processing {0}", webRequest.RequestUri);
                
                webRequest.Headers["Id"] = requestCounter.ToString();
                requestCounter++;
                
                return (ProcessRequestAsync(webRequest), requestCounter);
        }
        private async Task KillSingleRequestAsync(string url, int id)
        {
                var webRequest = CreateRequest(url);
                Log.InfoFormat("Processing {0}", webRequest.RequestUri);
                
                webRequest.Headers["Id"] = id.ToString();
                webRequest.Headers["kill"] = "true";
                
                await ProcessRequestAsync(webRequest);
        }
        
        private List<string> GetReplicasToExecute(int count = 10)
        {
            var replicas = new List<string>();

            if (UriStatistics.Count > 4 * count / 5)
            {
                var bestReplicas = UriStatistics
                    .OrderByDescending(x => x.Value)
                    .Take(count / 2)
                    .Select(x => x.Key);
                replicas = replicas
                    .Concat(bestReplicas)
                    .ToList();
            }

            return replicas
                .Concat(ReplicaAddresses
                    .Shuffle()
                    .Take(count - replicas.Count))
                .Shuffle()
                .ToList();
        }

        public override async Task<string> ProcessRequestAsync(string query, TimeSpan timeout)
        {
            var replicas = GetReplicasToExecute();
            
            var tasks = new ConcurrentBag<((Task<string> Value, int Id) Task, string Uri, Stopwatch Sw)>();

            
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
                        {
                            KillSingleRequestAsync(_task.Uri, _task.Task.Id);
                        }
                        
                        return await task;
                    }
            }
            
            foreach (var _task in tasks)
            {
                KillSingleRequestAsync(_task.Uri, _task.Task.Id);
            }

            throw new TimeoutException();
        }
    }
}