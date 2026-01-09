namespace HackPDM.Core.General;

public class MathUtility
{
    public static int Min(params int[] values)
    {
        if (values is null || values.Length < 1) throw new ArgumentException("values is null or empty");
        int val = values[0];

        // skip first value because val is already the first value
        for (int i = 1; i < values.Length; i++)
        {
            val = val < values[i] ? val : values[i];
        }
        return val;        
    }
    public static int Max(params int[] values)
    {
        if (values is null || values.Length < 1) throw new ArgumentException("values is null or empty");
        int val = values[0];

        // skip first value because val is already the first value
        for (int i = 1; i < values.Length; i++)
        {
            val = val > values[i] ? val : values[i];
        }
        return val;
    }
    public static int MaxUpTo(int max, params int[] values)
    {
        if (values is null || values.Length < 1) throw new ArgumentException("values is null or empty");
        int val = values[0];

        // skip first value because val is already the first value
        for (int i = 1; i < values.Length; i++)
        {
            val = val > values[i] ? val : values[i];
            if (val >= max) return max;
        }
        return val > max ? max : val;
    }
    public static int MinDownTo(int min, params int[] values)
    {
        if (values is null || values.Length < 1) throw new ArgumentException("values is null or empty");
        int val = values[0];

        // skip first value because val is already the first value
        for (int i = 1; i < values.Length; i++)
        {
            val = val < values[i] ? val : values[i];
            if (val <= min) return min;
        }
        return val < min ? min : val;
    }
    public static T MinDownTo<T>(T min, params T[] values) where T : IComparable<T>, IEquatable<T>
    {
        if (values is null || values.Length < 1) throw new ArgumentException("values is null or empty");
        T val = values[0];

        // skip first value because val is already the first value
        for (int i = 1; i < values.Length; i++)
        {
            T vNext = values[i];
            int compared = val.CompareTo(vNext);
            val = compared < 0 ? val : vNext;

            if (compared <= 0) return min;
        }
        return val.CompareTo(min) < 0 ? min : val;
    }
    public static T MaxUpTo<T>(T max, params T[] values) where T : IComparable<T>, IEquatable<T>
    {
        if (values is null || values.Length < 1) throw new ArgumentException("values is null or empty");
        T val = values[0];

        // skip first value because val is already the first value
        for (int i = 1; i < values.Length; i++)
        {
            T vNext = values[i];
            int compared = val.CompareTo(vNext);
            val = compared > 0 ? val : vNext;

            if (compared >= 0) return max;
        }
        return val.CompareTo(max) > 0 ? max : val;
    }
    
	public static void If(bool condition, Action @true, Action @false)
	{
		if (condition) @true(); else @false();
	}
	public static T If<T>(bool condition, Func<T> @true, Func<T> @false)
	{
		return condition ? @true() : @false();
	}
	public static Lazy<T> LazyIf<T>(bool condition, Func<T> @true, Func<T> @false)
	{
		return new Lazy<T>(() => condition ? @true() : @false());
	}
}