using System;
using System.Numerics;

namespace HackPDM.Core.General;

public class MathUtility
{
    /// <summary>Returns the smallest of several values, but not less than a floor, with a shortcut that often returns the floor.</summary>
    /// <typeparam name="T">A comparable value type such as <see cref="int"/> or <see cref="double"/>.</typeparam>
    /// <param name="min">The floor.</param>
    /// <param name="values">The values to compare; at least one.</param>
    /// <returns>
    /// <paramref name="min"/> as soon as the running minimum is less than or equal to the next value (so for two values,
    /// whenever the first is not greater than the second); otherwise the smallest value, raised to <paramref name="min"/>
    /// if it is below it. A single value is returned raised to <paramref name="min"/>.
    /// </returns>
    /// <exception cref="System.ArgumentException"><paramref name="values"/> is <see langword="null"/> or empty.</exception>
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


}