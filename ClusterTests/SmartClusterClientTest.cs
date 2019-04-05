using System;
using System.Diagnostics;
using ClusterClient.Clients;
using FluentAssertions;
using NUnit.Framework;

namespace ClusterTests
{
	public class SmartClusterClientTest : ClusterTest
	{
		protected override ClusterClientBase CreateClient(string[] replicaAddresses)
		{
			throw new NotImplementedException();
		}

		[Test]
		public void ClientShouldReturnSuccess_WhenOneReplicaIsGoodAndOthersAreBad()
		{
			CreateServer(Fast);
			CreateServer(Fast, true);
			for (int i = 0; i < 3; i++)
				CreateServer(Slow);

			ProcessRequests(Timeout);
		}

		[Test]
		public void ShouldAbortRequests_AfterTimeout()
		{
			for (var i = 0; i < 10; i++)
				CreateServer(Slow);

			var sw = Stopwatch.StartNew();
			Assert.Throws<TimeoutException>(() => ProcessRequests(Timeout));
			sw.Elapsed.Should().BeCloseTo(TimeSpan.FromMilliseconds(Timeout), TimeSpan.FromSeconds(2));
		}

		[Test]
		public void ShouldNotForgetPreviousAttempt_WhenStartNew()
		{
			CreateServer(1000);
			CreateServer(1000);
			CreateServer(10000);

			ProcessRequests(2700);
		}
	}
}