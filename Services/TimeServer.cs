using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using log4net;

namespace miniEAP.Services
{
    public class TimeServer
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(TimeServer));
        private TcpListener _listener;
        private CancellationTokenSource _cts;
        private readonly int _port;

        public TimeServer(int port)
        {
            _port = port;
        }

        public void Start()
        {
            _cts = new CancellationTokenSource();
            _listener = new TcpListener(IPAddress.Any, _port);
            
            try
            {
                _listener.Start();
                LogicalThreadContext.Properties["LogType"] = "System";
                log.Info($"TimeServer started on port {_port}");
                
                Task.Run(() => ListenAsync(_cts.Token));
            }
            catch (Exception ex)
            {
                LogicalThreadContext.Properties["LogType"] = "Error";
                log.Error($"Failed to start TimeServer: {ex.Message}");
            }
        }

        public void Stop()
        {
            _cts?.Cancel();
            _listener?.Stop();
            LogicalThreadContext.Properties["LogType"] = "System";
            log.Info("TimeServer stopped");
        }

        private async Task ListenAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    TcpClient client = await _listener.AcceptTcpClientAsync();
                    _ = HandleClientAsync(client);
                }
                catch (ObjectDisposedException)
                {
                    // Listener stopped
                    break;
                }
                catch (Exception ex)
                {
                    LogicalThreadContext.Properties["LogType"] = "Error";
                    log.Error($"Error accepting client: {ex.Message}");
                }
            }
        }

        private async Task HandleClientAsync(TcpClient client)
        {
            using (client)
            {
                try
                {
                    string timeString = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                    byte[] data = Encoding.UTF8.GetBytes(timeString);
                    
                    using (NetworkStream stream = client.GetStream())
                    {
                        await stream.WriteAsync(data, 0, data.Length);
                        await stream.FlushAsync();
                    }
                    
                    LogicalThreadContext.Properties["LogType"] = "System";
                    log.Info($"Sent time [{timeString}] to client {client.Client.RemoteEndPoint}");
                }
                catch (Exception ex)
                {
                    LogicalThreadContext.Properties["LogType"] = "Error";
                    log.Error($"Error handling client {client.Client.RemoteEndPoint}: {ex.Message}");
                }
            }
        }
    }
}
