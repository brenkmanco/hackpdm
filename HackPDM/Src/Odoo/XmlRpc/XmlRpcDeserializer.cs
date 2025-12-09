using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Xml;

namespace HackPDM.Odoo.XmlRpc;

public class XmlRpcDeserializer : XmlRpcXmlTokens
{
	private static readonly DateTimeFormatInfo DateFormat = new DateTimeFormatInfo();

	private object _container;

	private Stack _containerStack;

	protected string Text;
	protected object Value;

	protected string Name;

	public XmlRpcDeserializer()
	{
		Reset();
		DateFormat.FullDateTimePattern = "yyyyMMdd\\THH\\:mm\\:ss";
	}
	public virtual object Deserialize( TextReader xmlData )
	{
		return null;
	}

	protected void DeserializeNode( XmlTextReader reader )
	{
		switch ( reader.NodeType )
		{
			case XmlNodeType.Element:
				if ( Logger.Delegate != null )
				{
					Logger.WriteEntry( "START " + reader.Name, LogLevel.Information );
				}

				switch ( reader.Name )
				{
					case "value":
						Value = null;
						Text = null;
						break;
					case "struct":
						if ( !reader.IsEmptyElement )
						{
							PushContext();
							_container = new Hashtable();
						}

						break;
					case "array":
						if ( !reader.IsEmptyElement )
						{
							PushContext();
							_container = new ArrayList();
						}

						break;
				}

				break;
			case XmlNodeType.EndElement:
			{
				if ( Logger.Delegate != null )
				{
					Logger.WriteEntry( "END " + reader.Name, LogLevel.Information );
				}

				string name = reader.Name;
				if ( name == null )
				{
					break;
				}

				switch ( name )
				{
					case "base64":
						Value = Convert.FromBase64String( Text );
						break;
					case "boolean":
						switch ( short.Parse( Text ) )
						{
							case 0:
								Value = false;
								break;
							case 1:
								Value = true;
								break;
						}

						break;
					case "string":
						Value = Text;
						break;
					case "double":
						Value = double.Parse( Text );
						break;
					case "i4":
					case "int":
						Value = int.Parse( Text );
						break;
					case "dateTime.iso8601":
						Value = DateTime.ParseExact( Text, "F", DateFormat );
						break;
					case "name":
						Name = Text;
						break;
					case "value":
						if ( Value == null )
						{
							Value = Text;
						}

						if ( _container != null && _container is IList )
						{
							( (IList)_container ).Add( Value );
						}

						break;
					case "member":
						if ( _container != null && _container is IDictionary )
						{
							( (IDictionary)_container ).Add( Name, Value );
						}

						break;
					case "array":
					case "struct":
						Value = _container;
						PopContext();
						break;
				}

				break;
			}
			case XmlNodeType.Text:
				if ( Logger.Delegate != null )
				{
					Logger.WriteEntry( "Text " + reader.Value, LogLevel.Information );
				}

				Text = reader.Value;
				break;
		}
	}

	public object Deserialize( string xmlData )
	{
		StringReader xmlData2 = new StringReader(xmlData);
		return Deserialize( xmlData2 );
	}

	private void PopContext()
	{
		Context context = (Context)_containerStack.Pop();
		_container = context.Container;
		Name = context.Name;
	}

	private void PushContext()
	{
		Context context = default(Context);
		context.Container = _container;
		context.Name = Name;
		_containerStack.Push( context );
	}

	protected void Reset()
	{
		Text = null;
		Value = null;
		Name = null;
		_container = null;
		_containerStack = new Stack();
	}
}