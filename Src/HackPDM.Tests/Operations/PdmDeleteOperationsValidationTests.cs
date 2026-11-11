using System;
using System.Collections;
using System.Threading.Tasks;
using Xunit;
using HackPDM.Infrastructure.Operations;
using HackPDM.Infrastructure.Odoo.Models;
using HackPDM.Shared.GlobalData;
using HackPDM.Tests.Mocks;

namespace HackPDM.Tests.Operations;

public class PdmDeleteOperationsValidationTests
{
    [Fact]
    public async Task PermanentDeleteVersionProperty_NullOrEmptyIds_ReturnsFalseImmediately()
    {
        var logger = new MockStatusLogger();

        bool resultNull = await PdmDeleteOperations.PermanentDeleteVersionProperty(null!, logger);
        bool resultEmpty = await PdmDeleteOperations.PermanentDeleteVersionProperty([], logger);

        Assert.False(resultNull);
        Assert.False(resultEmpty);
        Assert.Equal(0, logger.Count);
    }

    [Fact]
    public async Task PermanentDeleteVersionRelationships_NullOrEmptyIds_ReturnsFalseImmediately()
    {
        var logger = new MockStatusLogger();

        bool resultNull = await PdmDeleteOperations.PermanentDeleteVersionRelationships(null!, logger);
        bool resultEmpty = await PdmDeleteOperations.PermanentDeleteVersionRelationships([], logger);

        Assert.False(resultNull);
        Assert.False(resultEmpty);
        Assert.Equal(0, logger.Count);
    }

    [Fact]
    public async Task PermanentDeleteVersions_NullOrEmptyIds_ReturnsFalseImmediately()
    {
        var logger = new MockStatusLogger();

        bool resultNull = await PdmDeleteOperations.PermanentDeleteVersions(null!, logger);
        bool resultEmpty = await PdmDeleteOperations.PermanentDeleteVersions([], logger);

        Assert.False(resultNull);
        Assert.False(resultEmpty);
        Assert.Equal(0, logger.Count);
    }

    [Fact]
    public async Task PermanentDeleteEntry_NullOrEmptyIds_ReturnsFalseImmediately()
    {
        var logger = new MockStatusLogger();

        bool resultNull = await PdmDeleteOperations.PermanentDeleteEntry(null!, logger);
        bool resultEmpty = await PdmDeleteOperations.PermanentDeleteEntry([], logger);

        Assert.False(resultNull);
        Assert.False(resultEmpty);
        Assert.Equal(0, logger.Count);
    }

    [Fact]
    public async Task LogicalDeleteEntries_NullOrEmptyArray_ReturnsFalseImmediately()
    {
        var logger = new MockStatusLogger();

        bool resultNull = await PdmDeleteOperations.LogicalDeleteEntries(null, logger);
        bool resultEmpty = await PdmDeleteOperations.LogicalDeleteEntries([], logger);

        Assert.False(resultNull);
        Assert.False(resultEmpty);
        Assert.Equal(0, logger.Count);
    }

    [Fact]
    public async Task LogicalUnDeleteEntries_NullOrEmptyArray_ReturnsFalseImmediately()
    {
        var logger = new MockStatusLogger();

        bool resultNull = await PdmDeleteOperations.LogicalUnDeleteEntries(null, logger);
        bool resultEmpty = await PdmDeleteOperations.LogicalUnDeleteEntries([], logger);

        Assert.False(resultNull);
        Assert.False(resultEmpty);
        Assert.Equal(0, logger.Count);
    }
}
