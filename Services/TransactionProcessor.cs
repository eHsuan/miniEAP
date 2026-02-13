using System;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace miniEAP.Services
{
    public class TransactionProcessor
    {
        public int Multiplier { get; set; } = 1;
        public Action<string, string> OnLog { get; set; }

        private void Log(string type, string message)
        {
            OnLog?.Invoke(type, message);
        }

        public string ProcessTransactionPayload(string jsonPayload, bool isRequestFromEqp)
        {
            try
            {
                JObject transactionData = JObject.Parse(jsonPayload);

                // Global Logic: If response from MES contains RefInputQty, always divide
                if (!isRequestFromEqp && transactionData["RefInputQty"] != null)
                {
                    if (double.TryParse(transactionData["RefInputQty"].ToString(), out double val))
                    {
                        double newVal = Math.Ceiling(val / Multiplier);
                        Log("Logic", $"[Global] RefInputQty Divide: {val} -> {newVal} (Multiplier: {Multiplier})");
                        transactionData["RefInputQty"] = newVal.ToString("0.0");
                    }
                }

                if (transactionData["TransactionName"] != null)
                {
                    string txName = transactionData["TransactionName"].ToString();
                    
                    switch (txName)
                    {
                        case "UserVerify":
                            //v1.0.1 : Fix User2DBarCode sometimes not upper issue
                            if (transactionData["User2DBarCode"] != null)
                            {
                                string valBarCode = transactionData["User2DBarCode"].ToString();
                                string newValBarCode = valBarCode;
                                if (isRequestFromEqp)
                                {
                                    // EQ -> MES: Specific layering casing logic
                                    // Example: {{{{758798{A1{{{ae29c8c325a24ef3a4a61f8e0f1adf9f
                                    string[] parts = valBarCode.Split('{');
                                    int nonEmptyCount = 0;
                                    bool hasMultipleSegments = parts.Count(p => !string.IsNullOrEmpty(p)) > 1;

                                    if (hasMultipleSegments)
                                    {
                                        for (int i = 0; i < parts.Length; i++)
                                        {
                                            if (!string.IsNullOrEmpty(parts[i]))
                                            {
                                                nonEmptyCount++;
                                                if (nonEmptyCount == 2)
                                                {
                                                    parts[i] = parts[i].ToUpper(); // A1 layer
                                                }
                                                else if (nonEmptyCount == 3)
                                                {
                                                    parts[i] = parts[i].ToLower(); // Guid layer
                                                }
                                            }
                                        }
                                        newValBarCode = string.Join("{", parts);
                                    }
                                    else
                                    {
                                        // Fallback for simple barcode
                                        newValBarCode = valBarCode.ToUpper();
                                    }
                                    
                                    if (valBarCode != newValBarCode)
                                    {
                                        Log("Logic", $"[UserVerify] Barcode Normalization: '{valBarCode}' -> '{newValBarCode}'");
                                    }
                                }
                                transactionData["User2DBarCode"] = newValBarCode;
                                break;
                            }
                            break;
                        case "WOCHECKOUT":
                            if (transactionData["OutPut"] != null)
                            {
                                if (double.TryParse(transactionData["OutPut"].ToString(), out double val))
                                {
                                    double newVal = val;
                                    if (isRequestFromEqp)
                                    {
                                        // EQ -> MES: Multiply
                                        newVal = Math.Ceiling(val * Multiplier);
                                        Log("Logic", $"[WOCHECKOUT] OutPut Multiply: {val} -> {newVal} (Multiplier: {Multiplier})");
                                    }
                                    transactionData["RefInputQty"] = newVal.ToString("0.0");
                                }
                            }
                            break;
                    }
                }
                return transactionData.ToString(Newtonsoft.Json.Formatting.None);
            }
            catch (Exception ex)
            {
                Log("Error", $"[ProcessTransactionPayload] Exception: {ex.Message}");
                return jsonPayload;
            }
        }
    }
}
