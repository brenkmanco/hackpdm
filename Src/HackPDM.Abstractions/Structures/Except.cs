using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;

namespace HackPDM.Abstractions.Structures;

/// <summary>Fluent argument checks that throw on failure and return the checker for chaining.</summary>
/// <example>
/// <code>
/// Except.Run().IsNull( array, nameof( array ) ).OutOfRange( array, index );
/// </code>
/// </example>
public class Except
{
	/// <summary>Throws when a value is <see langword="null"/>.</summary>
	/// <typeparam name="T">The value's type.</typeparam>
	/// <param name="value">The value to check.</param>
	/// <param name="paramName">The parameter name for the exception; "value" when omitted.</param>
	/// <returns>This checker.</returns>
	/// <exception cref="System.ArgumentNullException"><paramref name="value"/> is <see langword="null"/>.</exception>
	public Except IsNull<T>( [NotNull] T? value, [CallerArgumentExpression( nameof( value ) )] string? paramName = null ) {
		return value is null  
			? throw new ArgumentNullException( paramName ?? nameof( value ) ) 
			:  this;
	}
	/// <summary>Throws when a value is NOT <see langword="null"/> (the inverse of <see cref="IsNull"/>).</summary>
	/// <typeparam name="T">The value's type.</typeparam>
	/// <param name="value">The value to check.</param>
	/// <param name="paramName">The parameter name for the exception; "value" when omitted.</param>
	/// <returns>This checker.</returns>
	/// <exception cref="System.ArgumentNullException"><paramref name="value"/> is not <see langword="null"/> (an odd exception type for this case).</exception>
	public Except IsNotNull<T>( T? value, [CallerArgumentExpression( nameof( value ) )] string? paramName = null )
	{
		return value is not null  
			? throw new ArgumentNullException( paramName ?? nameof( value ) ) 
			:  this;
	}
	/// <summary>Throws when a collection has more elements than a limit.</summary>
	/// <typeparam name="T">The element type.</typeparam>
	/// <param name="collection">The collection; <see langword="null"/> passes.</param>
	/// <param name="capacity">The largest allowed count.</param>
	/// <returns>This checker.</returns>
	/// <exception cref="System.ArgumentOutOfRangeException">The collection has more than <paramref name="capacity"/> elements.</exception>
	public Except HasCapacity<T>( IEnumerable<T> collection, int capacity ) {
		return  collection is not null && collection.Count() > capacity 
			? throw new ArgumentOutOfRangeException( nameof( collection ), "Collection has more elements than the specified capacity." )
			:  this;
	}
	/// <summary>Throws when an index is outside an array.</summary>
	/// <typeparam name="T">The element type.</typeparam>
	/// <param name="arr">The array.</param>
	/// <param name="index">The index to check.</param>
	/// <returns>This checker.</returns>
	/// <exception cref="System.ArgumentOutOfRangeException"><paramref name="index"/> is negative or not less than the length.</exception>
	/// <exception cref="System.NullReferenceException"><paramref name="arr"/> is <see langword="null"/>.</exception>
	public Except OutOfRange<T>( T[] arr, int index)
	{
		return index < 0 || index >= arr.Length  
			? throw new ArgumentOutOfRangeException( nameof( index ), "Index is out of range." ) 
			: this;
	}

	/// <summary>Starts a chain of checks.</summary>
	/// <returns>A new checker.</returns>
	public static Except Run() => new();
	/// <summary>Starts a chain of checks bound to one argument, for use with <see cref="Except{T}.Exec"/>.</summary>
	/// <typeparam name="T">The argument's type.</typeparam>
	/// <param name="arg">The argument the actions receive.</param>
	/// <returns>A new checker holding <paramref name="arg"/>.</returns>
	public static Except<T> Run<T>( T arg ) => new( ref arg );
	/// <summary>Starts a chain of checks bound to two arguments.</summary>
	/// <typeparam name="T">The first argument's type.</typeparam>
	/// <typeparam name="T2">The second argument's type.</typeparam>
	/// <param name="arg">The first argument.</param>
	/// <param name="arg2">The second argument.</param>
	/// <returns>A new checker holding the arguments.</returns>
	public static Except<T, T2> Run<T, T2>( T arg, T2 arg2 ) => new( ref arg, ref arg2 );
	/// <summary>Starts a chain of checks bound to three arguments.</summary>
	/// <typeparam name="T">The first argument's type.</typeparam>
	/// <typeparam name="T2">The second argument's type.</typeparam>
	/// <typeparam name="T3">The third argument's type.</typeparam>
	/// <param name="arg">The first argument.</param>
	/// <param name="arg2">The second argument.</param>
	/// <param name="arg3">The third argument.</param>
	/// <returns>A new checker holding the arguments.</returns>
	public static Except<T, T2, T3> Run<T, T2, T3>( T arg, T2 arg2, T3 arg3 ) => new( ref arg, ref arg2, ref arg3 );
	/// <summary>Starts a chain of checks bound to four arguments.</summary>
	/// <typeparam name="T">The first argument's type.</typeparam>
	/// <typeparam name="T2">The second argument's type.</typeparam>
	/// <typeparam name="T3">The third argument's type.</typeparam>
	/// <typeparam name="T4">The fourth argument's type.</typeparam>
	/// <param name="arg">The first argument.</param>
	/// <param name="arg2">The second argument.</param>
	/// <param name="arg3">The third argument.</param>
	/// <param name="arg4">The fourth argument.</param>
	/// <returns>A new checker holding the arguments.</returns>
	public static Except<T, T2, T3, T4> Run<T, T2, T3, T4>( T arg, T2 arg2, T3 arg3, T4 arg4 ) => new( ref arg, ref arg2, ref arg3, ref arg4 );

