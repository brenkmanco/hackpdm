using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using HackPDM.Domain.Hack;
using HackPDM.Domain.OdooModels.Models;
using HackPDM.Domain.Representation;
using HackPDM.Shared.GlobalData;

namespace HackPDM.Core.Hack;

/// <summary>
/// A file of the vault as the client sees it: its local path and disk metadata (date, size, checksum) together
/// with the ids of the hp.entry / hp.version it belongs to on the server.
/// </summary>
/// <remarks>Two instances are equal when they point at the same full path; see <see cref="Equals(object?)"/>.</remarks>
public class HackFile : HackBaseFile
{
    // file settings
    public string? TypeExt 
    {
        get => field ??= Info?.Extension;
        set
        {
            field = value ?? Info?.Extension;
        }
    }
    /// <summary>The file's last-modified time, normally in UTC.</summary>
    /// <value>
    /// When <see cref="OverwriteDate"/> is set, every assignment stores that instead. The first assignment of
    /// <c>default</c> takes the date from disk.
    /// </value>
    /// <remarks>
    /// Side effect: once a date is set, assigning again while the file exists also writes the value to the file's
    /// last-write time on disk.
    /// </remarks>
    public DateTime ModifiedDate 
    {
        get;
        set
        {
            if (OverwriteDate != default)
            {
                field = OverwriteDate;
                return;
            }
            if (field == default)
            {
	            // FileInfo reports 1601-01-01 for a missing file, so only a file on disk has a date
	            field = value == default
		            ? Info is { Exists: true } info ? info.LastWriteTime.ToUniversalTime() : default
					: value;
            }
            else
            {
                if (Exists is true)
                {
                    Info?.LastWriteTime = value;
                    field = value == default
	                    ? Info?.LastWriteTime.ToUniversalTime() ?? default
	                    : value;
                }
                else
                {
                    field = value;
                }
            }
        }
    }

    /// <summary>
    /// A date that replaces <see cref="ModifiedDate"/> and is written to the local file after a download
    /// (the server version's date); <c>default</c> when not used.
    /// </summary>
    public DateTime OverwriteDate { get; set; } = default;
	
    public string? Checksum { get; set; }
    public long? FileSize 
    {
        // null while the file isn't on disk (FileInfo.Length throws for a missing file)
        get => field ??= (Info is { Exists: true } info
			? info.Length
			: FullPath is null || (Info = new FileInfo(FullPath)) is not { Exists: true } found
				? null
				: found.Length);
        set => field = value ?? (Info is { Exists: true } info ? info.Length : null);
    }
        
    // odoo settings
	public int? HpEntryId { get; set; }
	public bool? HasRemoteEntry { get; set; }
    public int? HpVersionId { get; set; }
    public bool? HasRemoteVersion { get; set; }
    public bool? Owner { get; set; }

    public bool? Exists
    {
        get
        {
            field ??= Info?.Exists;
            if (field is not null) return field;
            if (FullPath is not null) field ??= Path.Exists(FullPath);

            return field;
        }
    }


