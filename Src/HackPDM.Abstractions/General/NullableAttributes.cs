#if NETSTANDARD2_0
// netstandard2.0 has no nullable-analysis attributes. The compiler recognises them by name, so these internal copies give
// this project the same flow analysis as the net10.0 projects. Each assembly keeps its own copy; nothing here is public.

namespace System.Diagnostics.CodeAnalysis
{
	/// <summary>The output is not null when the method returns <see cref="ReturnValue"/>, even if its type allows null.</summary>
	[AttributeUsage( AttributeTargets.Parameter )]
	internal sealed class NotNullWhenAttribute( bool returnValue ) : Attribute
	{
		public bool ReturnValue { get; } = returnValue;
	}

	/// <summary>The output may be null when the method returns <see cref="ReturnValue"/>, even if its type does not allow it.</summary>
	[AttributeUsage( AttributeTargets.Parameter )]
	internal sealed class MaybeNullWhenAttribute( bool returnValue ) : Attribute
	{
		public bool ReturnValue { get; } = returnValue;
	}

	/// <summary>The value is not null when the method returns (it throws otherwise), even if its type allows null.</summary>
	[AttributeUsage( AttributeTargets.Field | AttributeTargets.Parameter | AttributeTargets.Property | AttributeTargets.ReturnValue )]
	internal sealed class NotNullAttribute : Attribute { }
}

namespace System.Runtime.CompilerServices
{
	/// <summary>The compiler fills the parameter with the source text of another argument.</summary>
	[AttributeUsage( AttributeTargets.Parameter )]
	internal sealed class CallerArgumentExpressionAttribute( string parameterName ) : Attribute
	{
		public string ParameterName { get; } = parameterName;
	}
}
#endif
