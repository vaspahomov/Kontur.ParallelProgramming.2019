using System;
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
        
        private Task<string> ProcessSingleRequestAsync(string url, TimeSpan timeout)
        {
            var webRequest = CreateRequest(url);
            Log.InfoFormat("Processing {0}", webRequest.RequestUri);
            var resultTask = ProcessRequestAsync(webRequest);
            
//            await Task.WhenAny(
//                resultTask,
//                Task.Delay(
//                    (int)timeout.TotalMilliseconds/ReplicaAddresses.Length ));
            
            return resultTask;
        }

        public override async Task<string> ProcessRequestAsync(string query, TimeSpan timeout)
        {
            var tasks = new List<Task<string>>();
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
                            return task.Result;
                        case TaskStatus.Faulted:
                        case TaskStatus.Canceled:
                            tasks.Remove(task);
                            break;
                    }
                }
            }
            
            throw new TimeoutException();
        }
    }
}