	protected internal Except() { }
}
/// <summary>A checker bound to one argument.</summary>
/// <typeparam name="T">The argument's type.</typeparam>
public class Except<T> : Except
{
	protected internal T _arg;
	
	protected internal Except( ref T arg ) => _arg = arg;
	/// <summary>Runs each action on the bound argument, in order.</summary>
	/// <param name="actions">Checks that throw on failure.</param>
	/// <returns>This checker.</returns>
	public Except<T> Exec( params Action<T>[] actions )
	{
		foreach( var action in actions ) {
			action( _arg );
		}
		return this;
	}
}
/// <summary>A checker bound to two arguments.</summary>
/// <typeparam name="T1">The first argument's type.</typeparam>
/// <typeparam name="T2">The second argument's type.</typeparam>
public class Except<T1, T2> : Except<T1>
{
	protected internal T2 _arg2 { get; init; }
	protected internal Except( ref T1 arg1, ref T2 arg2 ) : base(ref arg1) 
		=> _arg2 = arg2;

	/// <summary>Runs each action on the two bound arguments, in order.</summary>
	/// <param name="actions">Checks that throw on failure, each with a pair of ints that is currently ignored.</param>
	/// <returns>This checker.</returns>
	public Except<T1, T2> Exec(params (Action<T1, T2> action, (int? arg1, int? arg2))[] actions )
	{
		foreach( var (action, (arg1, arg2)) in actions ) {
			action( _arg, _arg2 );
		}
		return this;
	}
	/// <summary>Runs each action on two arguments, then returns a checker bound to them.</summary>
	/// <param name="arg1">The first argument.</param>
	/// <param name="arg2">The second argument.</param>
	/// <param name="actions">Checks that throw on failure.</param>
	/// <returns>A new checker holding the arguments.</returns>
	public static Except<T1, T2> Exec( T1 arg1, T2 arg2, params Action<T1, T2>[] actions )
	{
		foreach( var action in actions ) {
			action( arg1, arg2 );
		}
		return new Except<T1, T2>( ref arg1, ref arg2 );
	}
}
/// <summary>A checker bound to three arguments.</summary>
/// <typeparam name="T1">The first argument's type.</typeparam>
/// <typeparam name="T2">The second argument's type.</typeparam>
/// <typeparam name="T3">The third argument's type.</typeparam>
public class Except<T1, T2, T3> : Except<T1, T2>
{
	protected internal T3 _arg3 { get; init; }
	protected internal Except( ref T1 arg1, ref T2 arg2, ref T3 arg3 ) : base(ref arg1, ref arg2) 
		=> _arg3 = arg3;

	/// <summary>Runs each action on the three bound arguments, in order.</summary>
	/// <param name="actions">Checks that throw on failure.</param>
	/// <returns>This checker.</returns>
	public Except<T1, T2, T3> Exec( params Action<T1, T2, T3>[] actions )
	{
		foreach( var action in actions ) {
			action( _arg, _arg2, _arg3 );
		}
		return this;
	}
}
/// <summary>A checker bound to four arguments.</summary>
/// <typeparam name="T1">The first argument's type.</typeparam>
/// <typeparam name="T2">The second argument's type.</typeparam>
/// <typeparam name="T3">The third argument's type.</typeparam>
/// <typeparam name="T4">The fourth argument's type.</typeparam>
public class Except<T1, T2, T3, T4> : Except<T1, T2, T3>
{
	protected internal T4 _arg4 { get; init; }
	protected internal Except( ref T1 arg1, ref T2 arg2, ref T3 arg3, ref T4 arg4 ) : base(ref arg1, ref arg2, ref arg3) 
		=> _arg4 = arg4;

	/// <summary>Runs each action on the four bound arguments, in order.</summary>
	/// <param name="actions">Checks that throw on failure.</param>
	/// <returns>This checker.</returns>
	public Except<T1, T2, T3, T4> Exec( params Action<T1, T2, T3, T4>[] actions )
	{
		foreach( var action in actions ) {
			action( _arg, _arg2, _arg3, _arg4 );
		}
		return this;
	}
}
