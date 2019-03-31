using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;

namespace PLINQ
{
	class Program
	{
		private static void ProcessLine(string line, ref ConcurrentBag<string> stackTraces)
		{
			var datePrefixRE = new Regex(@"^\d{4}-\d{2}-\d{2}");
			if(!datePrefixRE.IsMatch(line))
			{
				Thread.SpinWait(100000);
				stackTraces.Add(line);
			}
			else
				Thread.SpinWait(100);
		}

		private static void ParseLogsParallel(Partitioner<string> logsPartitioner, out ConcurrentBag<string> stackTraces, out long workingTime)
		{
			var parallelTimer = Stopwatch.StartNew();
			var parallelStackTraces = new ConcurrentBag<string>();
			logsPartitioner.AsParallel()
				.ForAll(line => ProcessLine(line, ref parallelStackTraces));

			stackTraces = parallelStackTraces;
			workingTime = parallelTimer.ElapsedMilliseconds;
		}

		private static void ParseLogsSequential(Partitioner<string> logsPartitioner, out ConcurrentBag<string> stackTraces, out long workingTime)
		{
			var sequentialTimer = Stopwatch.StartNew();
			var sequentialStackTraces = new ConcurrentBag<string>();
			var enumerator = logsPartitioner.GetPartitions(1).Single();
			while(enumerator.MoveNext())
				ProcessLine(enumerator.Current, ref sequentialStackTraces);

			stackTraces = sequentialStackTraces;
			workingTime = sequentialTimer.ElapsedMilliseconds;
		}

		public static void Main()
		{
			const string filePath = "Files/bigLog.log";

			ConcurrentBag<string> parallelStackTraces, sequentialStackTraces;
			long parallelWorkingTime, sequentialWorkingTime;

			var logsPartitioner = new LogsPartitioner(filePath);
			ParseLogsParallel(logsPartitioner, out parallelStackTraces, out parallelWorkingTime);
			logsPartitioner = new LogsPartitioner(filePath);
			ParseLogsSequential(logsPartitioner, out sequentialStackTraces, out sequentialWorkingTime);

			Console.WriteLine("Parallel: {0} ms", parallelWorkingTime);
			Console.WriteLine("Sequential: {0} ms", sequentialWorkingTime);
			Console.WriteLine("Result stack traces are {0}equal and {1}correct",
				parallelStackTraces.OrderBy(trace => trace).SequenceEqual(sequentialStackTraces.OrderBy(trace => trace)) ? "" : "not ",
				//сообщение о методе, вызвавшем ошибку, не приклеивается
				parallelStackTraces.Count == 4845
					&& parallelStackTraces.Sum(trace => trace.Length) == 9539805 ? "" : "not ");
		}
	}
}