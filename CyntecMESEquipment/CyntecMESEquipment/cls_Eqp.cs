using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using CyntecMESEquipment.serviceEqp;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CyntecMESEquipment;

public class cls_Eqp
{
	private Eqp_Portal serviceEqp = new Eqp_Portal();

	private string MachNo = "";

	private string LogDefaultPath = "";

	public string MacID { get; private set; }

	public cls_Eqp(string pMachNo)
	{
		MachNo = pMachNo;
		SetLogDefaultPath();
		MacID = SearchMacID();
		SetServiceUrl();
	}

	public void SetServiceUrl(string pServiceUrl = "http://twcynmesqas01/CyntecDataCenter/service/Eqp/Eqp_Portal.asmx", int pTimeout = 30000)
	{
		serviceEqp.Url = pServiceUrl;
		serviceEqp.Timeout = pTimeout;
	}

	public void SetLogDefaultPath(string pLogDefaultPath = "C:/cyntec")
	{
		LogDefaultPath = pLogDefaultPath;
	}

	public string EqpTransaction(string pJSONParameter, string pMachNo, string pUserID)
	{
		string text = Guid.NewGuid().ToString().Trim();
		string text2 = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
		JObject jObject = null;
		JObject jObject2 = JsonConvert.DeserializeObject<JObject>(pJSONParameter);
		jObject2.Add("ModifyDate", text2);
		jObject2.Add("MacID", MacID);
		string text3 = JsonConvert.SerializeObject(jObject2);
		try
		{
			JObject jObject3 = new JObject();
			jObject3.Add("LogID", text);
			jObject3.Add("LogType", "TransactionStartLog");
			jObject3.Add("TransactionSide", "Client");
			jObject3.Add("MacID", MacID);
			jObject3.Add("MachID", pMachNo);
			jObject3.Add("ParameterJSON", text3);
			jObject3.Add("ModifyDate", text2);
			jObject3.Add("CreateUser", pUserID);
			TransactionStartLog(text2, text, "TransactionStartLog", MacID, pMachNo, text3);
			serviceEqp.InsertEqpTransactionLog(JsonConvert.SerializeObject(jObject3));
			string text4 = serviceEqp.EqpTransaction(text3);
			TransactionEndLog(text2, text, "TransactionEndLog", MacID, pMachNo, text3, text4);
			jObject3["LogType"] = "TransactionEndLog";
			jObject3.Add("InsertEqpTransactionLog", text4);
			serviceEqp.InsertEqpTransactionLog(JsonConvert.SerializeObject(jObject3));
			jObject = JsonConvert.DeserializeObject<JObject>(text4);
			if (jObject["Result"].ToString().Trim() != "success")
			{
				JObject jObject4 = new JObject();
				jObject4["LogID"] = text;
				jObject4["LogType"] = "Fail";
				jObject4["MacID"] = MacID;
				jObject4["MachID"] = pMachNo;
				jObject4["ErrorMsg"] = jObject["Message"].ToString().Trim();
				jObject4["ParameterJSON"] = text3;
				jObject4["ResultJSON"] = text4;
				jObject4["ModifyDate"] = text2;
				jObject4["CreateUser"] = pUserID;
				serviceEqp.InsertEqpErrorLog(JsonConvert.SerializeObject(jObject4));
				failLog(text2, text, "Fail", MacID, pMachNo, jObject["Message"].ToString().Trim(), text3, text4);
			}
		}
		catch (Exception ex)
		{
			StackTrace stackTrace = new StackTrace(ex, fNeedFileInfo: true);
			StackFrame frame = stackTrace.GetFrame(stackTrace.GetFrames().Count() - 1);
			JObject jObject5 = new JObject();
			jObject5["LogID"] = text;
			jObject5["LogType"] = "Exception";
			jObject5["MacID"] = MacID;
			jObject5["MachID"] = pMachNo;
			jObject5["ErrorMsg"] = ex.Message;
			jObject5["ParameterJSON"] = text3;
			jObject5["ResultJSON"] = "";
			jObject5["ModifyDate"] = text2;
			jObject5["CreateUser"] = pUserID;
			serviceEqp.InsertEqpErrorLog(JsonConvert.SerializeObject(jObject5));
			exceptionLog(text2, text, "Exception", MacID, pMachNo, "發生異常：" + ex.Message + Environment.NewLine + "發生錯誤檔案：" + Path.GetFileName(frame.GetFileName()) + Environment.NewLine + "錯誤行數：" + frame.GetFileLineNumber(), text3);
			jObject = new JObject();
			jObject.Add("Result", "fail");
			jObject.Add("Message", "發生異常：" + ex.Message + "||發生錯誤檔案：" + Path.GetFileName(frame.GetFileName()) + "||錯誤行數：" + frame.GetFileLineNumber());
		}
		return JsonConvert.SerializeObject(jObject);
	}

