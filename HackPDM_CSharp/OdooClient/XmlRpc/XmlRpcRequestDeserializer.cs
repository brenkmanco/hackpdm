using System.Xml;
using System.IO;

namespace XmlRpc.Goober
{
	public class XmlRpcRequestDeserializer : XmlRpcDeserializer
	{
		private static XmlRpcRequestDeserializer _singleton;

		public override object Deserialize( TextReader xmlData )
		{
			XmlTextReader xmlTextReader = new XmlTextReader(xmlData);
			XmlRpcRequest xmlRpcRequest = new XmlRpcRequest();
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
							case "methodName":
								xmlRpcRequest.MethodName = _text;
								break;
							case "methodCall":
								flag = true;
								break;
							case "param":
								xmlRpcRequest.Params.Add( _value );
								_text = null;
								break;
						}
					}
				}
			}

			return xmlRpcRequest;
		}
	}
}