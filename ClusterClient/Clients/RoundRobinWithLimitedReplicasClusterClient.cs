using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Fclp.Internals.Extensions;
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

        private (Task<string> Task, Guid Id) GetExecutingTaskWithGuid(string url)
        {
            var webRequest = CreateRequest(url);
            Log.InfoFormat("Processing {0}", webRequest.RequestUri);
            var id = Guid.NewGuid();
            webRequest.Headers["Id"] = id.ToString();

            return (ProcessRequestAsync(webRequest), id);
        }

        private async Task KillSingleRequestAsync(string uri, Guid id)
        {
            var webRequest = CreateRequest(uri);
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
                .Take((int) (coefficient * count))
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

            var timers = new ConcurrentDictionary<Guid, Stopwatch>();
            var tasks = new ConcurrentDictionary<Guid, Task<string>>();
            var uris = new ConcurrentDictionary<Guid, string>();

            var singleTimeout = TimeSpan.FromMilliseconds(timeout.TotalMilliseconds / replicas.Count);
            foreach (var uri in replicas.Shuffle())
            {
                var sw = Stopwatch.StartNew();
                var (task, id) = GetExecutingTaskWithGuid(uri + "?query=" + query);

                timers[id] = sw;
                tasks[id] = task;
                uris[id] = uri;

                await Task.WhenAny(Task.WhenAny(tasks.Values), Task.Delay(singleTimeout));

                var completedTasks = tasks
                    .Where(x => x.Value.IsCompleted && x.Value.Status == TaskStatus.RanToCompletion)
                    .ToList();

                if (completedTasks.Count <= 0) continue;

                WriteStatistics(timers, completedTasks, uris);
                KillTasks(tasks, completedTasks, uris);

                return await completedTasks.First().Value;
            }

            throw new TimeoutException();
        }

        private void WriteStatistics(
            IReadOnlyDictionary<Guid, Stopwatch> timers,
            IEnumerable<KeyValuePair<Guid, Task<string>>> completedTasks,
            IReadOnlyDictionary<Guid, string> uris)
        {
            completedTasks.ForEach(x =>
            {
                var completedTaskId = x.Key;
                UriStatistics[uris[completedTaskId]] = timers[completedTaskId].Elapsed;
            });
        }

        private void KillTasks(
            IReadOnlyDictionary<Guid, Task<string>> tasks,
            IEnumerable<KeyValuePair<Guid, Task<string>>> completedTasks,
            IReadOnlyDictionary<Guid, string> uris)
        {
            tasks.Except(completedTasks).ToList().ForEach(x =>
            {
                var completedTaskId = x.Key;
                var completedTaskUri = uris[completedTaskId];
                KillSingleRequestAsync(completedTaskUri, completedTaskId);
            });
        }
    }
}