    /// <summary>Creates an empty file description with no path.</summary>
    public HackFile() {}
    /// <summary>Creates a copy of another file description.</summary>
    /// <param name="hack">The file to copy.</param>
    /// <remarks>
    /// Copies the path, names, date, checksum, size, contents, <see cref="HpVersionId"/> and
    /// <see cref="HasRemoteVersion"/>, but not <see cref="HpEntryId"/>, <see cref="HasRemoteEntry"/>,
    /// <see cref="Owner"/> or <see cref="OverwriteDate"/>.
    /// </remarks>
    public HackFile(HackFile hack)
    {
        AssignToSelf( hack );
    }
    /// <summary>Creates a file description from known values, for example from server data.</summary>
    /// <param name="name">The file name with extension.</param>
    /// <param name="fullPath">The local absolute path; when the file exists there, its <see cref="System.IO.FileInfo"/> is attached.</param>
    /// <param name="typeExt">The extension (with the dot); taken from the file on disk when <see langword="null"/>.</param>
    /// <param name="modifiedDate">The modified date; <c>default</c> takes it from the file on disk.</param>
    /// <param name="sha1Checksum">The SHA-1 checksum in lowercase hex.</param>
    /// <param name="fileSize">The size in bytes; taken from the file on disk when <see langword="null"/>.</param>
    /// <param name="hpVersionId">The hp.version id.</param>
    /// <param name="hpEntryId">The hp.entry id.</param>
    /// <param name="hasRemoteEntry">Whether the entry exists on the server.</param>
    /// <param name="hasRemoteVersion">Whether a version exists on the server.</param>
    /// <param name="basePath">The folder, stored as <see cref="HackBaseFile.DirectoryName"/>.</param>
    /// <param name="relativePath">The path relative to the working folder, stored as <see cref="HackBaseFile.RelativePath"/>.</param>
    /// <remarks>No checksum is computed.</remarks>
    public HackFile(
        string name,
        string? fullPath=null,
        string? typeExt=null, 
        DateTime modifiedDate=default, 
        string? sha1Checksum=null,
        long? fileSize=null,
        int? hpVersionId=null, 
		int? hpEntryId=null,
		bool? hasRemoteEntry=null,
        bool? hasRemoteVersion=null,
        string? basePath=null,
        string? relativePath=null)
    {
        if (fullPath is not null and not "")
        {
            FileInfo file = new(fullPath);
            if (file.Exists)
            {
                this.Info = file;
            }
        }
        // base class
        this.FullPath = fullPath;
        this.Name = name;
        this.DirectoryName = basePath;
        this.RelativePath = relativePath;

        // this class
        this.TypeExt = typeExt;
        this.ModifiedDate = modifiedDate;
        this.Checksum = sha1Checksum;
        this.HpVersionId = hpVersionId;
		this.HpEntryId = hpEntryId;
		this.HasRemoteEntry = hasRemoteEntry;
        this.HasRemoteVersion = hasRemoteVersion;
        this.FileSize = fileSize;
    }
    /// <summary>Creates a file description from a file on disk, including its SHA-1 checksum.</summary>
    /// <param name="file">The file; <see langword="null"/> gives an empty description.</param>
    /// <remarks>Reads and hashes the whole file when it exists.</remarks>
    public HackFile(FileInfo? file) : this(file, computeChecksum: true) {}
    /// <summary>Creates a file description from a file on disk.</summary>
    /// <param name="file">
    /// The file; <see langword="null"/> gives an empty description. A file that doesn't exist yet gets its name,
    /// folder, path and extension but no date, size or checksum.
    /// </param>
    /// <param name="computeChecksum">
    /// false skips reading and hashing the whole file; <see cref="GetChecksum"/> computes it on demand.
    /// </param>
    public HackFile(FileInfo? file, bool computeChecksum)
    {
		if (file is null)
		{
			NullSelf();
			return;
		}
        Info = file;
        Name = file.Name;
        DirectoryName = file.DirectoryName;
        FullPath = file.FullName;
        TypeExt = file.Extension;
        // a file that isn't on disk yet (about to be downloaded, say) has no date, size or checksum
        if (!file.Exists) return;
        ModifiedDate = file.LastWriteTime.ToUniversalTime();
        FileSize = file.Length;
        if (computeChecksum)
            Checksum = Checksums.OfFile( file.FullName );
    }
	/// <summary>Creates a file description from a row of the file list.</summary>
	/// <param name="entry">
	/// The row; when its <see cref="HackPDM.Domain.Representation.EntryRow.LocalFile"/> is <see langword="null"/> the
	/// result is empty.
	/// </param>
	/// <remarks>
	/// The path comes from <see cref="HackPDM.Domain.Representation.EntryRow.FullName"/> (made absolute with
	/// <see cref="PwaPaths.ToAbsolute"/> unless the row is local-only); name, size, date and extension come from disk
	/// when the file exists, otherwise from the row. The entry and latest-version ids are copied; no checksum is computed.
	/// </remarks>
	public HackFile(EntryRow entry)
	{
		if (entry.LocalFile is not { }) { NullSelf(); return; }
		InitializeFromEntryRow(entry, true);
	}
    /// <summary>Creates a file description for a local path, hashing the file.</summary>
    /// <param name="fullPath">The file's absolute path; <see langword="null"/> gives an empty description.</param>
    /// <remarks>
    /// Same as <see cref="GetFromPath"/> with the defaults: reads date and size, computes the SHA-1 checksum, and sets
    /// <see cref="HackBaseFile.RelativePath"/> to the folder below the root (for example <c>root/a/b</c>) when the file exists.
    /// </remarks>
    public HackFile(string? fullPath) => AssignToSelf( GetFromPath( fullPath ) );
	internal void InitializeFromEntryRow(EntryRow entry, bool initFileInfo=false)
	{
		this.FullPath = entry.ReprType is EntryReprType.Local
			? entry.FullName
			: PwaPaths.ToAbsolute( entry.FullName );
		
		if (initFileInfo)
		{
			try
			{
				if (string.IsNullOrEmpty(this.FullPath))
				{
					throw new Exception();
				}

				this.Info = new(this.FullPath);
				if (!this.Info.Exists) this.Info = null;
			}
			catch { }
		}
		this.Name = this.Info?.Name ?? entry.Name;
		this.DirectoryName = this.Info?.DirectoryName ?? Path.GetDirectoryName(FullPath);
		this.FileSize = this.Info?.Length ?? entry.Size;
		this.ModifiedDate = this.Info?.LastWriteTime.ToUniversalTime() ?? entry.LocalDate ?? entry.RemoteDate ?? default;
		this.TypeExt = this.Info?.Extension ?? entry.Type;
		this.HpVersionId = entry.LatestId;
		this.HpEntryId = entry.Id;
		this.HasRemoteEntry = entry.Id is not (null or 0);
		this.HasRemoteVersion = entry.LatestId is not (null or 0);
	}
    internal void ApplyModifiedDateToLocal()
    {
        if (OverwriteDate == default || FullPath is not { } fullPath) return;
        Info ??= new(fullPath);
        if (Exists is true)
        {
            try
            {
                Info?.LastWriteTime = OverwriteDate;
            }
            catch { }
        }
    }
    internal void ApplyNonOwnerReadOnly()
    {
		if (FullPath is not null) Info ??= new(FullPath);
        if (Owner is true) return;
        if (Exists is true)
        {
            try
            {
                Info?.Attributes |= FileAttributes.ReadOnly;
            }
            catch { }
        }
    }
    /// <summary>Sets the date to give the local file when it is written (see <see cref="OverwriteDate"/>).</summary>
    /// <param name="date">The date, normally the server version's modified time.</param>
    /// <remarks>Only stores the date; it takes effect on the next <see cref="ModifiedDate"/> assignment or file write.</remarks>
    public void SetModifiedDate(DateTime date)
    {
	    OverwriteDate = date;
    }
	private void NullSelf()
	{
		Info = null;
		Name = null;
		DirectoryName = null;
		FullPath = null;
		TypeExt = null;
		ModifiedDate = default;
		FileSize = null;
		Checksum = null;
	}
    /// <summary>Gets the file's SHA-1 checksum, computing and caching it on first use.</summary>
    /// <returns>The checksum in lowercase hex, "" for an empty file, or <see langword="null"/> when the file can't be read.</returns>
    /// <remarks>A cached value is not refreshed when the file changes on disk.</remarks>
    public string? GetChecksum() => Checksum ??= Checksums.OfFile( FullPath ) ;
	private void AssignToSelf(HackFile? hack)
    {
        this.Info = hack?.Info;
        this.FullPath = hack?.FullPath;
        this.Name = hack?.Name;
        this.DirectoryName = hack?.DirectoryName;
        this.RelativePath = hack?.RelativePath;
        this.TypeExt = hack?.TypeExt;
        this.ModifiedDate = hack?.ModifiedDate ?? default;
        this.Checksum = hack?.Checksum;
        this.HpVersionId = hack?.HpVersionId;
        this.HasRemoteVersion = hack?.HasRemoteVersion;
        this.FileContents = hack?.FileContents;
        this.FileSize = hack?.FileSize;
    }
    /// <summary>
    /// The one factory for a file on disk: name, folder, extension, date and size; the checksum
    /// unless <paramref name="computeChecksum"/> is false; the folder relative to the working folder.
    /// Without <paramref name="initWithFileInfo"/> nothing is read from disk but the date.
    /// </summary>
    /// <param name="path">The file's path; <see langword="null"/> returns <see langword="null"/>.</param>
    /// <param name="directory">With <paramref name="initWithFileInfo"/> false, the folder to record instead of the path's own; otherwise ignored.</param>
    /// <param name="initWithFileInfo">Whether to read the file's metadata through a <see cref="System.IO.FileInfo"/>.</param>
    /// <param name="computeChecksum">Whether to hash the file (only with <paramref name="initWithFileInfo"/>).</param>
    /// <returns>
    /// The file description. When <paramref name="initWithFileInfo"/> is true and the file doesn't exist, it is
    /// returned without <see cref="HackBaseFile.RelativePath"/> being set.
    /// </returns>
    /// <remarks>
    /// <see cref="HackBaseFile.RelativePath"/> is set to <see cref="PwaPaths.Root"/> joined with the folders below the
    /// working folder using this OS's separator (just the root for a file outside the working folder).
    /// </remarks>
	public static HackFile? GetFromPath(string? path, string? directory = null, bool initWithFileInfo=true, bool computeChecksum=true)
    {
		if (path is null) return null;
        HackFile? hack;
		if (initWithFileInfo)
		{
			FileInfo file = new(path);
			hack = new(file, computeChecksum);
			if (!file.Exists) return hack;
		}
		else
		{
			hack = GetHackFromPathUninitialized(path, directory);
		}

        hack?.RelativePath = Path.Combine([PwaPaths.Root, .. PwaPaths.LocalSegments(hack?.DirectoryName) ?? []]);
        return hack;
    }
    private static HackFile? GetHackFromPathUninitialized(string? path, string? directory=null)
	{
		HackFile hack = new()
		{
			Name = Path.GetFileName(path),
			FullPath = Path.GetFullPath(path ?? ""),
			DirectoryName = directory ?? Path.GetDirectoryName(path),
			TypeExt = Path.GetExtension(path),
			ModifiedDate = File.GetLastWriteTime(path ?? "").ToUniversalTime(),
		};
		return hack;
	}
    
    
    // a HackFile stands for a file on disk: two are equal when they point at the same path
    private static readonly StringComparer PathComparer = OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;

    private string? IdentityPath
    {
        get
        {
            string? path = FullPath is { Length: > 0 } full
                ? full
                : DirectoryName is { Length: > 0 } dir && Name is { Length: > 0 } name ? Path.Combine(dir, name) : null;
            if (path is null) return null;
            try { return Path.GetFullPath(path); }
            catch { return path; }
        }
    }

    /// <summary>
    /// Same file path (version ids and checksums aren't identity: identical copies at two paths are
    /// two files). Consistent with <see cref="GetHashCode"/>, so hash sets of files de-duplicate.
    /// </summary>
    public override bool Equals( object? obj )
    {
        if ( ReferenceEquals( this, obj ) ) return true;
        if ( obj is not HackFile other ) return false;
        string? mine = IdentityPath, theirs = other.IdentityPath;
        return mine is not null && theirs is not null && PathComparer.Equals( mine, theirs );
    }
    public override int GetHashCode() => IdentityPath is { } path ? PathComparer.GetHashCode( path ) : 0;
    public static implicit operator HackFile( FileInfo file)
	{
		return new HackFile( file );
	}
}
