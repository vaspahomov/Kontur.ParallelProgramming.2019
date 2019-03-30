using System;
using System.Net;
using System.Threading.Tasks;
using log4net;

namespace ClusterServer
{
	public static class HttpListenerExtensions
	{
		private static readonly ILog Log = LogManager.GetLogger(typeof(HttpListenerExtensions));

		public async static Task StartProcessingRequestsAsync(this HttpListener listener, Func<HttpListenerContext, Task> callbackAsync)
		{
			listener.Start();

			while (true)
			{
				try
				{
					var context = await listener.GetContextAsync();

					Task.Run(
						async () =>
								{
									var ctx = context;
									try
									{
										await callbackAsync(ctx);
									}
									catch (Exception e)
									{
										Log.Error(e);
									}
									finally
									{
										ctx.Response.Close();
									}
								}
						);
				}
				catch (Exception e)
				{
					Log.Error(e);
				}
			}
		}

		public static void StartProcessingRequestsSync(this HttpListener listener, Action<HttpListenerContext> callbackSync)
		{
			listener.Start();

			while (true)
			{
				try
				{
					var context = listener.GetContext();

					try
					{
						callbackSync(context);
					}
					catch (Exception e)
					{
						Log.Error(e);
					}
					finally
					{
						context.Response.Close();
					}
				}
				catch (Exception e)
				{
					Log.Error(e);
				}
			}
		}
	}
}