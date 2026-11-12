using System;
using System.Collections.Generic;
using Xunit;
using HackPDM.Abstractions;

namespace HackPDM.Tests.Structures;

public class BiMapTests
{
    [Fact]
    public void Add_And_BidirectionalIndexLookup_Succeeds()
    {
        var biMap = new BiMap<string, int>
        {
            { "Apple", 1 },
            { "Banana", 2 },
            { "Cherry", 3 }
        };

        // Forward lookup
        Assert.Equal(1, biMap["Apple"]);
        Assert.Equal(2, biMap["Banana"]);
        Assert.Equal(3, biMap["Cherry"]);

        // Backward lookup
        Assert.Equal("Apple", biMap[1]);
        Assert.Equal("Banana", biMap[2]);
        Assert.Equal("Cherry", biMap[3]);

        Assert.Equal(3, biMap.Count);
    }

    [Fact]
    public void DuplicateKey_ThrowsArgumentException()
    {
        var biMap = new BiMap<string, int>
        {
            { "UniqueKey", 10 }
        };

        Assert.Throws<ArgumentException>(() => biMap.Add("UniqueKey", 20));
    }

    [Fact]
    public void DuplicateValue_ThrowsArgumentException()
    {
        var biMap = new BiMap<string, int>
        {
            { "Key1", 100 }
        };

        Assert.Throws<ArgumentException>(() => biMap.Add("Key2", 100));
    }

    [Fact]
    public void Remove_ByKey_RemovesBothDirections()
    {
        var biMap = new BiMap<string, int>
        {
            { "ToRemove", 42 }
        };

        bool removed = biMap.Remove("ToRemove");

        Assert.True(removed);
        Assert.False(biMap.ContainsKey("ToRemove"));
        Assert.False(biMap.ContainsValue(42));
        Assert.Empty(biMap);
    }

    [Fact]
    public void Clear_RemovesAllEntries()
    {
        var biMap = new BiMap<string, int>
        {
            { "A", 1 },
            { "B", 2 }
        };

        biMap.Clear();

        Assert.Empty(biMap);
        Assert.Empty(biMap.Keys);
        Assert.Empty(biMap.Values);
    }

    [Fact]
    public void TryGetValue_ForwardLookup_WorksCorrectly()
    {
        var biMap = new BiMap<string, int>
        {
            { "Found", 999 }
        };

        Assert.True(biMap.TryGetValue("Found", out int val));
        Assert.Equal(999, val);

        Assert.False(biMap.TryGetValue("NotFound", out int missing));
        Assert.Equal(0, missing);
    }

    [Fact]
    public void Remove_ByValue_RemovesBothDirections()
    {
        var biMap = new BiMap<string, int> { { "Apple", 1 }, { "Banana", 2 } };

        Assert.True(biMap.Remove(2));

        Assert.False(biMap.ContainsKey("Banana"));
        Assert.False(biMap.ContainsKey(2));
        Assert.True(biMap.ContainsKey("Apple"));
        Assert.True(biMap.ContainsKey(1));
        Assert.False(biMap.Remove(2));
    }

    [Fact]
    public void Remove_Pair_OnlyWhenTheKeyMapsToThatValue()
    {
        var biMap = new BiMap<string, int> { { "Apple", 1 }, { "Banana", 2 } };

        Assert.False(biMap.Remove(new KeyValuePair<string, int>("Apple", 2)));
        Assert.Equal(2, biMap.Count);

        Assert.True(biMap.Remove(new KeyValuePair<int, string>(1, "Apple")));
        Assert.False(biMap.ContainsKey("Apple"));
        Assert.False(biMap.ContainsKey(1));
        Assert.True(biMap.ContainsKey("Banana"));
    }

    [Fact]
    public void Remove_ThroughNonGenericDictionary_RemovesThatKeysPair()
    {
        var biMap = new BiMap<string, int> { { "Apple", 1 }, { "Banana", 2 } };

        ((System.Collections.IDictionary)biMap).Remove("Banana");
        ((System.Collections.IDictionary)biMap).Remove(42); // not a key type: ignored

        Assert.False(biMap.ContainsKey(2));
        Assert.True(biMap.ContainsKey("Apple"));
        Assert.True(biMap.ContainsKey(1));
    }
}
