using System;
using HackPDM.Core.Hack;

namespace HackPDM.Tests.Mocks;

/// <summary>
/// Decoupled builder for creating customizable mock <see cref="HackFile"/> instances.
/// </summary>
public class HackFileMockBuilder
{
    private string _name = "Part1.sldprt";
    private string? _fullPath = @"C:\Vault\Parts\Part1.sldprt";
    private string? _typeExt = ".sldprt";
    private DateTime _modifiedDate = new(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc);
    private string? _checksum = "da39a3ee5e6b4b0d3255bfef95601890afd80709";
    private long? _fileSize = 45056;
    private int? _hpVersionId = 10;
    private int? _hpEntryId = 1;
    private bool? _hasRemoteEntry = true;
    private bool? _hasRemoteVersion = true;
    private string? _basePath = @"C:\Vault";
    private string? _relativePath = @"Parts\Part1.sldprt";

    public HackFileMockBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    public HackFileMockBuilder WithFullPath(string? fullPath)
    {
        _fullPath = fullPath;
        return this;
    }

    public HackFileMockBuilder WithExtension(string? ext)
    {
        _typeExt = ext;
        return this;
    }

    public HackFileMockBuilder WithModifiedDate(DateTime date)
    {
        _modifiedDate = date;
        return this;
    }

    public HackFileMockBuilder WithChecksum(string? checksum)
    {
        _checksum = checksum;
        return this;
    }

    public HackFileMockBuilder WithFileSize(long? size)
    {
        _fileSize = size;
        return this;
    }

    public HackFileMockBuilder WithHpEntry(int? entryId, bool hasRemoteEntry = true)
    {
        _hpEntryId = entryId;
        _hasRemoteEntry = hasRemoteEntry;
        return this;
    }

    public HackFileMockBuilder WithHpVersion(int? versionId, bool hasRemoteVersion = true)
    {
        _hpVersionId = versionId;
        _hasRemoteVersion = hasRemoteVersion;
        return this;
    }

    public HackFileMockBuilder AsLocalOnly()
    {
        _hpEntryId = null;
        _hpVersionId = null;
        _hasRemoteEntry = false;
        _hasRemoteVersion = false;
        return this;
    }

    public HackFileMockBuilder AsRemoteOnly()
    {
        _fullPath = null;
        _basePath = null;
        _relativePath = null;
        _hasRemoteEntry = true;
        _hasRemoteVersion = true;
        return this;
    }

    public HackFile Build()
    {
        return new HackFile(
            name: _name,
            fullPath: _fullPath,
            typeExt: _typeExt,
            modifiedDate: _modifiedDate,
            sha1Checksum: _checksum,
            fileSize: _fileSize,
            hpVersionId: _hpVersionId,
            hpEntryId: _hpEntryId,
            hasRemoteEntry: _hasRemoteEntry,
            hasRemoteVersion: _hasRemoteVersion,
            basePath: _basePath,
            relativePath: _relativePath
        );
    }
}

/// <summary>
/// Static factory providing common decoupled mock <see cref="HackFile"/> configurations.
/// </summary>
public static class MockHackFileFactory
{
    public static HackFile CreatePart(string name = "Bracket.sldprt", int entryId = 1, int versionId = 10)
        => new HackFileMockBuilder()
            .WithName(name)
            .WithExtension(".sldprt")
            .WithFullPath($@"C:\Vault\Parts\{name}")
            .WithHpEntry(entryId)
            .WithHpVersion(versionId)
            .Build();

    public static HackFile CreateAssembly(string name = "MainAssembly.sldasm", int entryId = 2, int versionId = 20)
        => new HackFileMockBuilder()
            .WithName(name)
            .WithExtension(".sldasm")
            .WithFullPath($@"C:\Vault\Assemblies\{name}")
            .WithHpEntry(entryId)
            .WithHpVersion(versionId)
            .WithFileSize(124928)
            .Build();

    public static HackFile CreateDrawing(string name = "Bracket.slddrw", int entryId = 3, int versionId = 30)
        => new HackFileMockBuilder()
            .WithName(name)
            .WithExtension(".slddrw")
            .WithFullPath($@"C:\Vault\Drawings\{name}")
            .WithHpEntry(entryId)
            .WithHpVersion(versionId)
            .WithFileSize(86016)
            .Build();

    public static HackFile CreateLocalOnly(string name = "DraftDesign.sldprt")
        => new HackFileMockBuilder()
            .WithName(name)
            .WithExtension(".sldprt")
            .WithFullPath($@"C:\Vault\Drafts\{name}")
            .AsLocalOnly()
            .Build();

    public static HackFile CreateRemoteOnly(string name = "ArchivedPart.sldprt", int entryId = 99, int versionId = 990)
        => new HackFileMockBuilder()
            .WithName(name)
            .WithExtension(".sldprt")
            .WithHpEntry(entryId)
            .WithHpVersion(versionId)
            .AsRemoteOnly()
            .Build();
}
