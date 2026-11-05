using System;
using Xunit;
using HackPDM.Abstractions.Structures;

namespace HackPDM.Tests.Structures;

public class ExceptTests
{
    [Fact]
    public void IsNull_WithNonNullValue_ReturnsExceptInstance()
    {
        string valid = "Hello";
        var result = Except.Run().IsNull(valid, nameof(valid));

        Assert.NotNull(result);
    }

    [Fact]
    public void IsNull_WithNullValue_ThrowsArgumentNullException()
    {
        string? nullString = null;
        Assert.Throws<ArgumentNullException>(() =>
        {
            Except.Run().IsNull(nullString, nameof(nullString));
        });
    }

    [Fact]
    public void IsNotNull_WithNullValue_ReturnsExceptInstance()
    {
        string? nullString = null;
        var result = Except.Run().IsNotNull(nullString, nameof(nullString));

        Assert.NotNull(result);
    }

    [Fact]
    public void IsNotNull_WithNonNullValue_ThrowsArgumentNullException()
    {
        string valid = "Present";
        Assert.Throws<ArgumentNullException>(() =>
        {
            Except.Run().IsNotNull(valid, nameof(valid));
        });
    }

    [Fact]
    public void HasCapacity_WithinLimit_Succeeds()
    {
        int[] items = [1, 2, 3];
        var result = Except.Run().HasCapacity(items, 5);

        Assert.NotNull(result);
    }

    [Fact]
    public void HasCapacity_ExceedingLimit_ThrowsArgumentOutOfRangeException()
    {
        int[] items = [1, 2, 3, 4, 5, 6];
        Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            Except.Run().HasCapacity(items, 3);
        });
    }

    [Fact]
    public void OutOfRange_ValidIndex_Succeeds()
    {
        string[] array = ["A", "B", "C"];
        var result = Except.Run().OutOfRange(array, 1);

        Assert.NotNull(result);
    }

    [Fact]
    public void OutOfRange_InvalidIndex_ThrowsArgumentOutOfRangeException()
    {
        string[] array = ["A", "B"];
        Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            Except.Run().OutOfRange(array, 5);
        });
        Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            Except.Run().OutOfRange(array, -1);
        });
    }
}
