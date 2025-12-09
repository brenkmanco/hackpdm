using System;
using System.Collections;
using System.Reflection;

namespace HackPDM.Odoo.XmlRpc;

[XmlRpcExposed]
public class XmlRpcSystemObject
{
	private readonly XmlRpcServer _server;

	public static IDictionary MethodHelper { get; } = new Hashtable();


	public XmlRpcSystemObject( XmlRpcServer server )
	{
		_server = server;
		server.Add( "system", this );
        MethodHelper.Add( GetType().FullName + ".methodHelp", "Return a string description." );
	}

	public static object Invoke( object target, string methodName, IList parameters )
	{
		if ( target == null )
		{
			throw new XmlRpcException( -32601, "Server Error, requested method not found: Invalid target object." );
		}

		MethodInfo method = target.GetType().GetMethod(methodName);
		try
		{
			if ( !XmlRpcExposedAttribute.ExposedMethod( target, methodName ) )
			{
				throw new XmlRpcException( -32601, "Server Error, requested method not found: Method " + methodName + " is not exposed." );
			}
		}
		catch ( MissingMethodException ex )
		{
			throw new XmlRpcException( -32601, "Server Error, requested method not found: " + ex.Message );
		}

		object[] array = new object[parameters.Count];
		int num = 0;
		foreach ( object parameter in parameters )
		{
			array [ num ] = parameter;
			num++;
		}

		try
		{
			return method.Invoke( target, array ) ?? throw new XmlRpcException( -32500, "Application Error: Method returned NULL." );
		}
		catch ( XmlRpcException ex2 )
		{
			throw ex2;
		}
		catch ( ArgumentException ex3 )
		{
			Logger.WriteEntry( "Server Error, invalid method parameters: " + ex3.Message, LogLevel.Information );
			string text = methodName + "( ";
			object[] array2 = array;
			foreach ( object obj in array2 )
			{
				text += obj.GetType().Name;
				text += " ";
			}

			text += ")";
			throw new XmlRpcException( -32602, "Server Error, invalid method parameters: Arguement type mismatch invoking " + text );
		}
		catch ( TargetParameterCountException ex4 )
		{
			Logger.WriteEntry( "Server Error, invalid method parameters: " + ex4.Message, LogLevel.Information );
			throw new XmlRpcException( -32602, "Server Error, invalid method parameters: Arguement count mismatch invoking " + methodName );
		}
		catch ( TargetInvocationException ex5 )
		{
			throw new XmlRpcException( -32500, "Application Error Invoked method " + methodName + ": " + ex5.Message );
		}
	}

	[XmlRpcExposed]
	public IList ListMethods()
	{
		IList list = new ArrayList();
		foreach ( DictionaryEntry item in _server )
		{
			bool flag = XmlRpcExposedAttribute.IsExposed(item.Value.GetType());
			MemberInfo[] members = item.Value.GetType().GetMembers();
			foreach ( MemberInfo memberInfo in members )
			{
				if ( memberInfo.MemberType == MemberTypes.Method && ( (MethodInfo)memberInfo ).IsPublic && ( !flag || XmlRpcExposedAttribute.IsExposed( memberInfo ) ) )
				{
					list.Add( string.Concat( item.Key, ".", memberInfo.Name ) );
				}
			}
		}

		return list;
	}

	[XmlRpcExposed]
	public IList MethodSignature( string name )
	{
		IList list = new ArrayList();
		int num = name.IndexOf('.');
		if ( num < 0 )
		{
			return list;
		}

		string name2 = name.Substring(0, num);
		object obj = _server[name2];
		if ( obj == null )
		{
			return list;
		}

		MemberInfo[] member = obj.GetType().GetMember(name.Substring(num + 1));
		if ( member.Length != 1 )
		{
			return list;
		}

		MethodInfo methodInfo;
		try
		{
			methodInfo = (MethodInfo)member [ 0 ];
		}
		catch ( Exception ex )
		{
			Logger.WriteEntry( string.Concat( "Attempted methodSignature call on ", member [ 0 ], " caused: ", ex ), LogLevel.Information );
			return list;
		}

		if ( !methodInfo.IsPublic )
		{
			return list;
		}

		IList list2 = new ArrayList
		{
			methodInfo.ReturnType.Name
		};
		ParameterInfo[] parameters = methodInfo.GetParameters();
		foreach ( ParameterInfo parameterInfo in parameters )
		{
			list2.Add( parameterInfo.ParameterType.Name );
		}

		list.Add( list2 );
		return list;
	}

	[XmlRpcExposed]
	public string MethodHelp( string name )
	{
		string text = null;
		try
		{
			text = (string)MethodHelper[ _server.MethodName( name ) ];
		}
		catch ( XmlRpcException ex )
		{
			throw ex;
		}
		catch ( Exception )
		{
		}

		if ( text == null )
		{
			text = "No help available for: " + name;
		}

		return text;
	}

	[XmlRpcExposed]
	public IList MultiCall( IList calls )
	{
		IList list = new ArrayList();
		XmlRpcResponse xmlRpcResponse = new XmlRpcResponse();
		foreach ( IDictionary call in calls )
		{
			try
			{
				XmlRpcRequest req = new XmlRpcRequest((string)call["methodName"], (ArrayList)call["params"]);
				object value = _server.Invoke(req);
				IList list2 = new ArrayList
				{
					value
				};
				list.Add( list2 );
			}
			catch ( XmlRpcException ex )
			{
				xmlRpcResponse.SetFault( ex.FaultCode, ex.FaultString );
				list.Add( xmlRpcResponse.Value );
			}
			catch ( Exception ex2 )
			{
				xmlRpcResponse.SetFault( -32500, "Application Error: " + ex2.Message );
				list.Add( xmlRpcResponse.Value );
			}
		}

		return list;
	}
}