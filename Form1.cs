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

namespace miniEAP
{
    public partial class Form1 : Form
    {
        private string _ver = "1.0.2"; //版本號
        private HttpListener _listener;
        private bool _isRunning = false;
        private bool _isTestMode = false;
        private readonly TransactionProcessor _processor = new TransactionProcessor();
        private readonly object _logLock = new object();
        private readonly string _settingFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "multiplier.config");
        private bool _heartbeatToggle = false;

        public Form1()
        {
            InitializeComponent();
            _processor.OnLog = (type, msg) => WriteLog(type, msg);
        }

        private void Form1_Load(object sender, EventArgs e)
        {
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

            StartServer();
            timerHeartbeat.Start();
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
                        WriteLog("Receive from EQ", parameter);
                        UpdateUI(txtReceive, $"[Receive from EQ]: {parameter}");

                        // 2. Logic: Multiply RefInputQty
                        string modifiedParam = _processor.ProcessTransactionPayload(parameter, true);

                        // 3. Send to MES
                        WriteLog("Send to MES", modifiedParam);
                        UpdateUI(txtSend, $"[Send to MES]: {modifiedParam}");
                        
                        if (_isTestMode)
                        {
                            WriteLog("Test Mode", "Skipping MES transaction. Returning simulated success.");
                            
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

                        // 4. Receive from MES
                        WriteLog("Receive from MES", result);
                        UpdateUI(txtReceive, $"[Receive from MES]: {result}");
                        
                        // 5. Logic: Divide RefInputQty
                        string modifiedResult = _processor.ProcessTransactionPayload(result, false);

                        // 6. Send to EQ
                        WriteLog("Send to EQ", modifiedResult);
                        UpdateUI(txtSend, $"[Send to EQ]: {modifiedResult}");

                        SendSoapResponse(context, "EqpTransactionResponse", "EqpTransactionResult", modifiedResult);
                    }
                    else if (methodName == "InsertEqpErrorLog")
                    {
                            WriteLog("Receive from EQ (Log)", parameter);
                            Eqp_Portal service = new Eqp_Portal();
                            result = service.InsertEqpErrorLog(parameter);
                            SendSoapResponse(context, "InsertEqpErrorLogResponse", "InsertEqpErrorLogResult", result);
                    }
                    else if (methodName == "InsertEqpTransactionLog")
                    {
                            WriteLog("Receive from EQ (Log)", parameter);
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
            lock (_logLock)
            {
                try
                {
                    string month = DateTime.Now.ToString("MM"); // 01, 02...
                    string date = DateTime.Now.ToString("yyyy-MM-dd");
                    string logDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "log", month);
                    
                    if (!Directory.Exists(logDir))
                    {
                        Directory.CreateDirectory(logDir);
                    }

                    string logFile = Path.Combine(logDir, $"{date}_Log.txt");
                    string content = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{type}] {message}{Environment.NewLine}";
                    
                    File.AppendAllText(logFile, content);
                }
                catch (Exception ex)
                {
                    // Fallback in case logging fails
                    System.Diagnostics.Debug.WriteLine($"Logging Failed: {ex.Message}");
                }
            }
        }
    }
}
