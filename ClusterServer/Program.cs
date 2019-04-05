using System;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using log4net;
using log4net.Config;

namespace Cluster
{
	public static class Program
	{
		public static void Main(string[] args)
		{
			XmlConfigurator.Configure();

			try
			{
			    if(!ServerOptions.TryGetArguments(args, out var parsedArguments))
					return;

                var server = new ClusterServer(parsedArguments, Log);
                server.Start();

			    Console.ReadLine();
			    Log.InfoFormat("Server stopped!");

            }
			catch(Exception e)
			{
				Log.Fatal(e);
			}
		}





		private static readonly ILog Log = LogManager.GetLogger(typeof(Program));
	}
}
