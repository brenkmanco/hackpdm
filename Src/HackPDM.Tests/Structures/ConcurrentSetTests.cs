using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using HackPDM.Domain.Helper;

namespace HackPDM.Tests.Structures;

public class ConcurrentSetTests
{
    [Fact]
    public void Add_And_Contains_WorksForUniqueItems()
    {
        using var set = new ConcurrentSet<string>();

        Assert.True(set.Add("Item1"));
        Assert.True(set.Add("Item2"));
        Assert.False(set.Add("Item1")); // Duplicate

        Assert.Equal(2, set.Count);
        Assert.Contains("Item1", set);
        Assert.Contains("Item2", set);
        Assert.DoesNotContain("Item3", set);
    }

    [Fact]
    public void Remove_RemovesItemFromSet()
    {
        using var set = new ConcurrentSet<int> { 1, 2, 3 };

        Assert.True(set.Remove(2));
        Assert.False(set.Remove(999));
        Assert.Equal(2, set.Count);
        Assert.DoesNotContain(2, set);
    }

    [Fact]
    public void ConcurrentAdd_ThreadSafety_DoesNotCorruptState()
    {
        using var set = new ConcurrentSet<int>();
        const int itemCount = 1000;

        Parallel.For(0, itemCount, i =>
        {
            set.Add(i);
        });

        Assert.Equal(itemCount, set.Count);
        for (int i = 0; i < itemCount; i++)
        {
            Assert.Contains(i, set);
        }
    }

    [Fact]
    public void UnionWith_CombinesCollections()
    {
        using var set = new ConcurrentSet<int> { 1, 2, 3 };
        set.UnionWith([3, 4, 5]);

        Assert.Equal(5, set.Count);
        for (int i = 1; i <= 5; i++)
        {
            Assert.Contains(i, set);
        }
    }

    [Fact]
    public void IntersectWith_RetainsOnlyCommonElements()
    {
        using var set = new ConcurrentSet<int> { 1, 2, 3, 4 };
        set.IntersectWith([2, 4, 6]);

        Assert.Equal(2, set.Count);
        Assert.Contains(2, set);
        Assert.Contains(4, set);
    }

    [Fact]
    public void ExceptWith_RemovesSpecifiedElements()
    {
        using var set = new ConcurrentSet<int> { 10, 20, 30, 40 };
        set.ExceptWith([20, 40]);

        Assert.Equal(2, set.Count);
        Assert.Contains(10, set);
        Assert.Contains(30, set);
        Assert.DoesNotContain(20, set);
        Assert.DoesNotContain(40, set);
    }
}
