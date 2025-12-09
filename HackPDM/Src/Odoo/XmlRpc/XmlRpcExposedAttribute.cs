using System;
using System.Reflection;

namespace HackPDM.Odoo.XmlRpc;

[AttributeUsage( AttributeTargets.Class | AttributeTargets.Method )]
public class XmlRpcExposedAttribute : Attribute
{
	public static bool ExposedObject( object obj )
	{
		return IsExposed( obj.GetType() );
	}

	public static bool ExposedMethod( object obj, string methodName )
	{
		Type type = obj.GetType();
		MethodInfo method = type.GetMethod(methodName);
		if ( method == null )
		{
			throw new MissingMethodException( "Method " + methodName + " not found." );
		}

		if ( !IsExposed( type ) )
		{
			return true;
		}

		return IsExposed( method );
	}

	public static bool IsExposed( MemberInfo mi )
	{
		object[] customAttributes = mi.GetCustomAttributes(inherit: true);
		for ( int i = 0; i < customAttributes.Length; i++ )
		{
			if ( ( (Attribute)customAttributes [ i ] ) is XmlRpcExposedAttribute )
			{
				return true;
			}
		}

		return false;
	}
}