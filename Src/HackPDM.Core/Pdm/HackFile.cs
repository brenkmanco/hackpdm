using HackPDM.Core.Configuration;
using HackPDM.Core.Services;

namespace HackPDM.Core.Pdm;

/// <summary>Represents a local/vault file along with disk metadata and optional server ids.</summary>
public sealed class HackFile
{
	private static readonly StringComparer PathComparer = OperatingSystem.IsWindows()
		? StringComparer.OrdinalIgnoreCase
		: StringComparer.Ordinal;

	public string? Name { get; set; }
	public string? FullPath { get; set; }
	public string? DirectoryName { get; set; }
	public string? RelativePath
	{
		get => field ??= FullPath is null || string.IsNullOrEmpty( HackDefaults.Instance.PwaPathAbsolute )
			? null
			: Path.GetRelativePath( HackDefaults.Instance.PwaPathAbsolute, FullPath );
		set;
	}
	public byte[]? FileContents { get; set; }
	public FileInfo? Info { get; set; }

	public string? TypeExt
	{
		get => field ??= Info?.Extension;
		set => field = value ?? Info?.Extension;
	}

	public DateTime ModifiedDate
	{
		get;
		set
		{
			if( OverwriteDate != default )
			{
				field = OverwriteDate;
				return;
			}
			if( field == default )
			{
				field = value == default
					? ( Info is { Exists: true } info ? info.LastWriteTime.ToUniversalTime() : default )
					: value;
			}
			else if( Exists is true )
			{
				Info?.LastWriteTime = value;
				field = value == default ? ( Info?.LastWriteTime.ToUniversalTime() ?? default ) : value;
			}
			else
			{
				field = value;
			}
		}
	}

	public DateTime OverwriteDate { get; set; }
	public string? Checksum { get; set; }

