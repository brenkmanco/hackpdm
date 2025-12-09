using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Xml;

namespace HackPDM.Odoo.XmlRpc;

public class XmlRpcRequest
{
	private readonly XmlRpcResponseDeserializer _deserializer = new XmlRpcResponseDeserializer();

	private readonly Encoding _encoding = new ASCIIEncoding();


	private readonly XmlRpcRequestSerializer _serializer = new XmlRpcRequestSerializer();

	public virtual IList Params { get; }

	public virtual string MethodName
	{
		get; set;
	}

	public string MethodNameObject
	{
		get
		{
			int num = MethodName.IndexOf(".", StringComparison.Ordinal);
			if ( num == -1 )
			{
				return MethodName;
			}

			return MethodName.Substring( 0, num );
		}
	}

	public string MethodNameMethod
	{
		get
		{
			int num = MethodName.IndexOf(".", StringComparison.Ordinal);
			if ( num == -1 )
			{
				return MethodName;
			}

			return MethodName.Substring( num + 1, MethodName.Length - num - 1 );
		}
	}

	public XmlRpcRequest()
	{
		Params = new ArrayList();
	}

	public XmlRpcRequest( string methodName, IList parameters )
	{
		MethodName = methodName;
        Params = parameters;
	}

	public object Invoke( string url )
	{
		XmlRpcResponse? xmlRpcResponse = Send(url);
		if ( xmlRpcResponse?.IsFault == true )
		{
			throw new XmlRpcException( xmlRpcResponse.FaultCode, xmlRpcResponse.FaultString );
		}

		return xmlRpcResponse?.Value ?? false;
	}

	public XmlRpcResponse? Send( string url, int timeout = 0, IWebProxy proxy = null )
	{
		//HttpClient client = new();
		//SocketsHttpHandler handler = new()
		//{
		//	Proxy = proxy,
		//	UseProxy = proxy != null,
			
		//};
		//using var request = new HttpRequestMessage(HttpMethod.Post, url);
		HttpWebRequest httpWebRequest = (HttpWebRequest)WebRequest.Create(url);
		if ( httpWebRequest == null )
		{
			throw new XmlRpcException( -32300, "Transport Layer Error: Could not create request with " + url );
		}

		httpWebRequest.Proxy = proxy;
		httpWebRequest.Method = "POST";
		httpWebRequest.ContentType = "text/xml";
		httpWebRequest.AllowWriteStreamBuffering = true;
		if ( timeout > 0 )
		{
			httpWebRequest.Timeout = timeout;
		}

		XmlTextWriter xmlTextWriter = new XmlTextWriter(httpWebRequest.GetRequestStream(), _encoding);
		_serializer.Serialize( xmlTextWriter, this );
		xmlTextWriter.Flush();
		xmlTextWriter.Close();
		HttpWebResponse? httpWebResponse;
		try
		{
			httpWebResponse = (HttpWebResponse)httpWebRequest.GetResponse();
		}
		catch
		{
			Debug.WriteLine("web request exception");
			return null;
		}
		StreamReader streamReader = new StreamReader(httpWebResponse?.GetResponseStream());
		XmlRpcResponse result = (XmlRpcResponse)_deserializer.Deserialize(streamReader);
		streamReader.Close();
		httpWebResponse.Close();
		return result;
	}

	public override string ToString()
	{
		return _serializer.Serialize( this );
	}
}