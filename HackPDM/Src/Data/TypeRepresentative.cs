using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

using Windows.Graphics;

namespace HackPDM.Src.Data.Numeric
{
    
    class TypeRepresentative
    {
    }
	
	public struct int2 : IVectorize2<int>
    {
		public int x { get; set; }
        public int r { get => x; set => x = value; }
        public int w { get => x; set => x = value; }

		public int y { get; set; }
        public int g { get => y; set => y = value; }
        public int h { get => y; set => y = value; }

		public int2(int x, int y)
        {
            this.x = x;
            this.y = y;
        }

        public static int2 operator +(int2 a, int2 b) => new int2(a.x + b.x, a.y + b.y);
        public static int2 operator -(int2 a, int2 b) => new int2(a.x - b.x, a.y - b.y);
        public static implicit operator int2(ValueTuple<int, int> xy) => new int2(xy.Item1, xy.Item2);
        public override string ToString() => $"int2({x}, {y})";
    }
    public struct int3 : IVectorize3<int>
    {
		public int x { get; set; }
		public int y { get; set; }
		public int z { get; set; }

		public int3(int x, int y, int z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        public static int3 operator +(int3 a, int3 b) => new int3(a.x + b.x, a.y + b.y, a.z + b.z);
        public static int3 operator -(int3 a, int3 b) => new int3(a.x - b.x, a.y - b.y, a.z - b.z);
        public override string ToString() => $"int3({x}, {y}, {z})";
    }
    public struct int4 : IVectorize4<int>
    {
        public int x { get; set; }
        public int y { get; set; }
        public int width { get; set; }
        public int height { get; set; }
        public int r { get => x; set => x = value; }
        public int g { get => y; set => y = value; }
        public int b { get => width; set => width = value; }
        public int a { get => height; set => height = value; }
        public int4(int x, int y, int width, int height)
        {
            this.x = x;
            this.y = y;
            this.width = width;
            this.height = height;
        }

        public static int4 operator +(int4 a, int4 b) => new int4(a.x + b.x, a.y + b.y, a.width + b.width, a.height + b.height);
        public static int4 operator -(int4 a, int4 b) => new int4(a.x - b.x, a.y - b.y, a.width - b.width, a.height - b.height);
        public static implicit operator RectInt32(int4 wxyz) => new RectInt32(wxyz.x, wxyz.y, wxyz.width, wxyz.height);
        public static implicit operator int4(RectInt32 wxyz) => new int4(wxyz.X, wxyz.Y, wxyz.Width, wxyz.Height);
        public override string ToString() => $"int4({height}, {x}, {y}, {width})";
    }
    public struct float2 : IVectorize2<float>
    {
		public float x { get; set; }
		public float y { get; set; }
		public int Length { get; }

        public float2(float x, float y) 
        {
            this.x = x;
            this.y = y;
        }
		public static float2 operator +(float2 a, float2 b) => new float2(a.x + b.x, a.y + b.y);
        public static float2 operator -(float2 a, float2 b) => new float2(a.x - b.x, a.y - b.y);
        public override string ToString() => $"float2({x}, {y})";
    }
    public struct float3 : IVectorize3<float>
    {
        public float3(float x, float y, float z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

		public float x { get; set; }
		public float y { get; set; }
		public float z { get; set; }

		public static float3 operator +(float3 a, float3 b) => new float3(a.x + b.x, a.y + b.y, a.z + b.z);
        public static float3 operator -(float3 a, float3 b) => new float3(a.x - b.x, a.y - b.y, a.z - b.z);
        public override string ToString() => $"float2({x}, {y})";
    }
    public struct double2 : IVectorize2<double>
    {
        public double x { get; set; }
        public double y { get; set; }
        public double r { get => x; set => x = value; }
        public double g { get => y; set => y = value; }
        public double w { get => x; set => x = value; }
        public double h { get => y; set => y = value; }
        public int Length { get; }

        public double2(double x, double y)
        {
            this.x = x;
            this.y = y;
        }
        public static double2 operator +(double2 a, double2 b) => new double2(a.x + b.x, a.y + b.y);
        public static double2 operator -(double2 a, double2 b) => new double2(a.x - b.x, a.y - b.y);
        public override string ToString() => $"double2({x}, {y})";
    }
    public struct double3 : IVectorize3<double>
    {
        public double3(double x, double y, double z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        public double x { get; set; }
        public double y { get; set; }
        public double z { get; set; }
        public double r { get => x; set => x = value; }
        public double g { get => y; set => y = value; }
        public double b { get => z; set => z = value; }

        public static double3 operator +(double3 a, double3 b) => new double3(a.x + b.x, a.y + b.y, a.z + b.z);
        public static double3 operator -(double3 a, double3 b) => new double3(a.x - b.x, a.y - b.y, a.z - b.z);
        public override string ToString() => $"double2({x}, {y})";
    }
    public struct double4 : IVectorize4<double>
    {
        public double4(double x, double y, double width, double height)
        {
            this.x = x;
            this.y = y;
            this.width = width;
            this.height = height;
        }

        public double x { get; set; }
        public double y { get; set; }
        public double width { get; set; }
        public double height { get; set; }
        public double r { get => x; set => x = value; }
        public double g { get => y; set => y = value; }
        public double b { get => width; set => width = value; }
        public double a { get => height; set => height = value; }

        public static double4 operator +(double4 a, double4 b) => new double4(a.x + b.x, a.y + b.y, a.width + b.width, a.height + b.height);
        public static double4 operator -(double4 a, double4 b) => new double4(a.x - b.x, a.y - b.y, a.width - b.width, a.height - b.height);
        public override string ToString() => $"double4({x}, {y}, {width}, {height})";
    }
    public interface IVectorize2<T>
    {
        public abstract T x { get; set; }
        public abstract T y { get; set; }
        public T this[int index]
        {
            get => index switch
            {
                0 => x,
                1 => y,
                _ => throw new InvalidOperationException(),
            };
            set => _ = index switch
            {
                0 => x = value,
                1 => y = value,
                _ => throw new InvalidOperationException(),
            };
        }
        public int Length { get => 2; }
        public virtual string? ToString() => $"({x}, {y})";
    }
    public interface IVectorize3<T>
	{
        public abstract T x { get; set; }
        public abstract T y { get; set; }
        public abstract T z { get; set; }
        public T this[int index] 
        { 
            get => index switch {
                0 => x,
                1 => y,
                2 => z,
                _ => throw new InvalidOperationException(),
            }; 
            set => _ = index switch
            {
                0 => x=value,
                1 => y=value,
                2 => z=value,
                _ => throw new InvalidOperationException(),
            };
        }
		public int Length { get => 3; }
        public virtual string? ToString() => $"({x}, {y}, {z})";
    }
    public interface IVectorize4<T>
    {
        public abstract T x { get; set; }
        public abstract T y { get; set; }
        public abstract T width { get; set; }
        public abstract T height { get; set; }
        
        public T this[int index]
        {
            get => index switch
            {
                0 => x,
                1 => y,
                2 => width,
                3 => height,
                _ => throw new InvalidOperationException(),
            };
            set => _ = index switch
            {
                0 => x = value,
                1 => y = value,
                2 => width = value,
                3 => height = value,
                _ => throw new InvalidOperationException(),
            };
        }
        public int Length { get => 4; }
        public virtual string? ToString() => $"({x}, {y}, {width}, {height})";
    }
}
