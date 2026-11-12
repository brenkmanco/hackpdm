using System;
using System.Numerics;

namespace HackPDM.Domain.Representation
{
    
	
	/// <summary>A vector with at least two components that can also be indexed.</summary>
	/// <typeparam name="T">The component type.</typeparam>
	public interface IVectorize2<T>
    {
        public abstract T x { get; set; }
        public abstract T y { get; set; }
        /// <summary>Gets or sets a component by position (0 is <see cref="x"/>).</summary>
        /// <param name="index">The component's position, from 0 to <see cref="Length"/> - 1.</param>
        public T this[int index] { get; set; }
        /// <summary>The number of components.</summary>
        public int Length { get; }
    }
    /// <summary>A vector with at least three components.</summary>
    /// <typeparam name="T">The component type.</typeparam>
    public interface IVectorize3<T> : IVectorize2<T>
	{
        public abstract T z { get; set; }
    }
    /// <summary>A vector with four components.</summary>
    /// <typeparam name="T">The component type.</typeparam>
    public interface IVectorize4<T> : IVectorize3<T>
	{
        public abstract T w { get; set; }
    }
	/// <summary>A four-component vector, for example a rectangle as (x, y, width, height).</summary>
	/// <typeparam name="T">The component type.</typeparam>
	/// <param name="x">The first component.</param>
	/// <param name="y">The second component.</param>
	/// <param name="z">The third component.</param>
	/// <param name="w">The fourth component.</param>
	public struct Vector4<T>(T x, T y, T z, T w) : IVectorize4<T>
    {
		public T x { get; set; } = x;
		public T y { get; set; } = y;
        public T z { get; set; } = z;
        public T w { get; set; } = w;
		public readonly int Length { get => 4; }

		/// <summary>Gets or sets a component by position: 0 is x, 1 y, 2 z, 3 w.</summary>
		/// <param name="index">The component's position.</param>
		/// <exception cref="System.IndexOutOfRangeException"><paramref name="index"/> is not 0 to 3.</exception>
		public T this[int index]
		{
			readonly get => index switch
			{
				0 => x,
				1 => y,
				2 => z,
				3 => w,
				_ => throw new IndexOutOfRangeException(),
			};
			set => _ = index switch
			{
				0 => x = value,
				1 => y = value,
				2 => z = value,
				3 => w = value,
				_ => throw new IndexOutOfRangeException(),
			};
		}
		
		//public static Vector4<T> operator +(Vector4<T> a, Vector4<T> b) => new(a.x + b.x, a.y + b.y, a.z + b.z, a.w + b.w);
		//public static Vector4<T> operator -(Vector4<T> a, Vector4<T> b) => new(a.x - b.x, a.y - b.y, a.z - b.z, a.w - b.w);
		//public static implicit operator RectInt32(Vector4<T> xyzw)		=> xyzw is Vector4<int> vec ? new RectInt32(vec.x, vec.y, vec.z, vec.w) : default;

		public static implicit operator Vector4<T>( (T, T, T, T) v ) => new (v.Item1, v.Item2, v.Item3, v.Item4);

		//public static implicit operator Vector4<T>(RectInt32 wxyz)		=> new (wxyz.X, wxyz.Y, wxyz.Width, wxyz.Height);
		public override readonly string? ToString() => $"({x}, {y}, {z}, {w})";
	}
    
}
