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
        }
        
        private static async Task<(bool Success, T Value)> GetFirstSuccessfulTask<T>(IReadOnlyCollection<Task<T>> tasks, TimeSpan timeout)
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
                    if (sw.Elapsed > timeout)
                        tcs.SetException(new TimeoutException());
                    else if (Interlocked.Decrement(ref remainingTasks) == 0)
                        tcs.SetException(new AggregateException(
                            tasks.SelectMany(t2 => t2.Exception?.InnerExceptions ?? Enumerable.Empty<Exception>())));
                });
            
            return (tcs.Task.Status == TaskStatus.RanToCompletion, await tcs.Task);
        }
        
        protected override ILog Log => LogManager.GetLogger(typeof(RandomClusterClient));
        
        private async Task<(bool, string)> ProcessSingleRequestAsync(string url, TimeSpan timeout)
        {
            try
            {
                var webRequest = CreateRequest(url);
                Log.InfoFormat("Processing {0}", webRequest.RequestUri);
                return (true, await ProcessRequestAsync(webRequest));
            }
            catch (Exception e)
            {
                return (false, default);
            }
        }

        public override async Task<string> ProcessRequestAsync(string query, TimeSpan timeout)
        {
            var tasks = new ConcurrentBag<Task<(bool Success, string Value)>>();
            var failedTasks = new ConcurrentBag<Task<(bool Success, string Value)>>();
            
            var replicas = ReplicaAddresses
                .Except(UriStatistics.Keys)
                .Concat(UriStatistics
                    .OrderByDescending(x => x.Value)
                    .Select(x => x.Key));
            
            foreach (var uri in replicas)
            {
                tasks.Add(ProcessSingleRequestAsync(
                    uri + "?query=" + query, timeout));
                
                await Task.WhenAny(
                    Task.WhenAny(tasks), 
                    Task.Delay((int)timeout.TotalMilliseconds / ReplicaAddresses.Length));
                
                
                foreach (var task in tasks.Except(failedTasks))
                {
                    if (task.Status == TaskStatus.Canceled || 
                        task.Status == TaskStatus.Faulted)
                    {
                        failedTasks.Add(task);
                        continue;
                    }
                    if (!task.Result.Success) continue;
                        return task.Result.Value;
                }
                
                tasks = new ConcurrentBag<Task<(bool Success, string Value)>>(
                    tasks.Where(x=>x.Status != TaskStatus.Faulted ||
                                   x.Status != TaskStatus.Canceled)); 
            }
            
            throw new TimeoutException();
        }
    }
}