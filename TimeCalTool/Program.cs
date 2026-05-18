using System;
using System.Configuration;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using log4net;
using log4net.Config;

namespace TimeCalTool
{
    class Program
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(Program));

        [StructLayout(LayoutKind.Sequential)]
        public struct SystemTime
        {
            public ushort Year;
            public ushort Month;
            public ushort DayOfWeek;
            public ushort Day;
            public ushort Hour;
            public ushort Minute;
            public ushort Second;
            public ushort Milliseconds;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool SetLocalTime(ref SystemTime st);

        static void Main(string[] args)
        {
            XmlConfigurator.Configure();
            LogicalThreadContext.Properties["LogType"] = "System";
            log.Info("TimeCalTool started.");

            string ip = ConfigurationManager.AppSettings["RemoteIP"] ?? "127.0.0.1";
            string portStr = ConfigurationManager.AppSettings["RemotePort"] ?? "9000";
            
            if (!int.TryParse(portStr, out int port))
            {
                WriteLog("Error", "Invalid port configuration.");
                return;
            }

            try
            {
                using (TcpClient client = new TcpClient())
                {
                    log.Info($"Connecting to {ip}:{port}...");
                    client.Connect(ip, port);
                    
                    using (NetworkStream stream = client.GetStream())
                    {
                        byte[] buffer = new byte[1024];
                        int bytesRead = stream.Read(buffer, 0, buffer.Length);
                        string timeStr = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                        
                        log.Info($"Received time string: {timeStr}");
                        
                        if (DateTime.TryParse(timeStr, out DateTime targetTime))
                        {
                            if (SyncTime(targetTime))
                            {
                                WriteLog("System", $"Time successfully synchronized to {targetTime:yyyy-MM-dd HH:mm:ss.fff}");
                            }
                            else
                            {
                                WriteLog("Error", "Failed to set system time. Please run as Administrator.");
                            }
                        }
                        else
                        {
                            WriteLog("Error", $"Failed to parse time string: {timeStr}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                WriteLog("Error", $"Sync failed: {ex.Message}");
            }
            finally
            {
                log.Info("TimeCalTool exiting.");
            }
        }

        private static bool SyncTime(DateTime dt)
        {
            SystemTime st = new SystemTime
            {
                Year = (ushort)dt.Year,
                Month = (ushort)dt.Month,
                Day = (ushort)dt.Day,
                Hour = (ushort)dt.Hour,
                Minute = (ushort)dt.Minute,
                Second = (ushort)dt.Second,
                Milliseconds = (ushort)dt.Millisecond
            };

            return SetLocalTime(ref st);
        }

        private static void WriteLog(string type, string message)
        {
            LogicalThreadContext.Properties["LogType"] = type;
            if (type == "Error")
                log.Error(message);
            else
                log.Info(message);
            
            Console.WriteLine($"[{type}] {message}");
        }
    }
}
