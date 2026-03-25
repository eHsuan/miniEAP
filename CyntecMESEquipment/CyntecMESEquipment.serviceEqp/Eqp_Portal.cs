using System;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading;
using System.Web.Services;
using System.Web.Services.Description;
using System.Web.Services.Protocols;
using CyntecMESEquipment.Properties;

namespace CyntecMESEquipment.serviceEqp;

[GeneratedCode("System.Web.Services", "4.7.3190.0")]
[DebuggerStepThrough]
[DesignerCategory("code")]
[WebServiceBinding(Name = "Eqp_PortalSoap", Namespace = "CyntecMES")]
public class Eqp_Portal : SoapHttpClientProtocol
{
	private SendOrPostCallback EqpTransactionOperationCompleted;

	private SendOrPostCallback InsertEqpErrorLogOperationCompleted;

	private SendOrPostCallback InsertEqpTransactionLogOperationCompleted;

	private bool useDefaultCredentialsSetExplicitly;

	public new string Url
	{
		get
		{
			return base.Url;
		}
		set
		{
			if (IsLocalFileSystemWebService(base.Url) && !useDefaultCredentialsSetExplicitly && !IsLocalFileSystemWebService(value))
			{
				base.UseDefaultCredentials = false;
			}
			base.Url = value;
		}
	}

	public new bool UseDefaultCredentials
	{
		get
		{
			return base.UseDefaultCredentials;
		}
		set
		{
			base.UseDefaultCredentials = value;
			useDefaultCredentialsSetExplicitly = true;
		}
	}

	public event EqpTransactionCompletedEventHandler EqpTransactionCompleted;

	public event InsertEqpErrorLogCompletedEventHandler InsertEqpErrorLogCompleted;

	public event InsertEqpTransactionLogCompletedEventHandler InsertEqpTransactionLogCompleted;

	public Eqp_Portal()
	{
		Url = Settings.Default.CyntecMESEquipment_serviceEqp_Eqp_Portal;
		if (IsLocalFileSystemWebService(Url))
		{
			UseDefaultCredentials = true;
			useDefaultCredentialsSetExplicitly = false;
		}
		else
		{
			useDefaultCredentialsSetExplicitly = true;
		}
	}

	[SoapDocumentMethod("CyntecMES/EqpTransaction", RequestNamespace = "CyntecMES", ResponseNamespace = "CyntecMES", Use = SoapBindingUse.Literal, ParameterStyle = SoapParameterStyle.Wrapped)]
	public string EqpTransaction(string pParameter)
	{
		object[] array = Invoke("EqpTransaction", new object[1] { pParameter });
		return (string)array[0];
	}

	public void EqpTransactionAsync(string pParameter)
	{
		EqpTransactionAsync(pParameter, null);
	}

	public void EqpTransactionAsync(string pParameter, object userState)
	{
		if (EqpTransactionOperationCompleted == null)
		{
			EqpTransactionOperationCompleted = OnEqpTransactionOperationCompleted;
		}
		InvokeAsync("EqpTransaction", new object[1] { pParameter }, EqpTransactionOperationCompleted, userState);
	}

	private void OnEqpTransactionOperationCompleted(object arg)
	{
		if (this.EqpTransactionCompleted != null)
		{
			InvokeCompletedEventArgs e = (InvokeCompletedEventArgs)arg;
			this.EqpTransactionCompleted(this, new EqpTransactionCompletedEventArgs(e.Results, e.Error, e.Cancelled, e.UserState));
		}
	}

	[SoapDocumentMethod("CyntecMES/InsertEqpErrorLog", RequestNamespace = "CyntecMES", ResponseNamespace = "CyntecMES", Use = SoapBindingUse.Literal, ParameterStyle = SoapParameterStyle.Wrapped)]
	public string InsertEqpErrorLog(string pParameter)
	{
		object[] array = Invoke("InsertEqpErrorLog", new object[1] { pParameter });
		return (string)array[0];
	}

	public void InsertEqpErrorLogAsync(string pParameter)
	{
		InsertEqpErrorLogAsync(pParameter, null);
	}

	public void InsertEqpErrorLogAsync(string pParameter, object userState)
	{
		if (InsertEqpErrorLogOperationCompleted == null)
		{
			InsertEqpErrorLogOperationCompleted = OnInsertEqpErrorLogOperationCompleted;
		}
		InvokeAsync("InsertEqpErrorLog", new object[1] { pParameter }, InsertEqpErrorLogOperationCompleted, userState);
	}

	private void OnInsertEqpErrorLogOperationCompleted(object arg)
	{
		if (this.InsertEqpErrorLogCompleted != null)
		{
			InvokeCompletedEventArgs e = (InvokeCompletedEventArgs)arg;
			this.InsertEqpErrorLogCompleted(this, new InsertEqpErrorLogCompletedEventArgs(e.Results, e.Error, e.Cancelled, e.UserState));
		}
	}

	[SoapDocumentMethod("CyntecMES/InsertEqpTransactionLog", RequestNamespace = "CyntecMES", ResponseNamespace = "CyntecMES", Use = SoapBindingUse.Literal, ParameterStyle = SoapParameterStyle.Wrapped)]
	public string InsertEqpTransactionLog(string pParameter)
	{
		object[] array = Invoke("InsertEqpTransactionLog", new object[1] { pParameter });
		return (string)array[0];
	}

	public void InsertEqpTransactionLogAsync(string pParameter)
	{
		InsertEqpTransactionLogAsync(pParameter, null);
	}

	public void InsertEqpTransactionLogAsync(string pParameter, object userState)
	{
		if (InsertEqpTransactionLogOperationCompleted == null)
		{
			InsertEqpTransactionLogOperationCompleted = OnInsertEqpTransactionLogOperationCompleted;
		}
		InvokeAsync("InsertEqpTransactionLog", new object[1] { pParameter }, InsertEqpTransactionLogOperationCompleted, userState);
	}

	private void OnInsertEqpTransactionLogOperationCompleted(object arg)
	{
		if (this.InsertEqpTransactionLogCompleted != null)
		{
			InvokeCompletedEventArgs e = (InvokeCompletedEventArgs)arg;
			this.InsertEqpTransactionLogCompleted(this, new InsertEqpTransactionLogCompletedEventArgs(e.Results, e.Error, e.Cancelled, e.UserState));
		}
	}

	public new void CancelAsync(object userState)
	{
		base.CancelAsync(userState);
	}

	private bool IsLocalFileSystemWebService(string url)
	{
		if (url == null || url == string.Empty)
		{
			return false;
		}
		Uri uri = new Uri(url);
		if (uri.Port >= 1024 && string.Compare(uri.Host, "localHost", StringComparison.OrdinalIgnoreCase) == 0)
		{
			return true;
		}
		return false;
	}
}
