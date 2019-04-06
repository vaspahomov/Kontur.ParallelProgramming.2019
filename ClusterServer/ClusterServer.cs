using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using log4net;

namespace Cluster
{
	public class ClusterServer
    {
        private readonly ConcurrentDictionary<int, (bool Alive, Task Task, int Id)> handlingRequests 
            = new ConcurrentDictionary<int, (bool, Task, int)>();

        private ConcurrentBag<int> CanceledTasks = new ConcurrentBag<int>();
        
        public ClusterServer(ServerOptions serverOptions, ILog log)
        {
            this.ServerOptions = serverOptions;
            this.log = log;
        }

        public void Start()
        {
            if (Interlocked.CompareExchange(ref isRunning, Running, NotRunning) == NotRunning)
            {
                httpListener = new HttpListener
                {
                    Prefixes =
                    {
                        $"http://+:{ServerOptions.Port}/{ServerOptions.MethodName}/"
                    }
                };

                log.InfoFormat($"Server is starting listening prefixes: {string.Join(";", httpListener.Prefixes)}");

                if (ServerOptions.Async)
                {
                    log.InfoFormat("Press ENTER to stop listening");
                    httpListener.StartProcessingRequestsAsync(CreateAsyncCallback(ServerOptions));
                }
                else
                    httpListener.StartProcessingRequestsSync(CreateSyncCallback(ServerOptions));
            }
        }

        public void Stop()
        {
            if (Interlocked.CompareExchange(ref isRunning, NotRunning, Running) == Running)
            {
				if (httpListener.IsListening)
					httpListener.Stop();
            }
        }

        public ServerOptions ServerOptions { get; }

        private Action<HttpListenerContext> CreateSyncCallback(ServerOptions parsedOptions)
        {
            return context =>
            {
                var currentRequestId = Interlocked.Increment(ref RequestsCount);
                log.InfoFormat("Thread #{0} received request #{1} at {2}",
                    Thread.CurrentThread.ManagedThreadId, currentRequestId, DateTime.Now.TimeOfDay);

                Thread.Sleep(parsedOptions.MethodDuration);

                var encryptedBytes = ClusterHelpers.GetBase64HashBytes(context.Request.QueryString["query"]);
                context.Response.OutputStream.Write(encryptedBytes, 0, encryptedBytes.Length);

                log.InfoFormat("Thread #{0} sent response #{1} at {2}",
                    Thread.CurrentThread.ManagedThreadId, currentRequestId,
                    DateTime.Now.TimeOfDay);
            };
        }

        private Func<HttpListenerContext, Task> CreateAsyncCallback(ServerOptions parsedOptions)
        {
            return async context =>
            {
                var currentRequestNum = Interlocked.Increment(ref RequestsCount);
                var id = int.Parse(context.Request.Headers["id"]);
                
                if (context.Request.Headers.AllKeys.Contains("kill") &&
                    context.Request.Headers["kill"] == "true")
                {
                    foreach (var handlingRequest in handlingRequests)
                    {
                        if (handlingRequest.Value.Id == id)
                            CanceledTasks.Add(id);
                    }
                }
                    
                var query = context.Request.QueryString["query"];
                var task = HandleQueryRequest(
                    context, 
                    query, 
                    currentRequestNum, 
                    parsedOptions.MethodDuration,
                    id);
                handlingRequests[currentRequestNum] = (true, task, id);
                
                await task;
            };
        }

        private async Task HandleQueryRequest(
            HttpListenerContext context, 
            string query, 
            int currentRequestNum, 
            int delay,
            int id)
        {
            log.InfoFormat("Thread #{0} received request '{1}' #{2} at {3}",
                Thread.CurrentThread.ManagedThreadId, query, currentRequestNum, DateTime.Now.TimeOfDay);
            
            await Task.Delay(delay);
            
            var encryptedBytes = ClusterHelpers.GetBase64HashBytes(query);
            
            if (CanceledTasks.Contains(id))
            {
                while (!handlingRequests.TryRemove(currentRequestNum, out var task))
                    await Task.Delay(10);
                log.InfoFormat("Thread #{0} canceled '{1}' #{2} at {3}",
                    Thread.CurrentThread.ManagedThreadId, query, currentRequestNum,
                    DateTime.Now.TimeOfDay);
                return;
            }
            
            await context.Response.OutputStream.WriteAsync(encryptedBytes, 0, encryptedBytes.Length);
            
            log.InfoFormat("Thread #{0} sent response '{1}' #{2} at {3}",
                Thread.CurrentThread.ManagedThreadId, query, currentRequestNum,
                DateTime.Now.TimeOfDay);

            while (!handlingRequests.TryRemove(currentRequestNum, out var task))
                await Task.Delay(10);
        }

     


        private int RequestsCount;

        private int isRunning = NotRunning;

        private const int Running = 1;
        private const int NotRunning = 0;

        private readonly ILog log;
        private HttpListener httpListener;
    }
}
