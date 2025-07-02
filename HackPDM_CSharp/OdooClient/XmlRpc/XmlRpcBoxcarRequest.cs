using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XmlRpc.Goober
{
	public class XmlRpcBoxcarRequest : XmlRpcRequest
	{
		public IList Requests = new ArrayList();

		public override string MethodName => "system.multiCall";

		public override IList Params
		{
			get
			{
				_params.Clear();
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

				_params.Add( arrayList );
				return _params;
			}
		}
	}
}
