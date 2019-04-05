using System;
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

        private async Task<(bool Success, string Value)> ProcessSingleRequestAsync(string url, int timeout)
        {
            try
            {
                var webRequest = CreateRequest(url);
                Log.InfoFormat("Processing {0}", webRequest.RequestUri);
                var resultTask = ProcessRequestAsync(webRequest);
                await Task.WhenAny(
                    resultTask,
                    Task.Delay(timeout));
                return !resultTask.IsCompleted ? (false, default) : (true, resultTask.Result);
            }
            catch (Exception)
            {
                return (false, default);
            }
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
            foreach (var uri in replicas)
            {
                var sw = new Stopwatch();

                sw.Start();
                var (successes, value) = await ProcessSingleRequestAsync(
                    uri + "?query=" + query, 
                    (int) timeout.TotalMilliseconds / replicas.Count);
                UriStatistics[uri] = sw.Elapsed;
                sw.Stop();

                if (successes)
                    return value;
            }

            throw new TimeoutException();
        }
    }
}