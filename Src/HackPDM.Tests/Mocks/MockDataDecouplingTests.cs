using System;
using System.Linq;
using Xunit;
using HackPDM.Tests.Mocks;
using HackPDM.Shared.GlobalData;

namespace HackPDM.Tests.Mocks;

public class MockDataDecouplingTests
{
    [Fact]
    public void HpEntryMockBuilder_ProducesIndependentInstances()
    {
        var builder = new HpEntryMockBuilder().WithId(1).WithName("Part1.sldprt");
        var entry1 = builder.Build();
        var entry2 = builder.WithId(2).WithName("Part2.sldprt").Build();

        Assert.NotSame(entry1, entry2);
        Assert.Equal(1, entry1.id);
        Assert.Equal("Part1.sldprt", entry1.name);
        Assert.Equal(2, entry2.id);
        Assert.Equal("Part2.sldprt", entry2.name);
    }

    [Fact]
    public void HpEntryMockBuilder_SetsCheckedOutStateProperly()
    {
        var entry = new HpEntryMockBuilder()
            .WithId(5)
            .WithName("Bracket.sldprt")
            .AsCheckedOut(42, "CAD Designer")
            .Build();

        Assert.NotNull(entry.checkout_user);
        Assert.Equal(42, entry.checkout_user.id);
        Assert.Equal("CAD Designer", entry.checkout_user.name);
        Assert.NotNull(entry.checkout_date);
    }

    [Fact]
    public void HpVersionMockBuilder_ConfiguresPropertiesAccurately()
    {
        var version = new HpVersionMockBuilder()
            .WithId(25)
            .WithEntryId(5)
            .WithName("Bracket.sldprt")
            .WithFileExtension("sldprt")
            .WithFileSize(81920)
            .WithChecksum("abc123hash")
            .Build();

        Assert.Equal(25, version.id);
        Assert.Equal(5, version.entry_id?.id);
        Assert.Equal("Bracket.sldprt", version.name);
        Assert.Equal("sldprt", version.file_ext);
        Assert.Equal(81920, version.file_size);
        Assert.Equal("abc123hash", version.checksum);
    }

    [Fact]
    public void HackFileMockBuilder_ConstructsLocalAndRemoteConfigurations()
    {
        var localFile = new HackFileMockBuilder()
            .WithName("LocalOnly.sldprt")
            .AsLocalOnly()
            .Build();

        var remoteFile = new HackFileMockBuilder()
            .WithName("RemoteOnly.sldprt")
            .AsRemoteOnly()
            .Build();

        Assert.False(localFile.HasRemoteEntry);
        Assert.Null(localFile.HpEntryId);

        Assert.True(remoteFile.HasRemoteEntry);
        Assert.Null(remoteFile.FullPath);
    }

    [Fact]
    public void MockStatusLogger_CapturesMessagesAndConvertsToDelegate()
    {
        var logger = new MockStatusLogger();
        Action<StatusMessage, string> callback = logger;

        callback(StatusMessage.INFO, "Starting checkout");
        callback(StatusMessage.PROCESSING, "Locking entry 42");
        callback(StatusMessage.SUCCESS, "Checkout finished");

        Assert.Equal(3, logger.Count);
        Assert.True(logger.HasStatus(StatusMessage.SUCCESS));
        Assert.True(logger.HasMessageContaining("Locking entry 42"));
        Assert.Single(logger.GetMessagesForStatus(StatusMessage.SUCCESS));

        logger.Clear();
        Assert.Equal(0, logger.Count);
    }

    [Fact]
    public void MockPdmContext_StandardAssemblyGraph_ContainsCompleteHierarchy()
    {
        var graph = MockPdmContext.CreateStandardAssemblyGraph();

        Assert.NotNull(graph.AssemblyEntry);
        Assert.Equal(10, graph.AssemblyEntry.id);
        Assert.Equal("RoboticArm.sldasm", graph.AssemblyEntry.name);

        Assert.Equal(2, graph.PartEntries.Count);
        Assert.Equal(2, graph.PartVersions.Count);
        Assert.Equal(2, graph.PartFiles.Count);
        Assert.Equal(2, graph.Relationships.Count);
        Assert.Equal(2, graph.Properties.Count);
        Assert.Single(graph.Attachments);

        // Verify parent-child IDs in relationships
        Assert.All(graph.Relationships, r => Assert.Equal(100, r.parent_id?.id));
        Assert.Contains(graph.Relationships, r => r.child_id?.id == 101);
        Assert.Contains(graph.Relationships, r => r.child_id?.id == 102);
    }
}
