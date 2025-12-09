using System.Collections;

namespace HackPDM.Odoo.XmlRpc;

public class XmlRpcBoxcarRequest : XmlRpcRequest
{
	public IList Requests = new ArrayList();

	public override string MethodName => "system.multiCall";

	public override IList Params
	{
		get
		{
			field.Clear();
			ArrayList arrayList = [];
			foreach ( XmlRpcRequest request in Requests )
			{
				Hashtable hashtable = new Hashtable
				{
					{ "methodName", request.MethodName },
					{ "params", request.Params }
				};
				arrayList.Add( hashtable );
			}

			field.Add( arrayList );
			return field;
		}
	}
}