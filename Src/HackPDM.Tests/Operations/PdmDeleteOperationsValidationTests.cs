using System;
using System.Collections;
using System.Threading.Tasks;
using Xunit;
using HackPDM.Tests.Mocks;

namespace HackPDM.Tests.Operations;

public class DeleteOperationsValidationTests
{
    [Fact]
    public async Task PermanentDeleteVersionProperty_NullOrEmptyIds_ReturnsFalseImmediately()
    {
        var logger = new MockStatusLogger();

        bool resultNull = await DeleteOperations.PermanentDeleteVersionProperty(null!, logger);
        bool resultEmpty = await DeleteOperations.PermanentDeleteVersionProperty([], logger);

        Assert.False(resultNull);
        Assert.False(resultEmpty);
        Assert.Equal(0, logger.Count);
    }

    [Fact]
    public async Task PermanentDeleteVersionRelationships_NullOrEmptyIds_ReturnsFalseImmediately()
    {
        var logger = new MockStatusLogger();

        bool resultNull = await DeleteOperations.PermanentDeleteVersionRelationships(null!, logger);
        bool resultEmpty = await DeleteOperations.PermanentDeleteVersionRelationships([], logger);

        Assert.False(resultNull);
        Assert.False(resultEmpty);
        Assert.Equal(0, logger.Count);
    }

    [Fact]
    public async Task PermanentDeleteVersions_NullOrEmptyIds_ReturnsFalseImmediately()
    {
        var logger = new MockStatusLogger();

        bool resultNull = await DeleteOperations.PermanentDeleteVersions(null!, logger);
        bool resultEmpty = await DeleteOperations.PermanentDeleteVersions([], logger);

        Assert.False(resultNull);
        Assert.False(resultEmpty);
        Assert.Equal(0, logger.Count);
    }

    [Fact]
    public async Task PermanentDeleteEntry_NullOrEmptyIds_ReturnsFalseImmediately()
    {
        var logger = new MockStatusLogger();

        bool resultNull = await DeleteOperations.PermanentDeleteEntry(null!, logger);
        bool resultEmpty = await DeleteOperations.PermanentDeleteEntry([], logger);

        Assert.False(resultNull);
        Assert.False(resultEmpty);
        Assert.Equal(0, logger.Count);
    }

    [Fact]
    public async Task LogicalDeleteEntries_NullOrEmptyArray_ReturnsFalseImmediately()
    {
        var logger = new MockStatusLogger();

        bool resultNull = await DeleteOperations.SetDeletedAsync(null, true, logger);
        bool resultEmpty = await DeleteOperations.SetDeletedAsync([], true, logger);

        Assert.False(resultNull);
        Assert.False(resultEmpty);
        Assert.Equal(0, logger.Count);
    }

    [Fact]
    public async Task LogicalUnDeleteEntries_NullOrEmptyArray_ReturnsFalseImmediately()
    {
        var logger = new MockStatusLogger();

        bool resultNull = await DeleteOperations.SetDeletedAsync(null, false, logger);
        bool resultEmpty = await DeleteOperations.SetDeletedAsync([], false, logger);

        Assert.False(resultNull);
        Assert.False(resultEmpty);
        Assert.Equal(0, logger.Count);
    }
}
