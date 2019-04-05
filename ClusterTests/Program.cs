namespace ClusterTests
{
	public static class Program
	{
		public static void Main()
		{
			var fUnit = new FUnitLite();
			
			fUnit.AddTestFixture(new RandomClusterClientTest());
			fUnit.AddTestFixture(new StupidClusterClientTest());
			fUnit.AddTestFixture(new RoundRobinClusterClientTest());
			fUnit.AddTestFixture(new SmartClusterClientTest());
			
			fUnit.RunAndReport();
		}
	}
}