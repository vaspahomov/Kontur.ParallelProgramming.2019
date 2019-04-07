using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using log4net;

namespace ClusterClient.Clients
{
    public abstract class ClusterClientBase
    {
        protected readonly ConcurrentDictionary<string, TimeSpan> UriStatistics
            = new ConcurrentDictionary<string, TimeSpan>();

        protected ClusterClientBase(string[] replicaAddresses)
        {
            ReplicaAddresses = replicaAddresses;
        }

        protected string[] ReplicaAddresses { get; set; }
        protected abstract ILog Log { get; }

        public abstract Task<string> ProcessRequestAsync(string query, TimeSpan timeout);

        protected static HttpWebRequest CreateRequest(string uriStr)
        {
            var request = WebRequest.CreateHttp(Uri.EscapeUriString(uriStr));
            request.Proxy = null;
            request.KeepAlive = true;
            request.ServicePoint.UseNagleAlgorithm = false;
            request.ServicePoint.ConnectionLimit = 100500;
            return request;
        }

        protected static HttpWebRequest CreateRequestt(string uriStr)
        {
            var request = WebRequest.CreateHttp(Uri.EscapeUriString(uriStr));
            request.Proxy = null;
            request.KeepAlive = true;
            request.ServicePoint.UseNagleAlgorithm = false;
            request.ServicePoint.ConnectionLimit = 100500;
            return request;
        }

        protected async Task<string> ProcessRequestAsync(WebRequest request)
        {
            var timer = Stopwatch.StartNew();

            using (var response = await request.GetResponseAsync())
            {
                var result = await new StreamReader(response?.GetResponseStream(), Encoding.UTF8).ReadToEndAsync();
                Log.InfoFormat($"Response from {request.RequestUri} received in {timer.ElapsedMilliseconds} ms");
                return result;
            }
        }
    }
}