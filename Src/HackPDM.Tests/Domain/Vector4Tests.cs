using System;
using Xunit;
using HackPDM.Domain.Representation;

namespace HackPDM.Tests.Domain;

public class Vector4Tests
{
    [Fact]
    public void Constructor_And_Indexers_ReadPropertiesCorrectly()
    {
        var vec = new Vector4<int>(10, 20, 300, 400);

        Assert.Equal(10, vec.x);
        Assert.Equal(20, vec.y);
        Assert.Equal(300, vec.z);
        Assert.Equal(400, vec.w);

        Assert.Equal(10, vec[0]);
        Assert.Equal(20, vec[1]);
        Assert.Equal(300, vec[2]);
        Assert.Equal(400, vec[3]);
        Assert.Equal(4, vec.Length);
    }

    [Fact]
    public void ImplicitConversion_FromTuple_AssignsCoordinates()
    {
        Vector4<int> vec = (100, 200, 800, 600);

        Assert.Equal(100, vec.x);
        Assert.Equal(200, vec.y);
        Assert.Equal(800, vec.z);
        Assert.Equal(600, vec.w);
    }

    [Fact]
    public void Indexer_OutOfRange_ThrowsIndexOutOfRangeException()
    {
        var vec = new Vector4<double>(1.0, 2.0, 3.0, 4.0);

        Assert.Throws<IndexOutOfRangeException>(() => vec[4]);
        Assert.Throws<IndexOutOfRangeException>(() => vec[-1]);
    }
}
