using System.Xml;

namespace XmlRpc.Goober
{
	public class XmlRpcResponseSerializer : XmlRpcSerializer
	{
		public static XmlRpcResponseSerializer Singleton
		{
			get
			{
				if ( field == null )
				{
					field = new XmlRpcResponseSerializer();
				}

				return field;
			}
		}

		public override void Serialize( XmlTextWriter output, object obj )
		{
			XmlRpcResponse xmlRpcResponse = (XmlRpcResponse)obj;
			output.WriteStartDocument();
			output.WriteStartElement( "methodResponse" );
			if ( xmlRpcResponse.IsFault )
			{
				output.WriteStartElement( "fault" );
			}
			else
			{
				output.WriteStartElement( "params" );
				output.WriteStartElement( "param" );
			}

			output.WriteStartElement( "value" );
			SerializeObject( output, xmlRpcResponse.Value );
			output.WriteEndElement();
			output.WriteEndElement();
			if ( !xmlRpcResponse.IsFault )
			{
				output.WriteEndElement();
			}

			output.WriteEndElement();
		}
	}
}