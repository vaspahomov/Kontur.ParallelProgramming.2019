using System;
using System.Collections.Generic;
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
                await Task.WhenAny(Task.WhenAny(resultTasks), Task.Delay(timeout));
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