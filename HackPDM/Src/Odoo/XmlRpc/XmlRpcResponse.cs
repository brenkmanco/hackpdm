using System.Collections;

namespace HackPDM.Odoo.XmlRpc;

public class XmlRpcResponse
{
	private object _value;

	public bool IsFault;

	public object Value
	{
		get
		{
			return _value;
		}
		set
		{
			IsFault = false;
			_value = value;
		}
	}

	public int FaultCode
	{
		get
		{
			if ( !IsFault )
			{
				return 0;
			}

			return (int)( (Hashtable)_value ) [ "faultCode" ];
		}
	}

	public string FaultString
	{
		get
		{
			if ( !IsFault )
			{
				return "";
			}

			return (string)( (Hashtable)_value ) [ "faultString" ];
		}
	}

	public XmlRpcResponse()
	{
		Value = null;
		IsFault = false;
	}

	public XmlRpcResponse( int code, string message )
		: this()
	{
		SetFault( code, message );
	}

	public void SetFault( int code, string message )
	{
		Hashtable hashtable = new Hashtable
		{
			{ "faultCode", code },
			{ "faultString", message }
		};
		Value = hashtable;
		IsFault = true;
	}

	public override string ToString()
	{
		return XmlRpcResponseSerializer.Singleton.Serialize( this );
	}
}