	public long? FileSize
	{
		get => field ??= Info is { Exists: true } info
			? info.Length
			: FullPath is null || ( Info = new FileInfo( FullPath ) ) is not { Exists: true } found
				? null
				: found.Length;
		set => field = value ?? ( Info is { Exists: true } info ? info.Length : null );
	}

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
			if( field is not null )
				return field;
			if( FullPath is not null )
				field = Path.Exists( FullPath );
			return field;
		}
	}

	public HackFile() { }

	public HackFile( HackFile hack ) => AssignFrom( hack );

	public HackFile(
		string name,
		string? fullPath = null,
		string? typeExt = null,
		DateTime modifiedDate = default,
		string? sha1Checksum = null,
		long? fileSize = null,
		int? hpVersionId = null,
		int? hpEntryId = null,
		bool? hasRemoteEntry = null,
		bool? hasRemoteVersion = null,
		string? basePath = null,
		string? relativePath = null )
	{
		if( !string.IsNullOrEmpty( fullPath ) )
		{
			FileInfo file = new( fullPath );
			if( file.Exists )
				Info = file;
		}

		FullPath = fullPath;
		Name = name;
		DirectoryName = basePath;
		RelativePath = relativePath;
		TypeExt = typeExt;
		ModifiedDate = modifiedDate;
		Checksum = sha1Checksum;
		HpVersionId = hpVersionId;
		HpEntryId = hpEntryId;
		HasRemoteEntry = hasRemoteEntry;
		HasRemoteVersion = hasRemoteVersion;
		FileSize = fileSize;
	}

	public HackFile( FileInfo? file, bool computeChecksum = true )
	{
		if( file is null )
			return;
		Info = file;
		Name = file.Name;
		DirectoryName = file.DirectoryName;
		FullPath = file.FullName;
		TypeExt = file.Extension;
		if( !file.Exists )
			return;
		ModifiedDate = file.LastWriteTime.ToUniversalTime();
		FileSize = file.Length;
		if( computeChecksum )
			Checksum = Checksums.OfFile( file.FullName );
	}

	public HackFile( EntryRow entry )
	{
		if( entry.LocalFile is null )
			return;

		FullPath = entry.ReprType is EntryReprType.Local
			? entry.FullName
			: PwaPaths.ToAbsolute( entry.FullName );
		if( !string.IsNullOrEmpty( FullPath ) )
		{
			try
			{
				FileInfo info = new( FullPath );
				Info = info.Exists ? info : null;
			}
			catch( Exception ex ) when( ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException )
			{
				HackLogger.Warn( "HackFile", $"Could not inspect '{FullPath}': {ex.Message}", ex );
				Info = null;
			}
		}

		Name = Info?.Name ?? entry.Name;
		DirectoryName = Info?.DirectoryName ?? Path.GetDirectoryName( FullPath );
		FileSize = Info?.Length ?? entry.Size;
		ModifiedDate = Info?.LastWriteTime.ToUniversalTime() ?? entry.LocalDate ?? entry.RemoteDate ?? default;
		TypeExt = Info?.Extension ?? entry.Type;
		HpVersionId = entry.LatestId;
		HpEntryId = entry.Id;
		HasRemoteEntry = entry.Id is not ( null or 0 );
		HasRemoteVersion = entry.LatestId is not ( null or 0 );
	}

	public HackFile( string? fullPath ) => AssignFrom( GetFromPath( fullPath ) );

	public void SetModifiedDate( DateTime date ) => OverwriteDate = date;
	public string? GetChecksum() => Checksum ??= Checksums.OfFile( FullPath );

	internal void ApplyModifiedDateToLocal()
	{
		if( OverwriteDate == default || FullPath is not { } fullPath )
			return;
		Info ??= new( fullPath );
		if( Exists is not true )
			return;
		try
		{
			Info.LastWriteTime = OverwriteDate;
		}
		catch( Exception ex ) when( ex is IOException or UnauthorizedAccessException )
		{
			HackLogger.Warn( "HackFile", $"Could not set modified date on '{fullPath}': {ex.Message}", ex );
		}
	}

	internal void ApplyNonOwnerReadOnly()
	{
		if( FullPath is not null )
			Info ??= new( FullPath );
		if( Owner is true || Exists is not true )
			return;
		try
		{
			Info!.Attributes |= FileAttributes.ReadOnly;
		}
		catch( Exception ex ) when( ex is IOException or UnauthorizedAccessException )
		{
			HackLogger.Warn( "HackFile", $"Could not set read-only attribute on '{FullPath}': {ex.Message}", ex );
		}
	}

	private void AssignFrom( HackFile? hack )
	{
		Info = hack?.Info;
		FullPath = hack?.FullPath;
		Name = hack?.Name;
		DirectoryName = hack?.DirectoryName;
		RelativePath = hack?.RelativePath;
		TypeExt = hack?.TypeExt;
		ModifiedDate = hack?.ModifiedDate ?? default;
		Checksum = hack?.Checksum;
		HpVersionId = hack?.HpVersionId;
		HasRemoteVersion = hack?.HasRemoteVersion;
		FileContents = hack?.FileContents;
		FileSize = hack?.FileSize;
	}

	public static HackFile? GetFromPath(
		string? path,
		string? directory = null,
		bool initWithFileInfo = true,
		bool computeChecksum = true )
	{
		if( path is null )
			return null;

		HackFile hack;
		if( initWithFileInfo )
		{
			FileInfo file = new( path );
			hack = new( file, computeChecksum );
			if( !file.Exists )
				return hack;
		}
		else
		{
			hack = new()
			{
				Name = Path.GetFileName( path ),
				FullPath = Path.GetFullPath( path ),
				DirectoryName = directory ?? Path.GetDirectoryName( path ),
				TypeExt = Path.GetExtension( path ),
				ModifiedDate = File.GetLastWriteTime( path ).ToUniversalTime(),
			};
		}

		hack.RelativePath = Path.Combine( [ PwaPaths.Root, .. PwaPaths.LocalSegments( hack.DirectoryName ) ?? [] ] );
		return hack;
	}

	private string? IdentityPath
	{
		get
		{
			string? path = FullPath is { Length: > 0 } full
				? full
				: DirectoryName is { Length: > 0 } dir && Name is { Length: > 0 } name
					? Path.Combine( dir, name )
					: null;
			if( path is null )
				return null;
			try { return Path.GetFullPath( path ); }
			catch { return path; }
		}
	}

	public override bool Equals( object? obj )
	{
		if( ReferenceEquals( this, obj ) )
			return true;
		if( obj is not HackFile other )
			return false;
		string? mine = IdentityPath;
		string? theirs = other.IdentityPath;
		return mine is not null && theirs is not null && PathComparer.Equals( mine, theirs );
	}

	public override int GetHashCode() => IdentityPath is { } path ? PathComparer.GetHashCode( path ) : 0;

	public static implicit operator HackFile( FileInfo file ) => new( file );
}
