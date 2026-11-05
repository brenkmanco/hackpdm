using System;
using System.Collections.Generic;
using HackPDM.Core.Hack;
using HackPDM.Infrastructure.Odoo.Models;

namespace HackPDM.Tests.Mocks;

/// <summary>
/// A decoupled model graph representing a complete CAD assembly scenario for testing.
/// Contains an assembly entry, multiple part entries, versions, properties, relationships, and files.
/// </summary>
public class MockPdmAssemblyGraph
{
    public HpEntry AssemblyEntry { get; set; } = null!;
    public HpVersion AssemblyVersion { get; set; } = null!;
    public HackFile AssemblyFile { get; set; } = null!;

    public List<HpEntry> PartEntries { get; set; } = [];
    public List<HpVersion> PartVersions { get; set; } = [];
    public List<HackFile> PartFiles { get; set; } = [];

    public List<HpVersionRelationship> Relationships { get; set; } = [];
    public List<HpVersionProperty> Properties { get; set; } = [];
    public List<IrAttachment> Attachments { get; set; } = [];
}

/// <summary>
/// Factory creating pre-built decoupled mock data contexts for integration and unit testing.
/// </summary>
public static class MockPdmContext
{
    /// <summary>
    /// Creates a complete mock CAD assembly with 1 assembly, 2 child parts, version relationships, properties, and attachments.
    /// All instances are isolated and decoupled.
    /// </summary>
    public static MockPdmAssemblyGraph CreateStandardAssemblyGraph()
    {
        var graph = new MockPdmAssemblyGraph();

        // 1. Assembly
        graph.AssemblyEntry = new HpEntryMockBuilder()
            .WithId(10)
            .WithName("RoboticArm.sldasm")
            .WithLatestVersion(100, "v1")
            .Build();

        graph.AssemblyVersion = new HpVersionMockBuilder()
            .WithId(100)
            .WithEntryId(10)
            .WithName("RoboticArm.sldasm")
            .WithFileExtension("sldasm")
            .WithFileSize(524288)
            .Build();

        graph.AssemblyFile = MockHackFileFactory.CreateAssembly("RoboticArm.sldasm", entryId: 10, versionId: 100);

        // 2. Child Part 1: Base
        var baseEntry = new HpEntryMockBuilder()
            .WithId(11)
            .WithName("ArmBase.sldprt")
            .WithLatestVersion(101, "v1")
            .Build();

        var baseVersion = new HpVersionMockBuilder()
            .WithId(101)
            .WithEntryId(11)
            .WithName("ArmBase.sldprt")
            .WithFileExtension("sldprt")
            .WithFileSize(131072)
            .Build();

        var baseFile = MockHackFileFactory.CreatePart("ArmBase.sldprt", entryId: 11, versionId: 101);

        // 3. Child Part 2: Gripper
        var gripperEntry = new HpEntryMockBuilder()
            .WithId(12)
            .WithName("Gripper.sldprt")
            .WithLatestVersion(102, "v1")
            .Build();

        var gripperVersion = new HpVersionMockBuilder()
            .WithId(102)
            .WithEntryId(12)
            .WithName("Gripper.sldprt")
            .WithFileExtension("sldprt")
            .WithFileSize(65536)
            .Build();

        var gripperFile = MockHackFileFactory.CreatePart("Gripper.sldprt", entryId: 12, versionId: 102);

        graph.PartEntries.AddRange([baseEntry, gripperEntry]);
        graph.PartVersions.AddRange([baseVersion, gripperVersion]);
        graph.PartFiles.AddRange([baseFile, gripperFile]);

        // 4. Relationships (Assembly -> Base, Assembly -> Gripper)
        graph.Relationships.Add(new HpVersionRelationshipMockBuilder()
            .WithId(301)
            .WithParentId(100)
            .WithChildId(101)
            .Build());

        graph.Relationships.Add(new HpVersionRelationshipMockBuilder()
            .WithId(302)
            .WithParentId(100)
            .WithChildId(102)
            .Build());

        // 5. Properties
        graph.Properties.Add(new HpVersionPropertyMockBuilder()
            .WithId(401)
            .WithVersionId(101)
            .WithName("Material")
            .WithTextValue("Cast Iron")
            .Build());

        graph.Properties.Add(new HpVersionPropertyMockBuilder()
            .WithId(402)
            .WithVersionId(102)
            .WithName("Material")
            .WithTextValue("Titanium Grade 5")
            .Build());

        // 6. Attachments
        graph.Attachments.Add(new IrAttachmentMockBuilder()
            .WithId(501)
            .WithResId(100)
            .WithName("RoboticArm.sldasm")
            .WithFileSize(524288)
            .Build());

        return graph;
    }
}
