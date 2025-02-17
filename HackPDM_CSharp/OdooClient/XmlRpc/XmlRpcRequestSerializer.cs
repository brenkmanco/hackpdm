using System.Xml;

namespace XmlRpc.Goober
{
	internal class XmlRpcRequestSerializer : XmlRpcSerializer
	{
		private static XmlRpcRequestSerializer _singleton;

		public static XmlRpcRequestSerializer Singleton
		{
			get
			{
				if ( _singleton == null )
				{
					_singleton = new XmlRpcRequestSerializer();
				}

				return _singleton;
			}
		}

		public override void Serialize( XmlTextWriter output, object obj )
		{
			XmlRpcRequest xmlRpcRequest = (XmlRpcRequest)obj;
			output.WriteStartDocument();
			output.WriteStartElement( "methodCall" );
			output.WriteElementString( "methodName", xmlRpcRequest.MethodName );
			output.WriteStartElement( "params" );
			foreach ( object param in xmlRpcRequest.Params )
			{
				output.WriteStartElement( "param" );
				output.WriteStartElement( "value" );
				SerializeObject( output, param );
				output.WriteEndElement();
				output.WriteEndElement();
			}

			output.WriteEndElement();
			output.WriteEndElement();
		}
	}
}