namespace HackPDM.Core.Pdm;

/// <summary>Pure status resolution comparing remote Odoo metadata against local disk state.</summary>
public static class EntryStatusResolver
{
	public static FileStatus ResolveRemote(
		bool? deleted,
		int? checkoutUserId,
		int myUserId,
		object? latestChecksum,
		string? localChecksum,
		DateTime? remoteDate,
		DateTime? localModified )
	{
		if( deleted is not false )
			return FileStatus.Dt;

		if( checkoutUserId is not ( null or 0 ) )
			return checkoutUserId == myUserId ? FileStatus.Cm : FileStatus.Co;

		return latestChecksum switch
		{
			bool => FileStatus.NoVersions,
			string when localChecksum is null => FileStatus.Ro,
			string latest when localChecksum == latest => FileStatus.Ok,
			string => remoteDate > localModified ? FileStatus.Nv : FileStatus.Lm,
			_ => FileStatus.Unknown,
		};
	}

	public static FileStatus ResolveLocal( bool restrictTypes, bool filtered, bool knownType, bool ignored )
	{
		if( restrictTypes && ignored )
			return FileStatus.If;
		if( ( restrictTypes || filtered ) && !knownType )
			return FileStatus.Ft;
		return FileStatus.Lo;
	}
}

public enum EntryOpenTarget
{
	None,
	Local,
	Remote,
}

/// <summary>Decides whether an entry opens locally, remotely, or cannot be opened.</summary>
public static class FileStatusPolicy
{
	public static EntryOpenTarget ForOpen( bool hasRemoteId, FileStatus status )
	{
		if( !hasRemoteId )
			return EntryOpenTarget.Local;
		return status switch
		{
			FileStatus.Ro or FileStatus.Nv => EntryOpenTarget.Remote,
			FileStatus.Lm or FileStatus.Ok or FileStatus.Co or FileStatus.Ft or FileStatus.If or FileStatus.Cm => EntryOpenTarget.Local,
			_ => EntryOpenTarget.None,
		};
	}

	public static EntryOpenTarget ForLatestRemote( bool hasRemoteId, FileStatus status )
	{
		if( !hasRemoteId )
			return EntryOpenTarget.None;
		return status switch
		{
			FileStatus.Ro or FileStatus.Nv or FileStatus.Lm or FileStatus.Ok
				or FileStatus.Co or FileStatus.Ft or FileStatus.If or FileStatus.Cm => EntryOpenTarget.Remote,
			_ => EntryOpenTarget.None,
		};
	}

	public static EntryOpenTarget ForLatestLocal( bool hasRemoteId, FileStatus status )
	{
		if( !hasRemoteId )
			return EntryOpenTarget.Local;
		return status switch
		{
			FileStatus.Nv or FileStatus.Lm or FileStatus.Ok or FileStatus.Co
				or FileStatus.Ft or FileStatus.If or FileStatus.Cm => EntryOpenTarget.Local,
			_ => EntryOpenTarget.None,
		};
	}

	public static bool CanUploadIcon( FileStatus status )
		=> status is FileStatus.Lo or FileStatus.Ft or FileStatus.If;
}

/// <summary>Validates preview image bytes before upload to hp.version.preview_image.</summary>
public static class PreviewImageValidator
{
	public const int MaxBytes = 4 * 1024 * 1024;
	public static readonly string[] SupportedExtensions = [ ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".webp" ];

	public static string? Validate( byte[]? bytes )
	{
		if( bytes is not { Length: > 0 } )
			return "The selected image file is empty.";
		if( bytes.Length > MaxBytes )
			return $"Preview images must be {MaxBytes / ( 1024 * 1024 )} MiB or smaller.";
		if( !HasSupportedImageHeader( bytes ) )
			return "Preview image must be a PNG, JPEG, BMP, GIF, or WebP image.";
		return null;
	}

	public static bool HasSupportedImageHeader( ReadOnlySpan<byte> bytes )
	{
		if( bytes.Length >= 8
			&& bytes[ 0 ] == 0x89 && bytes[ 1 ] == 0x50 && bytes[ 2 ] == 0x4E && bytes[ 3 ] == 0x47
			&& bytes[ 4 ] == 0x0D && bytes[ 5 ] == 0x0A && bytes[ 6 ] == 0x1A && bytes[ 7 ] == 0x0A )
			return true;
		if( bytes.Length >= 3 && bytes[ 0 ] == 0xFF && bytes[ 1 ] == 0xD8 && bytes[ 2 ] == 0xFF )
			return true;
		if( bytes.Length >= 6
			&& bytes[ 0 ] == 'G' && bytes[ 1 ] == 'I' && bytes[ 2 ] == 'F' && bytes[ 3 ] == '8'
			&& ( bytes[ 4 ] == '7' || bytes[ 4 ] == '9' ) && bytes[ 5 ] == 'a' )
			return true;
		if( bytes.Length >= 2 && bytes[ 0 ] == 'B' && bytes[ 1 ] == 'M' )
			return true;
		if( bytes.Length >= 12
			&& bytes[ 0 ] == 'R' && bytes[ 1 ] == 'I' && bytes[ 2 ] == 'F' && bytes[ 3 ] == 'F'
			&& bytes[ 8 ] == 'W' && bytes[ 9 ] == 'E' && bytes[ 10 ] == 'B' && bytes[ 11 ] == 'P' )
			return true;
		return false;
	}
}
