using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using log4net;

namespace Cluster
{
    public class ClusterServer
    {
        private const int Running = 1;
        private const int NotRunning = 0;

        private readonly ConcurrentBag<Guid> canceledTasks = new ConcurrentBag<Guid>();
        private readonly ConcurrentBag<Guid> handlingRequests = new ConcurrentBag<Guid>();

        private readonly ILog log;
        private HttpListener httpListener;

        private int isRunning = NotRunning;
        private int requestsCount;

        public ClusterServer(ServerOptions serverOptions, ILog log)
        {
            ServerOptions = serverOptions;
            this.log = log;
        }

        public ServerOptions ServerOptions { get; }

        public void Start()
        {
            if (Interlocked.CompareExchange(
                    ref isRunning,
                    Running,
                    NotRunning) != NotRunning)
                return;
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
            {
                httpListener.StartProcessingRequestsSync(CreateSyncCallback(ServerOptions));
            }
        }

        public void Stop()
        {
            if (Interlocked.CompareExchange(
                    ref isRunning, 
                    NotRunning, 
                    Running) != Running) 
                return;
            if (httpListener.IsListening)
                httpListener.Stop();
        }

        private Action<HttpListenerContext> CreateSyncCallback(ServerOptions parsedOptions)
        {
            return context =>
            {
                var currentRequestId = Interlocked.Increment(ref requestsCount);
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
                var currentRequestNum = Interlocked.Increment(ref requestsCount);
                var id = Guid.Parse(context.Request.Headers["id"]);
                var query = context.Request.QueryString["query"];

                if (context.Request.Headers.AllKeys.Contains("kill") &&
                    context.Request.Headers["kill"] == "true" &&
                    handlingRequests.Contains(id))
                {
                    canceledTasks.Add(id);
                    return;
                }

                await Task.Delay(parsedOptions.MethodDuration);
                var task = HandleQueryRequest(
                    context,
                    query,
                    currentRequestNum,
                    id);
                handlingRequests.Add(id);
                await task;
            };
        }

        private async Task HandleQueryRequest(
            HttpListenerContext context,
            string query,
            int currentRequestNum,
            Guid id)
        {
            log.InfoFormat("Thread #{0} received request '{1}' #{2} at {3}",
                Thread.CurrentThread.ManagedThreadId, query, currentRequestNum, DateTime.Now.TimeOfDay);

            if (canceledTasks.Contains(id))
            {
                log.InfoFormat("Thread #{0} canceled '{1}' #{2} at {3}",
                    Thread.CurrentThread.ManagedThreadId, query, currentRequestNum,
                    DateTime.Now.TimeOfDay);

                while (!handlingRequests.TryTake(out id))
                    await Task.Delay(10);
                while (!canceledTasks.TryTake(out id))
                    await Task.Delay(10);
            }
            else
            {
                var encryptedBytes = ClusterHelpers.GetBase64HashBytes(query);
                await context
                    .Response
                    .OutputStream
                    .WriteAsync(
                        encryptedBytes,
                        0,
                        encryptedBytes.Length);

                log.InfoFormat("Thread #{0} sent response '{1}' #{2} at {3}",
                    Thread.CurrentThread.ManagedThreadId, query, currentRequestNum,
                    DateTime.Now.TimeOfDay);

                while (!handlingRequests.TryTake(out id))
                    await Task.Delay(10);
            }
        }
    }
}