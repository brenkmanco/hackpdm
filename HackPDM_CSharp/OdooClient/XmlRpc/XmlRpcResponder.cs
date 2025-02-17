using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace XmlRpc.Goober
{
	public class XmlRpcResponder
	{
		private TcpClient _client;

		private readonly XmlRpcRequestDeserializer _deserializer = new XmlRpcRequestDeserializer();

		private readonly XmlRpcResponseSerializer _serializer = new XmlRpcResponseSerializer();

		private readonly XmlRpcServer _server;

		public SimpleHttpRequest HttpReq
		{
			get; private set;
		}

		public XmlRpcResponder( XmlRpcServer server, TcpClient client )
		{
			_server = server;
			_client = client;
			HttpReq = new SimpleHttpRequest( _client );
		}

		~XmlRpcResponder()
		{
			Close();
		}

		public void Respond()
		{
			Respond( HttpReq );
		}

		public void Respond( SimpleHttpRequest httpReq )
		{
			XmlRpcRequest req = (XmlRpcRequest)_deserializer.Deserialize(httpReq.Input);
			XmlRpcResponse xmlRpcResponse = new XmlRpcResponse();
			try
			{
				xmlRpcResponse.Value = _server.Invoke( req );
			}
			catch ( XmlRpcException ex )
			{
				xmlRpcResponse.SetFault( ex.FaultCode, ex.FaultString );
			}
			catch ( Exception ex2 )
			{
				xmlRpcResponse.SetFault( -32500, "Application Error: " + ex2.Message );
			}

			if ( Logger.Delegate != null )
			{
				Logger.WriteEntry( xmlRpcResponse.ToString(), LogLevel.Information );
			}

			XmlRpcServer.HttpHeader( httpReq.Protocol, "text/xml", 0L, " 200 OK", httpReq.Output );
			httpReq.Output.Flush();
			XmlTextWriter xmlTextWriter = new XmlTextWriter(httpReq.Output);
			_serializer.Serialize( xmlTextWriter, xmlRpcResponse );
			xmlTextWriter.Flush();
			httpReq.Output.Flush();
		}

		public void Close()
		{
			if ( HttpReq != null )
			{
				HttpReq.Close();
				HttpReq = null;
			}

			if ( _client != null )
			{
				_client.Close();
				_client = null;
			}
		}
	}
}
