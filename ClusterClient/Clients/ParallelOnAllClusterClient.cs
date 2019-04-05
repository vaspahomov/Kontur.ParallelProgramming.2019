using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using log4net;

namespace ClusterClient.Clients
{
    public class ParallelOnAllClusterClient : ClusterClientBase
    {
        public ParallelOnAllClusterClient(string[] replicaAddresses)
            : base(replicaAddresses)
        {
        }

        protected override ILog Log => LogManager.GetLogger(typeof(RandomClusterClient));

        private static async Task<T> GetFirstSuccessfulTask<T>(IReadOnlyCollection<Task<T>> tasks, TimeSpan timeout)
        {
            var tcs = new TaskCompletionSource<T>();
            var remainingTasks = tasks.Count;
            var sw = new Stopwatch();
            sw.Start();
            foreach (var task in tasks)
                task.ContinueWith(t =>
                {
                    if (task.Status == TaskStatus.RanToCompletion)
                        tcs.TrySetResult(t.Result);
                    else if (sw.Elapsed > timeout)
                        throw new TimeoutException();
//                        tcs.SetException(new TimeoutException());
                    else if (Interlocked.Decrement(ref remainingTasks) == 0)
                        tcs.SetException(new AggregateException(
                            tasks.SelectMany(t2 => t2.Exception?.InnerExceptions ?? Enumerable.Empty<Exception>())));
                });
            
            return await tcs.Task;
        }

        public override async Task<string> ProcessRequestAsync(string query, TimeSpan timeout)
        {
            var resultTasks = new List<Task<string>>();
            foreach (var replicaAddress in ReplicaAddresses)
            {
                var webRequest = CreateRequest(replicaAddress + "?query=" + query);

                Log.InfoFormat("Processing {0}", webRequest.RequestUri);

                var resultTask = ProcessRequestAsync(webRequest);
                resultTasks.Add(resultTask);
            }

            try
            {
                Task.WhenAny(resultTasks)
                    .Wait(TimeSpan.FromMilliseconds((int) timeout.TotalMilliseconds / ReplicaAddresses.Length));
                foreach (var tuple in resultTasks)
                {
                    if (tuple.IsCompleted && tuple.Status == TaskStatus.RanToCompletion)
                        return tuple.Result;
                }
                throw new TimeoutException();
            }
            catch (Exception e)
            {
                Log.Error(e);
                throw new TimeoutException();
            }
        }
    }
}