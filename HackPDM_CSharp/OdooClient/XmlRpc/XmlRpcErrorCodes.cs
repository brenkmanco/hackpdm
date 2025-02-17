using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XmlRpc.Goober
{
	public class XmlRpcErrorCodes
	{
		public const int PARSE_ERROR_MALFORMED = -32700;

		public const string PARSE_ERROR_MALFORMED_MSG = "Parse Error, not well formed";

		public const int PARSE_ERROR_ENCODING = -32701;

		public const string PARSE_ERROR_ENCODING_MSG = "Parse Error, unsupported encoding";

		public const int SERVER_ERROR_METHOD = -32601;

		public const string SERVER_ERROR_METHOD_MSG = "Server Error, requested method not found";

		public const int SERVER_ERROR_PARAMS = -32602;

		public const string SERVER_ERROR_PARAMS_MSG = "Server Error, invalid method parameters";

		public const int APPLICATION_ERROR = -32500;

		public const string APPLICATION_ERROR_MSG = "Application Error";

		public const int TRANSPORT_ERROR = -32300;

		public const string TRANSPORT_ERROR_MSG = "Transport Layer Error";
	}
}
