using System;
using System.Numerics;

namespace HackPDM.Core.General;

public class MathUtility
{
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