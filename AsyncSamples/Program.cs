using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AsyncSamples
{
	class Program
	{
		public static void Main(string[] args)
		{
			try
			{
				var evnt = ApmTest();
				Console.WriteLine("Main thread still working");
				evnt.WaitOne();
				Console.WriteLine("Main thread finishing");

				Task task;
//				task = FileContinueWith();
//				task = FileAsync();
//				task = FileCompareAsync();
//				task = LambdaAsync();

//				task = DownloadWebPageAsync();

//				Console.WriteLine($"Main thread still working [{Thread.CurrentThread.ManagedThreadId}]");
//				task.Wait();
//				Console.WriteLine("Main thread finishing");
			}
			catch(Exception ex)
			{
				Console.Error.WriteLine(ex);
			}
		}

		private const int WRITE_BLOCK_SIZE = 16 * 1024;
		private const int WRITE_DATA_SIZE = 128 * 1024 * 1024;

		public static AutoResetEvent ApmTest()
		{
			var buf = new byte[WRITE_DATA_SIZE];
			new Random().NextBytes(buf);
			Console.WriteLine("Generated random data");

			var evnt = new AutoResetEvent(false);

			var fStream = new FileStream("file1.txt", FileMode.Create, FileAccess.ReadWrite, FileShare.None, WRITE_BLOCK_SIZE, FileOptions.WriteThrough);
			var sw = Stopwatch.StartNew();

			fStream.BeginWrite(buf, 0, buf.Length,
				asyncResult =>
				{
					var fs = (FileStream)asyncResult.AsyncState;
					fs.EndWrite(asyncResult);
					evnt.Set();
					fStream.Dispose();
					Console.WriteLine("Finished disk I/O! in {0} ms (thread {1})", sw.ElapsedMilliseconds, Thread.CurrentThread.ManagedThreadId);
				}, fStream);
			Console.WriteLine("Started disk I/O! in {0} ms total (thread {1})", sw.ElapsedMilliseconds, Thread.CurrentThread.ManagedThreadId);

			return evnt;
		}

		public static Task FileContinueWith()
		{
			var buf = new byte[WRITE_DATA_SIZE];
			new Random().NextBytes(buf);
			Console.WriteLine("Generated random data");

			var fStream = new FileStream("file2.txt", FileMode.Create, FileAccess.ReadWrite, FileShare.None, WRITE_BLOCK_SIZE, FileOptions.WriteThrough);

			var sw = Stopwatch.StartNew();

			var copyToAsyncTask = fStream.WriteAsync(buf, 0, buf.Length);
			Console.WriteLine("Started disk I/O! in {0} ms (thread {1})", sw.ElapsedMilliseconds, Thread.CurrentThread.ManagedThreadId);

			var resultTask = copyToAsyncTask.ContinueWith(writeTask =>
			{
				fStream.Dispose();
				if (writeTask.IsFaulted)
					throw new Exception("", writeTask.Exception);
				Console.WriteLine("Finished disk I/O! in {0} ms total (thread {1})", sw.ElapsedMilliseconds, Thread.CurrentThread.ManagedThreadId);
			});

			return resultTask;
		}

		public static async Task FileAsync()
		{
			var buf = new byte[WRITE_DATA_SIZE];
			for(int i = 0; i < buf.Length; i++)
				buf[i] = (byte)(i % 256);

			using(var fStream = new FileStream("file3.txt", FileMode.Create, FileAccess.ReadWrite, FileShare.None, WRITE_BLOCK_SIZE, FileOptions.WriteThrough))
			{
				var sw = Stopwatch.StartNew();

				var writeAsyncTask = fStream.WriteAsync(buf, 0, buf.Length);
				Console.WriteLine("Started disk I/O! in {0} ms (thread {1})", sw.ElapsedMilliseconds, Thread.CurrentThread.ManagedThreadId);

				await writeAsyncTask;

				Console.WriteLine("Finished disk I/O! in {0} ms total (thread {1})", sw.ElapsedMilliseconds, Thread.CurrentThread.ManagedThreadId);
			}
		}

		public static async Task FileCompareAsync()
		{
			var sw = Stopwatch.StartNew();

			var taskZeros1 = /*await */HashFileAsync("file1.txt");
			Console.WriteLine("Started async read of first file [thread {0}]", Thread.CurrentThread.ManagedThreadId);

			var taskZeros2 = /*await */HashFileAsync("file2.txt");
			Console.WriteLine("Started async read of second file [thread {0}]", Thread.CurrentThread.ManagedThreadId);

			Console.WriteLine(" Started async reads in {0} ms [thread {1}]", sw.ElapsedMilliseconds, Thread.CurrentThread.ManagedThreadId);


//			var hash1 = await taskZeros1;
//			Console.WriteLine(Thread.CurrentThread.ManagedThreadId);
//			var hash2 = await taskZeros2;
//			Console.WriteLine(Thread.CurrentThread.ManagedThreadId);

			//или так
			var hashes = await Task.WhenAll(taskZeros1, taskZeros2);
			var hash1 = hashes[0];
			var hash2 = hashes[1];

			Console.WriteLine("Hash1: {0} [thread {1}]", hash1, Thread.CurrentThread.ManagedThreadId);
			Console.WriteLine("Hash2: {0} [thread {1}]", hash2, Thread.CurrentThread.ManagedThreadId);
		}

		private static async Task<string> HashFileAsync(string filename)
		{
			using(var ms = new MemoryStream())
			{
				var sw = Stopwatch.StartNew();
				using(var ifs = new FileStream(filename, FileMode.Open))
				{
					Console.WriteLine("Starting async read of file {0} [thread {1}]", filename, Thread.CurrentThread.ManagedThreadId);
					await ifs.CopyToAsync(ms);
				}
				Console.WriteLine("Read file {0} in {1} ms  [thread {2}]", filename, sw.ElapsedMilliseconds, Thread.CurrentThread.ManagedThreadId);
				sw.Restart();
				byte[] hash = MD5.Create().ComputeHash(ms.GetBuffer(), 0, (int)ms.Position);
				Console.WriteLine("Computed hash of file {0} in {1} ms [thread {2}]", filename, sw.ElapsedMilliseconds, Thread.CurrentThread.ManagedThreadId);

				return Convert.ToBase64String(hash);
			}
		}

		public static async Task LambdaAsync()
		{
			var sw = new Stopwatch();
			sw.Start();

			double result = 0;

			result = await CosCalcAsync();
			Console.WriteLine("Finished with {0} in {1}ms", result, sw.Elapsed.TotalMilliseconds);

			sw.Restart();
			await Task.WhenAll(Enumerable.Range(1, 100).Select(_ => CosCalcAsync()));
			Console.WriteLine("Finished with {0} in {1}ms", result, sw.Elapsed.TotalMilliseconds);
		}

		private static Task<double> CosCalcAsync()
		{
			return Task.Run(async () =>
			{
				double cur = 1;
				for(int i = 0; i < 1 * 1000 * 1000; i++)
				{
					cur = Math.Cos(cur);
				}
				await Task.Delay(1000);
				return cur;
			});
		}

		public static async Task DownloadWebPageAsync()
		{
			var sw = Stopwatch.StartNew();
			var request = CreateRequest("http://e1.ru");
			var response = await request.GetResponseAsync();
			using(var stream = response.GetResponseStream())
			{
				var ms = new MemoryStream();
				await stream.CopyToAsync(ms);
				Console.WriteLine("Got {0} bytes in {1} ms", ms.Position, sw.ElapsedMilliseconds);
				Console.WriteLine(Encoding.GetEncoding(1251).GetString(ms.ToArray()));
			}
		}

		private static HttpWebRequest CreateRequest(string uriStr, int timeout = 30 * 1000)
		{
			var request = WebRequest.CreateHttp(uriStr);
			request.Timeout = timeout;
			request.KeepAlive = true;
			request.ServicePoint.UseNagleAlgorithm = false;
			request.ServicePoint.ConnectionLimit = 100500;
			return request;
		}
	}
}
