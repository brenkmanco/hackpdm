using System;
using System.Collections;
using System.IO;
using System.Xml;

namespace HackPDM.Odoo.XmlRpc;

public class XmlRpcSerializer
{
	public virtual void Serialize( XmlTextWriter output, object obj )
	{
	}

	public string Serialize( object obj )
	{
		StringWriter stringWriter = new StringWriter();
		XmlTextWriter xmlTextWriter = new XmlTextWriter(stringWriter)
		{
			Formatting = Formatting.Indented,
			Indentation = 4
		};
		Serialize( xmlTextWriter, obj );
		xmlTextWriter.Flush();
		string result = stringWriter.ToString();
		xmlTextWriter.Close();
		return result;
	}

	public void SerializeObject( XmlTextWriter output, object obj )
	{
		if ( obj == null )
		{
			return;
		}

		if ( obj is byte [] )
		{
			byte[] array = (byte[])obj;
			output.WriteStartElement( "base64" );
			output.WriteBase64( array, 0, array.Length );
			output.WriteEndElement();
		}
		else if ( obj is string )
		{
			output.WriteElementString( "string", obj.ToString() );
		}
		else if ( obj is int )
		{
			output.WriteElementString( "i4", obj.ToString() );
		}
		else if ( obj is DateTime )
		{
			output.WriteElementString( "dateTime.iso8601", ( (DateTime)obj ).ToString( "yyyyMMdd\\THH\\:mm\\:ss" ) );
		}
		else if ( obj is double )
		{
			output.WriteElementString( "double", obj.ToString() );
		}
		else if ( obj is bool )
		{
			output.WriteElementString( "boolean", ( (bool)obj ) ? "1" : "0" );
		}
		else if ( obj is IList )
		{
			output.WriteStartElement( "array" );
			output.WriteStartElement( "data" );
			if ( ( (ArrayList)obj ).Count > 0 )
			{
				foreach ( object item in (IList)obj )
				{
					output.WriteStartElement( "value" );
					SerializeObject( output, item );
					output.WriteEndElement();
				}
			}

			output.WriteEndElement();
			output.WriteEndElement();
		}
		else
		{
			if ( !( obj is IDictionary ) )
			{
				return;
			}

			IDictionary dictionary = (IDictionary)obj;
			output.WriteStartElement( "struct" );
			foreach ( string key in dictionary.Keys )
			{
				output.WriteStartElement( "member" );
				output.WriteElementString( "name", key );
				output.WriteStartElement( "value" );
				SerializeObject( output, dictionary [ key ] );
				output.WriteEndElement();
				output.WriteEndElement();
			}

			output.WriteEndElement();
		}
	}
}