using System;
using System.Collections.Generic;
using System.Runtime.InteropServices.ComTypes;
using System.Text;

namespace HackPDM.Abstractions.Structures;

public class Except
{
	public static Except IsNull<T>( T? value, string? paramName = null )
	{
		return value is null
			? throw new ArgumentNullException( paramName ?? nameof( value ) )
			: new Except();
	}
	public Except IsNulled<T>( T? value, string? paramName = null ) {
		return value is null  
			? throw new ArgumentNullException( paramName ?? nameof( value ) ) 
			:  this;
	}

	public Except IsNotNull<T>( T? value, string? paramName = null )
	{
		return value is not null  
			? throw new ArgumentNullException( paramName ?? nameof( value ) ) 
			:  this;
	}

	public Except HasCapacity<T>( IEnumerable<T> collection, int capacity ) {
		return  collection is not null && collection.Count() > capacity 
			? throw new ArgumentOutOfRangeException( nameof( collection ), "Collection has more elements than the specified capacity." )
			:  this;
	}

	public Except OutOfRange<T>( T[] arr, int index)
	{
		return index < 0 || index >= arr.Length  
			? throw new ArgumentOutOfRangeException( nameof( index ), "Index is out of range." ) 
			: this;
	}

	public static Except Run() => new();
	public static Except<T> Run<T>( T arg ) => new( arg );
	public static Except<T, T2> Run<T, T2>( T arg, T2 arg2 ) => new( arg, arg2 );
	public static Except<T, T2, T3> Run<T, T2, T3>( T arg, T2 arg2, T3 arg3 ) => new( arg, arg2, arg3 );
	public static Except<T, T2, T3, T4> Run<T, T2, T3, T4>( T arg, T2 arg2, T3 arg3, T4 arg4 ) => new( arg, arg2, arg3, arg4 );

	protected internal Except() { }
}
public class Except<T> : Except
{
	protected internal T _arg;
	
	protected internal Except( T arg ) => _arg = arg;
	public Except<T> Exec( params Action<T>[] actions )
	{
		foreach( var action in actions ) {
			action( _arg );
		}
		return this;
	}
}
public class Except<T1, T2> : Except<T1>
{
	protected internal T2 _arg2 { get; init; }
	protected internal Except( ref T1 arg1, ref T2 arg2 ) : base(arg1) 
		=> _arg2 = arg2;

	public Except<T1, T2> Exec(params (Action<T1, T2> action, (int? arg1, int? arg2))[] actions )
	{
		foreach( var (action, (arg1, arg2)) in actions ) {
			action( _arg, _arg2 );
		}
		return this;
	}
	public static Except<T1, T2> Exec( T1 arg1, T2 arg2, params Action<T1, T2>[] actions )
	{
		foreach( var action in actions ) {
			action( arg1, arg2 );
		}
		return new Except<T1, T2>( ref arg1, ref arg2 );
	}
}
public class Except<T1, T2, T3> : Except<T1, T2>
{
	protected internal T3 _arg3 { get; init; }
	protected internal Except( ref T1 arg1, ref T2 arg2, ref T3 arg3 ) : base(ref arg1, ref arg2) 
		=> _arg3 = arg3;

	public Except<T1, T2, T3> Exec( params Action<T1, T2, T3>[] actions )
	{
		foreach( var action in actions ) {
			action( _arg, _arg2, _arg3 );
		}
		return this;
	}
}
public class Except<T1, T2, T3, T4> : Except<T1, T2, T3>
{
	protected internal T4 _arg4 { get; init; }
	protected internal Except( T1 arg1, T2 arg2, T3 arg3, T4 arg4 ) : base(arg1, arg2, arg3) 
		=> _arg4 = arg4;

	public Except<T1, T2, T3, T4> Exec( params Action<T1, T2, T3, T4>[] actions )
	{
		foreach( var action in actions ) {
			action( _arg, _arg2, _arg3, _arg4 );
		}
		return this;
	}
}
public class Helper
{
	public static Except<T2, T1> R2x1<T1, T2>( ref T1 dat1, ref T2 dat2 ) => new(ref dat2, ref dat1);
	public static Except
}
public class ExceptAdapt<T1, T2>
{
	Except<T1, T2> _except { get; init; }
	public ExceptAdapt( ref Except<T1, T2> except ) => _except=except;
}