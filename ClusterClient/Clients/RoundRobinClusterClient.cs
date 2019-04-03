using System;
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
            foreach (var uri in ReplicaAddresses.Shuffle())
            {
                var (successes, value) = await ProcessSingleRequestAsync(
                    uri + "?query=" + query, timeout);
                if (successes)
                    return value;
            }

            throw new TimeoutException();
        }
    }
}