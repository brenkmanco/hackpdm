using System.Numerics;
using Windows.Graphics;

namespace HackPDM.Domain.Representation
{
    
    class TypeRepresentative
    {
    }
	
	public interface IVectorize2<T>
    {
        public abstract T x { get; set; }
        public abstract T y { get; set; }
        public T this[int index] { get; set; }
        public int Length { get; }
    }
    public interface IVectorize3<T> : IVectorize2<T>
	{
        public abstract T z { get; set; }
    }
    public interface IVectorize4<T> : IVectorize3<T>
	{
        public abstract T w { get; set; }
    }
	public struct Vector2<T>(T x, T y) : IVectorize2<T>
	{
		public T x { get; set; } = x;
		public T y { get; set; } = y;
		public readonly int Length { get => 2; }

		public T this[int index]
		{
			readonly get => index switch
			{
				0 => x,
				1 => y,
				_ => throw new IndexOutOfRangeException(),
			};
			set => _ = index switch
			{
				0 => x = value,
				1 => y = value,
				_ => throw new IndexOutOfRangeException(),
			};
		}

		public override readonly string? ToString() => $"({x}, {y})";
	}
	public struct Vector3<T>(T x, T y, T z) : IVectorize3<T>
	{
		public T x { get; set; } = x;
		public T y { get; set; } = y;
		public T z { get; set; } = z;
		public readonly int Length { get => 3; }

		public T this[int index]
		{
			readonly get => index switch
			{
				0 => x,
				1 => y,
				2 => z,
				_ => throw new IndexOutOfRangeException(),
			};
			set => _ = index switch
			{
				0 => x = value,
				1 => y = value,
				2 => z = value,
				_ => throw new IndexOutOfRangeException(),
			};
		}

		public override readonly string? ToString() => $"({x}, {y}, {z})";
	}
	public struct Vector4<T>(T x, T y, T z, T w) : IVectorize4<T>
    {
		public T x { get; set; } = x;
		public T y { get; set; } = y;
        public T z { get; set; } = z;
        public T w { get; set; } = w;
		public readonly int Length { get => 4; }

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
		public static implicit operator RectInt32(Vector4<T> xyzw)		=> xyzw is Vector4<int> vec ? new RectInt32(vec.x, vec.y, vec.z, vec.w) : default;
		//public static implicit operator Vector4<T>(RectInt32 wxyz)		=> new (wxyz.X, wxyz.Y, wxyz.Width, wxyz.Height);
		public override readonly string? ToString() => $"({x}, {y}, {z}, {w})";
	}
    
}
