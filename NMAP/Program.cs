using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using log4net.Config;

namespace NMAP
{
    class Program
	{
		static void Main(string[] args)
		{
			XmlConfigurator.Configure();

			var ipAddrs = GenIpAddrs();
			var ports = new[] {21, 25, 80, 443, 3389 };

			var scanner = new ParallelScaner();
			scanner.Scan(ipAddrs, ports).Wait();
		}

		private static IPAddress[] GenIpAddrs()
		{
			var konturAddrs = new List<IPAddress>();
			uint focusIpInt = 0x0ACB112E;
			for (int b = 0; b <= byte.MaxValue; b++)
				konturAddrs.Add(new IPAddress((focusIpInt & 0x00FFFFFF) | (uint)b << 24));
			return konturAddrs.ToArray();
		}
	}
}