	private string InsertEqpErrorLog(string pParameter)
	{
		return serviceEqp.InsertEqpErrorLog(pParameter);
	}

	private string InsertEqpTransactionLog(string pParameter)
	{
		return serviceEqp.InsertEqpTransactionLog(pParameter);
	}

	private void TransactionStartLog(string pModifyDate, string pLogID, string pLogType, string pMacID, string pMachID, string pstrParameter)
	{
		string text = pMachID + "_TransactionStartLog_" + DateTime.Now.ToString("yyyyMMdd") + ".txt";
		if (!Directory.Exists(LogDefaultPath + "/TransactionStartLog"))
		{
			Directory.CreateDirectory(LogDefaultPath + "/TransactionStartLog");
		}
		string contents = pModifyDate + "||" + pLogID + "||" + pLogType + "||" + pMacID + "||" + pMachID + "||" + pstrParameter + Environment.NewLine;
		File.AppendAllText(LogDefaultPath + "/TransactionStartLog/" + text, contents);
	}

	private void TransactionEndLog(string pModifyDate, string pLogID, string pLogType, string pMacID, string pMachID, string pstrParameter, string pstrResult)
	{
		string text = pMachID + "_TransactionEndLog_" + DateTime.Now.ToString("yyyyMMdd") + ".txt";
		if (!Directory.Exists(LogDefaultPath + "/TransactionEndLog"))
		{
			Directory.CreateDirectory(LogDefaultPath + "/TransactionEndLog");
		}
		string contents = pModifyDate + "||" + pLogID + "||" + pLogType + "||" + pMacID + "||" + pMachID + "||" + pstrParameter + "||" + pstrResult + Environment.NewLine;
		File.AppendAllText(LogDefaultPath + "/TransactionEndLog/" + text, contents);
	}

	private void failLog(string pModifyDate, string pLogID, string pLogType, string pMacID, string pMachID, string pErrorMsg, string pstrParameter, string pstrResult)
	{
		string text = MachNo + "_fail_" + DateTime.Now.ToString("yyyyMMdd") + ".txt";
		if (!Directory.Exists(LogDefaultPath + "/faillog"))
		{
			Directory.CreateDirectory(LogDefaultPath + "/faillog");
		}
		string contents = pModifyDate + "||" + pLogID + "||" + pLogType + "||" + pMacID + "||" + pMachID + "||" + pErrorMsg + "||" + pstrParameter + "||" + pstrResult + Environment.NewLine;
		File.AppendAllText(LogDefaultPath + "/faillog/" + text, contents);
	}

	private void exceptionLog(string pModifyDate, string pLogID, string pLogType, string pMacID, string pMachID, string pExceptionMsg, string pstrParameter)
	{
		string text = MachNo + "_exception_" + DateTime.Now.ToString("yyyyMMdd") + ".txt";
		if (!Directory.Exists(LogDefaultPath + "/exceptionlog"))
		{
			Directory.CreateDirectory(LogDefaultPath + "/exceptionlog");
		}
		string contents = pModifyDate + "||" + pLogID + "||" + pLogType + "||" + pMacID + "||" + pMachID + "||" + pExceptionMsg + "||" + pstrParameter + Environment.NewLine;
		File.AppendAllText(LogDefaultPath + "/exceptionlog/" + text, contents);
	}

	private string SearchMacID()
	{
		return "12345";
	}
}
