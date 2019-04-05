using System;
using System.Runtime.InteropServices;
using ClusterClient.Clients;
using NUnit.Framework;

namespace ClusterTests
{
	public class StupidClusterClientTest : ClusterTest
	{
		protected override ClusterClientBase CreateClient(string[] replicaAddresses)
		{
			return new ParallelOnAllClusterClient(replicaAddresses);
		}

		private const int Fast = 1000;

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
		public void ClientShouldReturnSuccess_WhenTimeoutIsClose()
		{
			for (int i = 0; i < 4; i++)
				CreateServer(Fast);

			ProcessRequests(Fast + 100);
		}
	}
}