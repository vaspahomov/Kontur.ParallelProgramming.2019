using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PLINQ
{
	public class LogsPartitioner : Partitioner<string>
	{
		IEnumerator<string> fileData;

		public LogsPartitioner(string filePath)
		{
			if(!File.Exists(filePath))
			{
				Console.WriteLine($"File {filePath} not found");
				Environment.Exit(1);
			}
			fileData = File.ReadLines(filePath).GetEnumerator();
			fileData.MoveNext();
		}

		public override IList<IEnumerator<string>> GetPartitions(int partitionCount)
		{
			return Enumerable.Range(0, partitionCount).Select(_ => GetEnumerator()).ToList();
		}

		private IEnumerator<string> GetEnumerator()
		{
			List<string> myPart = new List<string>();
			while(true)
			{
				lock(fileData)
				{
					string currentLine = fileData.Current;
					if(string.IsNullOrEmpty(currentLine))
						yield break;

					myPart.Add(currentLine);

					bool haveNext = fileData.MoveNext();

					bool isStartOfStackTrace = !currentLine.StartsWith("2015");
					currentLine = fileData.Current;
					if(isStartOfStackTrace)
						while(haveNext && !currentLine.StartsWith("2015"))
						{
							myPart.Add(currentLine);
							haveNext = fileData.MoveNext();
							currentLine = fileData.Current;
						}
				}
				yield return myPart.Count != 1 ? string.Join("\n", myPart) : myPart[0];
				myPart.Clear();
			}
		}
	}
}
