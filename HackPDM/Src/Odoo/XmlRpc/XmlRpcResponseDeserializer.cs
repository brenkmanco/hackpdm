using System.IO;
using System.Xml;

namespace HackPDM.Odoo.XmlRpc;

public class XmlRpcResponseDeserializer : XmlRpcDeserializer
{
	private static XmlRpcResponseDeserializer _singleton;

	public override object Deserialize( TextReader xmlData )
		=> DeserializeResponse( xmlData );
	public XmlRpcResponse DeserializeResponse( TextReader xmlData )
	{
		XmlTextReader xmlTextReader = new XmlTextReader(xmlData);
		XmlRpcResponse xmlRpcResponse = new XmlRpcResponse();
		bool flag = false;
		lock ( this )
		{
			Reset();
			while ( !flag && xmlTextReader.Read() )
			{
				DeserializeNode( xmlTextReader );
				if ( xmlTextReader.NodeType == XmlNodeType.EndElement )
				{
					switch ( xmlTextReader.Name )
					{
						case "fault":
							xmlRpcResponse.Value = Value;
							xmlRpcResponse.IsFault = true;
							break;
						case "param":
							xmlRpcResponse.Value = Value;
							Value = null;
							Text = null;
							break;
					}
				}
			}
		}

		return xmlRpcResponse;
	}
}