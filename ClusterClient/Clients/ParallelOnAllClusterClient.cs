using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using log4net;

namespace ClusterClient.Clients
{
    public class ParallelOnAllClusterClient:ClusterClientBase
    {
        public ParallelOnAllClusterClient(string[] replicaAddresses)
            : base(replicaAddresses)
        {
        }

        protected override ILog Log => LogManager.GetLogger(typeof(RandomClusterClient));
        public override async Task<string> ProcessRequestAsync(string query, TimeSpan timeout)
        {
            var resultTasks = new List<Task>();
            var bag = new ConcurrentBag<string>();
            var cancellationTokenSource = new CancellationTokenSource();
            foreach (var replicaAddress in ReplicaAddresses)
            {
                var webRequest = CreateRequest(replicaAddress + "?query=" + query);

                Log.InfoFormat("Processing {0}", webRequest.RequestUri);

                var resultTask = ProcessRequestAsync(webRequest)
                    .ContinueWith(x => { bag.Add(x.Result); }, 
                        cancellationTokenSource.Token);
                resultTasks.Add(resultTask);
            }
            await Task.WhenAny(resultTasks)
                .ContinueWith(x=>{cancellationTokenSource.Cancel();});
            
            var res = bag.SingleOrDefault();
            
            return res;
        }
    }
}