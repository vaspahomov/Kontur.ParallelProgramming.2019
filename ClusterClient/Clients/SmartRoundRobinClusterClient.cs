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
            
//            await Task.WhenAny(
//                resultTask,
//                Task.Delay(
//                    (int)timeout.TotalMilliseconds/ReplicaAddresses.Length ));
            
        }

        public override async Task<string> ProcessRequestAsync(string query, TimeSpan timeout)
        {
            var tasks = new ConcurrentBag<Task<(bool, string)>>();
            foreach (var uri in ReplicaAddresses.Shuffle())
            {
                tasks.Add(ProcessSingleRequestAsync(
                    uri + "?query=" + query, timeout));
                
                await Task.WhenAny(
                    Task.WhenAny(tasks), 
                    Task.Delay((int)timeout.TotalMilliseconds / ReplicaAddresses.Length));

                foreach (var task in tasks)
                {
                    switch (task.Status)
                    {
                        case TaskStatus.RanToCompletion:
                        {
                            if (task.Result.Item1)
                                return task.Result.Item2;
                            break;
                        }
                        case TaskStatus.Faulted:
                        case TaskStatus.Canceled:
                            break;
                    }
                }
            }
            
            throw new TimeoutException();
        }
    }
}