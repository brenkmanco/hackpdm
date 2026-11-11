namespace HackPDM.Infrastructure.Odoo;

/// <summary>
/// The outcome of a call: its value, or why it failed. Replaces the shared static
/// OdooClient.LatestException, which concurrent calls overwrote.
/// </summary>
public readonly record struct Result<T>( T? Value, string? Error )
{
	public bool Ok => Error is null;

	public static Result<T> Success( T value ) => new( value, null );
	public static Result<T> Fail( string error ) => new( default, string.IsNullOrEmpty( error ) ? "unknown error" : error );

	/// <summary>The value, or <paramref name="fallback"/> when the call failed (or returned nothing).</summary>
	public T ValueOr( T fallback ) => Ok && Value is not null ? Value : fallback;

	/// <summary>The value, or an exception carrying the error.</summary>
	public T Unwrap() => Ok ? Value! : throw new OdooException( Error! );

	public Result<TOut> Map<TOut>( Func<T, TOut> map ) => Ok ? Result<TOut>.Success( map( Value! ) ) : Result<TOut>.Fail( Error! );

	public override string ToString() => Ok ? $"Ok({Value})" : $"Fail({Error})";
}

/// <summary>An Odoo call failed (the message is the server's or the transport's).</summary>
public sealed class OdooException( string message ) : Exception( message );
