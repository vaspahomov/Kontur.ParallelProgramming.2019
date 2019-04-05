using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using log4net;

namespace ClusterClient.Clients
{
    public class RoundRobinClusterClient : ClusterClientBase
    {
        public RoundRobinClusterClient(string[] replicaAddresses)
            : base(replicaAddresses)
        {
        }

        protected override ILog Log => LogManager.GetLogger(typeof(RandomClusterClient));

        private async Task<(bool Success, string Value)> ProcessSingleRequestAsync(string url, TimeSpan timeout)
        {
            try
            {
                var webRequest = CreateRequest(url);
                Log.InfoFormat("Processing {0}", webRequest.RequestUri);
                var resultTask = ProcessRequestAsync(webRequest);
                await Task.WhenAny(
                    resultTask,
                    Task.Delay((int) timeout.TotalMilliseconds / ReplicaAddresses.Length));
                return !resultTask.IsCompleted ? (false, default) : (true, resultTask.Result);
            }
            catch (Exception)
            {
                return (false, default);
            }
        }

        public override async Task<string> ProcessRequestAsync(string query, TimeSpan timeout)
        {
            var replicas = ReplicaAddresses
                .Except(UriStatistics.Keys)
                .Concat(UriStatistics
                    .OrderByDescending(x => x.Value)
                    .Select(x => x.Key));
            
            foreach (var uri in replicas)
            {
                var sw = new Stopwatch();
                sw.Start();
                var (successes, value) = await ProcessSingleRequestAsync(
                    uri + "?query=" + query, timeout);
                UriStatistics[uri] = sw.Elapsed;
                sw.Stop();
                if (successes)
                    return value;
            }

            throw new TimeoutException();
        }
    }
}