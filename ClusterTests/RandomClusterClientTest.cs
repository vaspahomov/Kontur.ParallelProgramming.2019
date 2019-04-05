using System;
using ClusterClient.Clients;
using FluentAssertions;
using NUnit.Framework;

namespace ClusterTests
{
	public class RandomClusterClientTest : ClusterTest
	{
		protected override ClusterClientBase CreateClient(string[] replicaAddresses) => new RandomClusterClient(replicaAddresses);
	}
}