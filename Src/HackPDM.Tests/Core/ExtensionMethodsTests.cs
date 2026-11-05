using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using HackPDM.Core.General;
using HackPDM.Domain.Helper;

namespace HackPDM.Tests.Core;

public class ExtensionMethodsTests
{
    [Fact]
    public void AddAll_AppendsAllElementsToHashSet()
    {
        var set = new HashSet<string> { "Alpha" };
        var result = set.AddAll(["Beta", "Gamma", "Alpha"]);

        Assert.Same(set, result);
        Assert.Equal(3, set.Count);
        Assert.Contains("Beta", set);
        Assert.Contains("Gamma", set);
    }

    [Fact]
    public void TakeAndRemove_ExtractsMatchingItemsAndMutatesSource()
    {
        var numbers = new List<int> { 1, 2, 3, 4, 5, 6, 7, 8 };
        var even = numbers.TakeAndRemove(n => n % 2 == 0);

        Assert.Equal([2, 4, 6, 8], even);
        Assert.Equal([1, 3, 5, 7], numbers);
    }

    [Fact]
    public void NullableCompare_OrdersNullableIntsCorrectly()
    {
        int? nullVal = null;
        int? five = 5;
        int? ten = 10;
        int? anotherFive = 5;

        Assert.True(five.Compare(ten) < 0);
        Assert.True(ten.Compare(five) > 0);
        Assert.Equal(0, five.Compare(anotherFive));
        Assert.True(nullVal.Compare(five) < 0);
        Assert.True(five.Compare(nullVal) > 0);
    }

    [Fact]
    public void ToArrayList_ConvertsEnumerableToArrayList()
    {
        var list = new List<string> { "One", "Two", "Three" };
        ArrayList arrayList = list.ToArrayList();

        Assert.Equal(3, arrayList.Count);
        Assert.Equal("One", arrayList[0]);
        Assert.Equal("Two", arrayList[1]);
        Assert.Equal("Three", arrayList[2]);
    }

    [Fact]
    public void ToConcurrentSet_ConvertsEnumerableToConcurrentSet()
    {
        var numbers = new[] { 10, 20, 30, 20, 10 };
        using ConcurrentSet<int> set = numbers.ToConcurrentSet();

        Assert.Equal(3, set.Count);
        Assert.Contains(10, set);
        Assert.Contains(20, set);
        Assert.Contains(30, set);
    }
}
