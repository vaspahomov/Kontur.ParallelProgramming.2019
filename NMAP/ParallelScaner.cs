using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading.Tasks;
using log4net;

namespace NMAP
{
    public class ParallelScaner : IPScanner
    {
        protected virtual ILog log => LogManager.GetLogger(typeof(ParallelScaner));

        public async Task Scan(IPAddress[] ipAdrrs, int[] ports)
        {
            await Task.WhenAll(ipAdrrs.Select(ipAddr => (ipAddr, PingAddr(ipAddr)))
                .Select(async x=>
                {
                    var (ipAddress, pingResult) = x;
                    if (await pingResult != IPStatus.Success) return;
                    
                    var nestedTasks = ports.Select(port => CheckPort(ipAddress, port));
                    await Task.WhenAll(nestedTasks);
                }));
        }

        protected async Task<IPStatus> PingAddr(IPAddress ipAddr, int timeout = 3000)
        {
            log.Info($"Pinging {ipAddr}");
            using (var ping = new Ping())
            {
                var sendTask = await ping.SendPingAsync(ipAddr, timeout);
                log.Info($"Pinged {ipAddr}: {sendTask.Status}"); 
                return sendTask.Status;
            }
        }

        protected async Task CheckPort(IPAddress ipAddr, int port, int timeout = 3000)
        {
            using (var tcpClient = new TcpClient())
            {
                log.Info($"Checking {ipAddr}:{port}");

                var connectTask = await tcpClient.ConnectAsync(ipAddr, port, timeout);

                PortStatus portStatus;
                switch (connectTask.Status)
                {
                    case TaskStatus.RanToCompletion:
                        portStatus = PortStatus.OPEN;
                        break;
                    case TaskStatus.Faulted:
                        portStatus = PortStatus.CLOSED;
                        break;
                    default:
                        portStatus = PortStatus.FILTERED;
                        break;
                }

                log.Info($"Checked {ipAddr}:{port} - {portStatus}");
            }
        }
    }
}