using System;
using System.Drawing; // Added for Color
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Linq;
using System.Linq;
using System.Configuration;
using Newtonsoft.Json.Linq;
using CyntecMESEquipment.serviceEqp;
using miniEAP.Services;
using log4net;
using log4net.Config;

namespace miniEAP
{
    public partial class Form1 : Form
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(Form1));
        private string _ver = "1.0.6"; //版本號
        private HttpListener _listener;
        private bool _isRunning = false;
        private bool _isTestMode = false;
        private readonly TransactionProcessor _processor = new TransactionProcessor();
        private readonly object _logLock = new object();
        private readonly string _settingFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "multiplier.config");
        private string _externalLogPath = "";
        private string _ftpUser = "";
        private string _ftpPassword = "";
        private bool _heartbeatToggle = false;

        public Form1()
        {
            InitializeComponent();
            _processor.OnLog = (type, msg) => WriteLog(type, msg);
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            XmlConfigurator.Configure();
            // Read TestMode from Config
            string testModeSetting = ConfigurationManager.AppSettings["TestMode"];
            if (!string.IsNullOrEmpty(testModeSetting))
            {
                bool.TryParse(testModeSetting, out _isTestMode);
            }
            if (_isTestMode)
            {
                this.Text += " (Test Mode)";
                WriteLog("System", "Running in TEST MODE");
            }
            this.Text += " v." + _ver;
            // Load Multiplier Setting
            if (File.Exists(_settingFile))
            {
                string savedValue = File.ReadAllText(_settingFile).Trim();
                if (cmbMultiplier.Items.Contains(savedValue))
                {
                    cmbMultiplier.SelectedItem = savedValue;
                }
                else
                {
                    cmbMultiplier.SelectedIndex = 0; 
                }
            }
            else
            {
                cmbMultiplier.SelectedIndex = 0; // Default to 1
            }

            // Load External Log Path from Config
            _externalLogPath = ConfigurationManager.AppSettings["ExternalLogPath"];
            if (string.IsNullOrEmpty(_externalLogPath))
            {
                _externalLogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ExternalLogs");
            }
            _ftpUser = ConfigurationManager.AppSettings["ExternalLogFtpUser"] ?? "";
            _ftpPassword = ConfigurationManager.AppSettings["ExternalLogFtpPassword"] ?? "";
            
            WriteLog("System", $"External log path set to: {_externalLogPath}");

            StartServer();
            timerHeartbeat.Start();
            timerLogSync.Start();
            
            // Initial maintenance check
            Task.Run(() => MaintenanceTask());
        }

        private void timerLogSync_Tick(object sender, EventArgs e)
        {
            Task.Run(() => MaintenanceTask());
        }

        private void LogReportProcessing(string json)
        {
            try
            {
                JObject obj = JObject.Parse(json);
                string txName = obj["TransactionName"]?.ToString();

                if (txName == "EQPSTATUS")
                {
                    StringBuilder sb = new StringBuilder();
                    sb.Append($"[{txName}],");
                    sb.Append($"EqpNo:{obj["EqpNo"]},");
                    sb.Append($"StatusCode:{obj["StatusCode"]},");
                    
                    string paramListStr = obj["ParameterList"]?.ToString();
                    if (!string.IsNullOrEmpty(paramListStr))
                    {
                        sb.Append("[ParameterList]");
                        try
                        {
                            JArray paramsArr = JArray.Parse(paramListStr);
                            sb.Append("[");
                            for (int i = 0; i < paramsArr.Count; i++)
                            {
                                var p = paramsArr[i];
                                sb.Append($"{{P_Name:{p["P_Name"]},P_Value:{p["P_Value"]},P_LSL:{p["P_LSL"]},P_USL:{p["P_USL"]}}}");
                                if (i < paramsArr.Count - 1) sb.Append(",");
                            }
                            sb.Append("]");
                        }
                        catch
                        {
                            sb.Append(paramListStr);
                        }
                    }
                    WriteLog("Report", sb.ToString());
                }
                else if (obj["UserID"] != null && obj["UserName"] != null)
                {
                    // Likely UserVerify or similar success response
                    string displayTx = txName ?? "UserVerify";
                    WriteLog("Report", $"[{displayTx}],UserID:{obj["UserID"]},UserName:{obj["UserName"]}");
                }
            }
            catch
            {
                // Silently ignore parse errors for report logging
            }
        }

        private void MaintenanceTask()
        {
            try
            {
                DateTime now = DateTime.Now;
                string uploadPath = _externalLogPath;

                // FTP 不支援目錄清理邏輯，僅針對本地路徑執行
                if (!string.IsNullOrEmpty(uploadPath) && !uploadPath.StartsWith("ftp://", StringComparison.OrdinalIgnoreCase) && Directory.Exists(uploadPath))
                {
                    var logFiles = Directory.GetFiles(uploadPath, "*.txt");
                    foreach (var file in logFiles)
                    {
                        string fileName = Path.GetFileNameWithoutExtension(file);
                        // 檢查檔名是否為 yyyyMMdd 格式
                        if (DateTime.TryParseExact(fileName, "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out DateTime fileDate))
                        {
                            if ((now - fileDate).TotalDays > 3)
                            {
                                try { File.Delete(file); } catch { }
                            }
                        }
                    }

                    // 同時清理舊的日期目錄 (相容舊版本產生的目錄)
                    var dateDirs = Directory.GetDirectories(uploadPath);
                    foreach (var dir in dateDirs)
                    {
                        string dirName = Path.GetFileName(dir);
                        if (DateTime.TryParseExact(dirName, "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out DateTime dirDate))
                        {
                            if ((now - dirDate).TotalDays > 3)
                            {
                                try { Directory.Delete(dir, true); } catch { }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // 注意：此處若寫 log 可能會因為 log4net 設定而混入單一檔案
                System.Diagnostics.Debug.WriteLine($"MaintenanceTask Failed: {ex.Message}");
            }
        }

        private void timerHeartbeat_Tick(object sender, EventArgs e)
        {
            // Check if server thread is supposedly running and listener is active
            if (_isRunning && _listener != null && _listener.IsListening)
            {
                _heartbeatToggle = !_heartbeatToggle;
                // Green blinking for healthy state
                pnlHeartbeat.BackColor = _heartbeatToggle ? Color.LimeGreen : Color.Green;
            }
            else
            {
                // Red for stopped/error state
                pnlHeartbeat.BackColor = Color.Red;
            }
        }

        private void cmbMultiplier_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (int.TryParse(cmbMultiplier.SelectedItem.ToString(), out int val))
            {
                _processor.Multiplier = val;
                WriteLog("System", $"Multiplier changed to {_processor.Multiplier}");
                
                // Save Setting
                try 
                {
                    File.WriteAllText(_settingFile, cmbMultiplier.SelectedItem.ToString());
                }
                catch (Exception ex)
                {
                    WriteLog("Error", $"Failed to save setting: {ex.Message}");
                }
            }
        }

        private void StartServer()
        {
            try
            {
                _listener = new HttpListener();
                // Listen on localhost only to avoid firewall/admin permission issues
                _listener.Prefixes.Add("http://localhost:5566/");
                _listener.Start();
                _isRunning = true;
                WriteLog("System", "Server started on port 5566");

                Task.Run(() => ListenLoop());
            }
            catch (Exception ex)
            {
                WriteLog("Error", $"Failed to start server: {ex.Message}");
                MessageBox.Show($"Failed to start server: {ex.Message}");
            }
        }

        private async void ListenLoop()
        {
            while (_isRunning && _listener.IsListening)
            {
                try
                {
                    var context = await _listener.GetContextAsync();
                    ProcessRequest(context);
                }
                catch (Exception ex)
                {
                    WriteLog("Error", $"Listener error: {ex.Message}");
                }
            }
        }

        private void ProcessRequest(HttpListenerContext context)
        {
            Task.Run(() =>
            {
                string requestBody = "";
                try
                {
                    using (var reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding))
                    {
                        requestBody = reader.ReadToEnd();
                    }

                    // Parse SOAP to find method and parameter
                    string methodName = "";
                    string parameter = "";
                    
                    try 
                    {
                        XDocument doc = XDocument.Parse(requestBody);
                        XNamespace soapEnv = "http://schemas.xmlsoap.org/soap/envelope/";
                        
                        var body = doc.Root.Element(soapEnv + "Body");
                        if (body != null)
                        {
                            var methodElement = body.Elements().FirstOrDefault(); // Get first child of Body (the method)
                            if (methodElement != null)
                            {
                                methodName = methodElement.Name.LocalName;
                                var paramElement = methodElement.Elements().FirstOrDefault(); // Get first param
                                if (paramElement != null)
                                {
                                    parameter = paramElement.Value;
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        WriteLog("Error", $"XML Parse Failed. Body: {requestBody}. Ex: {ex.Message}");
                        throw; // Re-throw to send fault
                    }

                    string result = "";
                    
                    if (methodName == "EqpTransaction")
                    {
                        // 1. Receive from EQ
                        WriteLog("Json", $"[Receive from EQ]: {parameter}");
                        UpdateUI(txtReceive, $"[Receive from EQ]: {parameter}");

                        // 2. Logic: Multiply RefInputQty
                        string modifiedParam = _processor.ProcessTransactionPayload(parameter, true);

                        // 3. Send to MES
                        WriteLog("Json", $"[Send to MES]: {modifiedParam}");
                        UpdateUI(txtSend, $"[Send to MES]: {modifiedParam}");
                        LogReportProcessing(modifiedParam);
                        
                        if (_isTestMode)
                        {
                            WriteLog("Json", "[Test Mode]: Skipping MES transaction. Returning simulated success.");
                            
                            try {
                                JObject inputObj = JObject.Parse(modifiedParam);
                                string transactionName = inputObj["TransactionName"]?.ToString();
                                JObject fakeResponse = new JObject();
                                
                                switch (transactionName)
                                {
                                    case "CONNECTION":
                                        fakeResponse["Result"] = "success";
                                        fakeResponse["ResultCode"] = "0000";
                                        fakeResponse["Message"] = "Test Mode Connection Success";
                                        fakeResponse["ResultDt"] = "[]"; // Empty DataTable
                                        break;

                                    case "UserVerify":
                                        fakeResponse["Result"] = "success";
                                        fakeResponse["ResultCode"] = "0000";
                                        fakeResponse["Message"] = "Test Mode User Verify Success";
                                        fakeResponse["UserID"] = inputObj["UserID"] ?? "TEST_USER";
                                        fakeResponse["UserName"] = "Test User Name";
                                        break;

                                    case "WOQRY":
                                        fakeResponse["Result"] = "success";
                                        fakeResponse["ResultCode"] = "0000";
                                        fakeResponse["Message"] = "Test Mode WO Query Success";
                                        fakeResponse["MatNo"] = "TEST-MAT-001";
                                        fakeResponse["WorkCenterNo"] = "WC001";
                                        fakeResponse["WorkCenterName"] = "Test Work Center";
                                        // Default WIPQty remains unchanged
                                        fakeResponse["WIPQty"] = "20.0"; 
                                        fakeResponse["RefInputQty"] = "20.0";
                                        fakeResponse["AlreadyInFlag"] = "N";
                                        fakeResponse["BatchNo"] = "BATCH001";
                                        
                                        // Mock DataTables
                                        fakeResponse["NGCodeListDT"] = "[]";
                                        
                                        // Mock InputCodeListDT (Manage Items)
                                        JArray inputCodeArr = new JArray();
                                        JObject inputItem = new JObject();
                                        inputItem["InputCode"] = "IC001";
                                        inputItem["InputCodeName"] = "Test Input Item";
                                        inputItem["RequireInput"] = "Y";
                                        inputItem["DefaultValue"] = "Default";
                                        inputItem["DataListItem"] = "";
                                        inputCodeArr.Add(inputItem);
                                        fakeResponse["InputCodeListDT"] = inputCodeArr.ToString(Newtonsoft.Json.Formatting.None);

                                        // Mock InputParamListDT (Manage Params)
                                        JArray paramArr = new JArray();
                                        JObject paramItem = new JObject();
                                        paramItem["ParamCode"] = "P001";
                                        paramItem["ParamName"] = "Test Param";
                                        paramItem["DefaultValue"] = "100";
                                        paramItem["RequireInput"] = "Y";
                                        paramItem["RefFieldCode"] = "REF001";
                                        paramItem["Showable"] = "Y";
                                        paramItem["UpperValue"] = "200";
                                        paramItem["LowerValue"] = "0";
                                        paramItem["DataListItem"] = "";
                                        paramArr.Add(paramItem);
                                        fakeResponse["InputParamListDT"] = paramArr.ToString(Newtonsoft.Json.Formatting.None);
                                        break;

                                    case "WOCHECKIN":
                                    case "WOCHECKOUT":
                                    case "UPLOADTESTFILE":
                                        fakeResponse["Result"] = "success";
                                        fakeResponse["ResultCode"] = "0000";
                                        fakeResponse["Message"] = $"Test Mode {transactionName} Success";
                                        break;

                                    default:
                                        fakeResponse["Result"] = "success";
                                        fakeResponse["Message"] = "Test Mode Default Success";
                                        break;
                                }

                                // If input had RefInputQty (e.g. from EQ to MES), mirror it in response if needed
                                
                                result = fakeResponse.ToString(Newtonsoft.Json.Formatting.None);
                            } catch (Exception ex) {
                                WriteLog("Error", $"Test Mode Simulation Failed: {ex.Message}");
                                result = "{\"Result\":\"fail\",\"Message\":\"Test Mode Simulation Failed\"}";
                            }
                        }
                        else
                        {
                            Eqp_Portal service = new Eqp_Portal();
                            result = service.EqpTransaction(modifiedParam);
                        }

                        // 5. Logic: Divide RefInputQty
                        string modifiedResult = _processor.ProcessTransactionPayload(result, false);

                        // 6. Send to EQ
                        WriteLog("Json", $"[Send to EQ]: {modifiedResult}");
                        UpdateUI(txtSend, $"[Send to EQ]: {modifiedResult}");

                        SendSoapResponse(context, "EqpTransactionResponse", "EqpTransactionResult", modifiedResult);

                        // 7. 在回應設備後，於背景非同步寫入外部日誌 (不影響交易速度)
                        Task.Run(() => WriteExternalLog(modifiedParam, result));
                    }
                    else if (methodName == "InsertEqpErrorLog")
                    {
                            Eqp_Portal service = new Eqp_Portal();
                            result = service.InsertEqpErrorLog(parameter);
                            SendSoapResponse(context, "InsertEqpErrorLogResponse", "InsertEqpErrorLogResult", result);
                    }
                    else if (methodName == "InsertEqpTransactionLog")
                    {
                            Eqp_Portal service = new Eqp_Portal();
                            result = service.InsertEqpTransactionLog(parameter);
                            SendSoapResponse(context, "InsertEqpTransactionLogResponse", "InsertEqpTransactionLogResult", result);
                    }
                    else
                    {
                            // Unknown method
                            WriteLog("Error", $"Unknown method: {methodName}");
                            SendSoapFault(context, "Client", $"Unknown method: {methodName}");
                    }
                }
                catch (Exception ex)
                {
                    WriteLog("Error", $"Processing error: {ex.Message}");
                    SendSoapFault(context, "Server", ex.Message);
                }
            });
        }

        private void WriteExternalLog(string sendToMesPayload, string receiveFromMesResult)
        {
            try
            {
                // 1. 檢查回覆是否成功 (不分大小寫)
                if (string.IsNullOrEmpty(receiveFromMesResult)) return;

                bool isSuccess = false;
                try
                {
                    JObject resObj = JObject.Parse(receiveFromMesResult);
                    string resStr = resObj["Result"]?.ToString()?.ToLower();
                    if (resStr == "success") isSuccess = true;
                }
                catch
                {
                    // 若 JSON 解析失敗，退而求其次檢查字串
                    if (receiveFromMesResult.ToLower().Contains("success")) isSuccess = true;
                }

                if (!isSuccess) return;

                // 2. 獲取路徑 (已在 Load 時讀取自 App.config)
                string path = _externalLogPath;

                if (string.IsNullOrEmpty(path)) return;

                // 3. 生成檔名 (yyyyMMdd.txt)
                string fileName = DateTime.Now.ToString("yyyyMMdd") + ".txt";

                // 4. 解析 TransactionName 作為 Log Type
                string txName = "ExternalLog";
                try
                {
                    JObject obj = JObject.Parse(sendToMesPayload);
                    txName = obj["TransactionName"]?.ToString() ?? "ExternalLog";
                }
                catch { }

                // 5. 格式化日誌內容
                string logLine = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{txName}] {sendToMesPayload}";

                // 6. 判斷路徑類型並寫入
                if (path.StartsWith("ftp://", StringComparison.OrdinalIgnoreCase))
                {
                    // 確保 URL 以斜槓結尾
                    string baseUrl = path.EndsWith("/") ? path : path + "/";
                    WriteToFtp(baseUrl + fileName, logLine);
                    WriteLog("System", $"External log appended to FTP: {fileName}");
                }
                else
                {
                    // 3. 確保目錄存在
                    if (!Directory.Exists(path))
                    {
                        Directory.CreateDirectory(path);
                    }
                    string filePath = Path.Combine(path, fileName);
                    File.AppendAllText(filePath, logLine + Environment.NewLine);
                    WriteLog("System", $"External log appended to local: {fileName}");
                }
            }
            catch (Exception ex)
            {
                WriteLog("Error", $"Failed to write external log: {ex.Message}");
            }
        }

        private void WriteToFtp(string url, string content)
        {
            try
            {
                FtpWebRequest request = (FtpWebRequest)WebRequest.Create(url);
                request.Method = WebRequestMethods.Ftp.AppendFile;
                request.Timeout = 3000;         // 連線逾時 3 秒
                request.ReadWriteTimeout = 3000; // 讀寫逾時 3 秒
                
                if (!string.IsNullOrEmpty(_ftpUser))
                {
                    request.Credentials = new NetworkCredential(_ftpUser, _ftpPassword);
                }

                byte[] fileContents = Encoding.UTF8.GetBytes(content + Environment.NewLine);
                request.ContentLength = fileContents.Length;

                using (Stream requestStream = request.GetRequestStream())
                {
                    requestStream.Write(fileContents, 0, fileContents.Length);
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"FTP Upload Error: {ex.Message}");
            }
        }

        private void SendSoapFault(HttpListenerContext context, string code, string message)
        {
            try 
            {
                string soapFault = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<soap:Envelope xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/"">
  <soap:Body>
    <soap:Fault>
      <faultcode>soap:{code}</faultcode>
      <faultstring>{message}</faultstring>
    </soap:Fault>
  </soap:Body>
</soap:Envelope>";
                byte[] buffer = Encoding.UTF8.GetBytes(soapFault);
                context.Response.ContentType = "text/xml; charset=utf-8"; // Important!
                context.Response.StatusCode = 500;
                context.Response.ContentLength64 = buffer.Length;
                context.Response.OutputStream.Write(buffer, 0, buffer.Length);
                context.Response.Close();
            }
            catch {}
        }

        private void SendSoapResponse(HttpListenerContext context, string responseName, string resultName, string resultValue)
        {
            string soap = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<soap:Envelope xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/"">
  <soap:Body>
    <{responseName} xmlns=""CyntecMES"">
      <{resultName}>{System.Net.WebUtility.HtmlEncode(resultValue)}</{resultName}>
    </{responseName}>
  </soap:Body>
</soap:Envelope>";

            byte[] buffer = Encoding.UTF8.GetBytes(soap);
            context.Response.ContentType = "text/xml; charset=utf-8";
            context.Response.ContentLength64 = buffer.Length;
            context.Response.OutputStream.Write(buffer, 0, buffer.Length);
            context.Response.OutputStream.Close();
        }

        private void UpdateUI(TextBox txt, string msg)
        {
            if (txt.InvokeRequired)
            {
                txt.Invoke(new Action(() => UpdateUI(txt, msg)));
            }
            else
            {
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                txt.AppendText($"[{timestamp}] {msg}" + Environment.NewLine + Environment.NewLine);
            }
        }

        private void WriteLog(string type, string message)
        {
            try
            {
                // Set LogType property for log4net path
                LogicalThreadContext.Properties["LogType"] = type;
                log.Info(message);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Logging Failed: {ex.Message}");
            }
        }
    }
}
