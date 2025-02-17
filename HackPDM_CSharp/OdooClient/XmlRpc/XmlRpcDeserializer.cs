
using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Xml;

namespace XmlRpc.Goober
{
	public class XmlRpcDeserializer : XmlRpcXmlTokens
	{
		private static readonly DateTimeFormatInfo _dateFormat = new DateTimeFormatInfo();

		private object _container;

		private Stack _containerStack;

		protected string _text;
		protected object _value;

		protected string _name;

		public XmlRpcDeserializer()
		{
			Reset();
			_dateFormat.FullDateTimePattern = "yyyyMMdd\\THH\\:mm\\:ss";
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
							_value = null;
							_text = null;
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
							_value = Convert.FromBase64String( _text );
							break;
						case "boolean":
							switch ( short.Parse( _text ) )
							{
								case 0:
									_value = false;
									break;
								case 1:
									_value = true;
									break;
							}

							break;
						case "string":
							_value = _text;
							break;
						case "double":
							_value = double.Parse( _text );
							break;
						case "i4":
						case "int":
							_value = int.Parse( _text );
							break;
						case "dateTime.iso8601":
							_value = DateTime.ParseExact( _text, "F", _dateFormat );
							break;
						case "name":
							_name = _text;
							break;
						case "value":
							if ( _value == null )
							{
								_value = _text;
							}

							if ( _container != null && _container is IList )
							{
								( (IList)_container ).Add( _value );
							}

							break;
						case "member":
							if ( _container != null && _container is IDictionary )
							{
								( (IDictionary)_container ).Add( _name, _value );
							}

							break;
						case "array":
						case "struct":
							_value = _container;
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

					_text = reader.Value;
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
			_name = context.Name;
		}

		private void PushContext()
		{
			Context context = default(Context);
			context.Container = _container;
			context.Name = _name;
			_containerStack.Push( context );
		}

		protected void Reset()
		{
			_text = null;
			_value = null;
			_name = null;
			_container = null;
			_containerStack = new Stack();
		}
	}
}