using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace XmlRpc.Goober
{
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
								xmlRpcResponse.Value = _value;
								xmlRpcResponse.IsFault = true;
								break;
							case "param":
								xmlRpcResponse.Value = _value;
								_value = null;
								_text = null;
								break;
						}
					}
				}
			}

			return xmlRpcResponse;
		}
	}
}
