using System;
using System.Runtime.Serialization;

namespace XmlRpc.Goober
{
	[Serializable]
	internal class XmlRpcException : Exception
	{
		public string FaultString => Message;
		public int FaultCode
		{
			get;
		}

		public XmlRpcException( int code, string message )
			: base( message )
		{
			FaultCode = code;
		}

		public override string ToString()
		{
			return "Code: " + FaultCode + " Message: " + base.ToString();
		}
	}
}