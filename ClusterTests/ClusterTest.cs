using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Cluster;
using ClusterClient.Clients;
using FluentAssertions;
using log4net;
using log4net.Config;
using NUnit.Framework;

namespace ClusterTests
{
	[TestFixture]
    public abstract class ClusterTest
    {
	    protected const int Slow = 10_000_000;
	    protected const int Fast = 10;
	    protected const int Timeout = 6_000;

	    [Test]
	    public void Client_should_return_success_when_there_is_only_one_fast_replica()
	    {
		    CreateServer(Fast);

		    ProcessRequests(Timeout);
	    }

	    [Test]
	    public void Client_should_return_success_when_all_replicas_are_fast()
	    {
		    for (int i = 0; i < 3; i++)
			    CreateServer(Fast);

		    ProcessRequests(Timeout);
	    }

	    [Test]
	    public void Client_should_fail_when_all_replicas_are_slow()
	    {
		    for (int i = 0; i < 3; i++)
			    CreateServer(Slow);

		    Action action = () => ProcessRequests(Timeout);

		    action.Should().Throw<TimeoutException>();
	    }

		protected abstract ClusterClientBase CreateClient(string[] replicaAddresses);

		[SetUp] public void SetUp() => clusterServers = new List<ClusterServer>();
        [TearDown] public void TearDown() => StopServers();

		protected ClusterServer CreateServer(int delay, bool notStart = false)
		{
			var serverOptions = new ServerOptions
			{
				Async = true, MethodDuration = delay, MethodName = "some_method",
				Port = GetFreePort()
			};

			var server = new ClusterServer(serverOptions, log);
			clusterServers.Add(server);

			if (!notStart)
			{
				server.Start();
				Console.WriteLine($"Started server at port {serverOptions.Port}");
			}

			return server;
		}

		protected TimeSpan[] ProcessRequests(double timeout)
        {
	        var addresses = clusterServers
		        .Select(cs => $"http://localhost:{cs.ServerOptions.Port}/{cs.ServerOptions.MethodName}/")
		        .ToArray();

			var client = CreateClient(addresses);

			Thread.Sleep(1000);

			var queries = new[]
            {
				"lorem", "ipsum", "dolor", "sit", "amet", "consectetuer",
				"adipiscing", "elit", "sed", "diam", "nonummy", "nibh", "euismod",
				"tincidunt", "ut", "laoreet", "dolore", "magna", "aliquam",
				"erat"
			};

            Console.WriteLine("Testing {0} started", client.GetType());
            var result = Task.WhenAll(queries.Select(
	            async query =>
	            {
		            var timer = Stopwatch.StartNew();
		            try
		            {
			            var clientResult = await client.ProcessRequestAsync(query, TimeSpan.FromMilliseconds(timeout));
						timer.Stop();

			            clientResult.Should().Be(Encoding.UTF8.GetString(ClusterHelpers.GetBase64HashBytes(query)));

			            Console.WriteLine("Query \"{0}\" successful ({1} ms)", query, timer.ElapsedMilliseconds);

			            return timer.Elapsed;
		            }
		            catch (TimeoutException)
		            {
			            Console.WriteLine("Query \"{0}\" timeout ({1} ms)", query, timer.ElapsedMilliseconds);
			            throw;
		            }
	            }).ToArray()).GetAwaiter().GetResult();
            Console.WriteLine("Testing {0} finished", client.GetType());
            return result;
        }


        private void StopServers()
        {
            foreach (var clusterServer in clusterServers)
                clusterServer.Stop();
        }

        private static int GetFreePort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            try
            {
                listener.Start();
                return ((IPEndPoint)listener.LocalEndpoint).Port;
            }
            finally
            {
                listener.Stop();
            }
        }

        private List<ClusterServer> clusterServers;

        private readonly ILog log = LogManager.GetLogger(typeof(Program));

        static ClusterTest() => XmlConfigurator.Configure();
    }
}
