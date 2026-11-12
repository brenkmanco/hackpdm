using System.Diagnostics.CodeAnalysis;

namespace HackPDM.Infrastructure.Odoo;

/// <summary>
/// The outcome of a call: its value, or why it failed. Replaces the shared static
/// OdooClient.LatestException, which concurrent calls overwrote.
/// </summary>
/// <typeparam name="T">The type of the value a successful call produces.</typeparam>
/// <param name="Value">The value; meaningful only when <see cref="Ok"/> is true.</param>
/// <param name="Error">Why the call failed; null on success.</param>
public readonly record struct Result<T>( T? Value, string? Error )
{
	/// <summary>Whether the call succeeded (<see cref="Error"/> is null).</summary>
	[MemberNotNullWhen( false, nameof( Error ) )]
	public bool Ok => Error is null;

	/// <summary>Creates a successful result.</summary>
	/// <param name="value">The value.</param>
	/// <returns>A result with no error.</returns>
	public static Result<T> Success( T value ) => new( value, null );
	/// <summary>Creates a failed result.</summary>
	/// <param name="error">Why the call failed; an empty or null message becomes "unknown error" so the result is never mistaken for success.</param>
	/// <returns>A result with no value.</returns>
	public static Result<T> Fail( string error ) => new( default, string.IsNullOrEmpty( error ) ? "unknown error" : error );

	/// <summary>Returns the value, or <paramref name="fallback"/> when the call failed or returned null.</summary>
	/// <param name="fallback">The value to use instead.</param>
	/// <returns>The value or <paramref name="fallback"/>.</returns>
	public T ValueOr( T fallback ) => Ok && Value is not null ? Value : fallback;

	/// <summary>Returns the value, throwing when the call failed.</summary>
	/// <returns>The value (which can still be null for a successful call that returned nothing).</returns>
	/// <exception cref="OdooException">The call failed; the message is <see cref="Error"/>.</exception>
	public T Unwrap() => Ok ? Value! : throw new OdooException( Error );

	/// <summary>Transforms a successful value and passes a failure through unchanged.</summary>
	/// <typeparam name="TOut">The type of the transformed value.</typeparam>
	/// <param name="map">Applied to the value only when <see cref="Ok"/> is true.</param>
	/// <returns>The mapped value, or a failed result with the same error.</returns>
	public Result<TOut> Map<TOut>( Func<T, TOut> map ) => Ok ? Result<TOut>.Success( map( Value! ) ) : Result<TOut>.Fail( Error );

	public override string ToString() => Ok ? $"Ok({Value})" : $"Fail({Error})";
}

/// <summary>An Odoo call failed (the message is the server's or the transport's).</summary>
public sealed class OdooException( string message ) : Exception( message );
