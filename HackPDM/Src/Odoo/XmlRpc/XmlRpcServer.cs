using System;
using System.Collections;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace HackPDM.Odoo.XmlRpc;

public class XmlRpcServer : IEnumerable
{
	private const int RESPONDER_COUNT = 10;

	private readonly IDictionary _handlers;

	private readonly int _port;

	private readonly WaitCallback _wc;

	private IPAddress _address;

	private TcpListener _myListener;

	private XmlRpcSystemObject _system;

	public object this [ string name ] => _handlers [ name ];

	public XmlRpcServer( IPAddress address, int port )
	{
		_port = port;
		_address = address;
		_handlers = new Hashtable();
		_system = new XmlRpcSystemObject( this );
		_wc = WaitCallback;
	}

	public XmlRpcServer( int port )
		: this( IPAddress.Any, port )
	{
	}

	public IEnumerator GetEnumerator()
	{
		return _handlers.GetEnumerator();
	}

	public void Start()
	{
		try
		{
			Stop();
			lock ( this )
			{
				_myListener = new TcpListener( IPAddress.Any, _port );
				_myListener.Start();
				new Thread( StartListen ).Start();
			}
		}
		catch ( Exception ex )
		{
			Logger.WriteEntry( "An Exception Occurred while Listening :" + ex, LogLevel.Error );
		}
	}

	public void Stop()
	{
		try
		{
			if ( _myListener != null )
			{
				lock ( this )
				{
					_myListener.Stop();
					_myListener = null;
					return;
				}
			}
		}
		catch ( Exception ex )
		{
			Logger.WriteEntry( "An Exception Occurred while stopping :" + ex, LogLevel.Error );
		}
	}

	public void StartListen()
	{
		while ( _myListener != null )
		{
			XmlRpcResponder state = new XmlRpcResponder(this, _myListener.AcceptTcpClient());
			ThreadPool.QueueUserWorkItem( _wc, state );
		}
	}

	public void Add( string name, object obj )
	{
		_handlers.Add( name, obj );
	}

	public string MethodName( string methodName )
	{
		int num = methodName.LastIndexOf('.');
		if ( num == -1 )
		{
			throw new XmlRpcException( -32601, "Server Error, requested method not found: Bad method name " + methodName );
		}

		string text = methodName.Substring(0, num);
		return ( _handlers [ text ] ?? throw new XmlRpcException( -32601, "Server Error, requested method not found: Object " + text + " not found" ) ).GetType().FullName + "." + methodName.Substring( num + 1 );
	}

	public object Invoke( XmlRpcRequest req )
	{
		return Invoke( req.MethodNameObject, req.MethodNameMethod, req.Params );
	}

	public object Invoke( string objectName, string methodName, IList parameters )
	{
		return XmlRpcSystemObject.Invoke( _handlers [ objectName ] ?? throw new XmlRpcException( -32601, "Server Error, requested method not found: Object " + objectName + " not found" ), methodName, parameters );
	}

	public void WaitCallback( object responder )
	{
		XmlRpcResponder xmlRpcResponder = (XmlRpcResponder)responder;
		if ( xmlRpcResponder.HttpReq.HttpMethod == "POST" )
		{
			try
			{
				xmlRpcResponder.Respond();
			}
			catch ( Exception ex )
			{
				Logger.WriteEntry( "Failed on post: " + ex, LogLevel.Error );
			}
		}
		else
		{
			Logger.WriteEntry( "Only POST methods are supported: " + xmlRpcResponder.HttpReq.HttpMethod + " ignored", LogLevel.Error );
		}

		xmlRpcResponder.Close();
	}

	public static void HttpHeader( string sHttpVersion, string sMimeHeader, long iTotBytes, string sStatusCode, TextWriter output )
	{
		string text = "";
		if ( sMimeHeader.Length == 0 )
		{
			sMimeHeader = "text/html";
		}

		text = text + sHttpVersion + sStatusCode + "\r\n";
		text += "Connection: close\r\n";
		if ( iTotBytes > 0 )
		{
			text = text + "Content-Length: " + iTotBytes + "\r\n";
		}

		text += "Server: XmlRpcServer \r\n";
		text = text + "Content-Type: " + sMimeHeader + "\r\n";
		text += "\r\n";
		output.Write( text );
	}
}