using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using log4net;

namespace ClusterClient.Clients
{
    public class SmartRoundRobinClusterClient : ClusterClientBase
    {
        public SmartRoundRobinClusterClient (string[] replicaAddresses)
            : base(replicaAddresses)
        {
            sw = Stopwatch.StartNew();
        }
        
        private static async Task<T> GetFirstSuccessfulTask<T>(IEnumerable<Task<T>> tasks, int timeout)
        {
            var tcs = new TaskCompletionSource<T>();
            var remainingTasks = tasks.Count();
            var sw = new Stopwatch();
            sw.Start();
            foreach (var task in tasks)
                task.ContinueWith(t =>
                {
                    if (task.Status == TaskStatus.RanToCompletion)
                        tcs.TrySetResult(t.Result);
//                    if (sw.Elapsed.TotalMilliseconds > timeout)
//                        tcs.SetException(new TimeoutException());
                    else if (Interlocked.Decrement(ref remainingTasks) == 0)
                        tcs.SetException(new AggregateException(
                            tasks.SelectMany(t2 => t2.Exception?.InnerExceptions ?? Enumerable.Empty<Exception>())));
                });
            
            return tcs.Task.Result;
        }

        private Stopwatch sw;
        
        protected override ILog Log => LogManager.GetLogger(typeof(RandomClusterClient));
        
        private (bool, Task<string>) ProcessSingleRequestAsync(string url, TimeSpan timeout)
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
                        uri + "?query=" + query, timeout));

                    var task = Task.WhenAny(tasks.Select(x => (Task) x.Value))
                        .Wait(TimeSpan.FromMilliseconds((int) timeout.TotalMilliseconds / replicas.Count));
                    
                    if (task)
                    {
                        foreach (var tuple in tasks)
                        {
                            if (tuple.Value.IsCompleted && tuple.Value.Status == TaskStatus.RanToCompletion)
                                return tuple.Value.Result;
                        }
                    }
                    if (sw.Elapsed > timeout)
                        throw new NotImplementedException();
                }
            }
            catch (Exception)
            {
            }

            throw new TimeoutException();
        }
    }
}