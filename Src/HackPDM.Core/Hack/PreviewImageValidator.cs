namespace HackPDM.Core.Hack;

/// <summary>
/// Validates user-supplied preview images before uploading them to Odoo (<c>hp.version.preview_image</c>).
/// Mirrors the server-side format and size guards in <c>hp_version.py</c>.
/// </summary>
public static class PreviewImageValidator
{
	/// <summary>Maximum allowed preview image size in bytes (4 MiB).</summary>
	public const int MaxBytes = 4 * 1024 * 1024;

	/// <summary>Supported image extensions offered in the file picker.</summary>
	public static readonly string[] SupportedExtensions = [ ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".webp" ];

	/// <summary>
	/// Checks whether <paramref name="bytes"/> is a non-empty supported image within <see cref="MaxBytes"/>.
	/// </summary>
	/// <returns><see langword="null"/> when valid; otherwise a user-facing validation error message.</returns>
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

	/// <summary>Returns whether <paramref name="bytes"/> starts with a recognized PNG, JPEG, BMP, GIF, or WebP signature.</summary>
	public static bool HasSupportedImageHeader( ReadOnlySpan<byte> bytes )
	{
		// PNG: 89 50 4E 47 0D 0A 1A 0A
		if( bytes.Length >= 8
			&& bytes[ 0 ] == 0x89 && bytes[ 1 ] == 0x50 && bytes[ 2 ] == 0x4E && bytes[ 3 ] == 0x47
			&& bytes[ 4 ] == 0x0D && bytes[ 5 ] == 0x0A && bytes[ 6 ] == 0x1A && bytes[ 7 ] == 0x0A )
			return true;

		// JPEG: FF D8 FF
		if( bytes.Length >= 3 && bytes[ 0 ] == 0xFF && bytes[ 1 ] == 0xD8 && bytes[ 2 ] == 0xFF )
			return true;

		// GIF: GIF87a / GIF89a
		if( bytes.Length >= 6
			&& bytes[ 0 ] == 'G' && bytes[ 1 ] == 'I' && bytes[ 2 ] == 'F' && bytes[ 3 ] == '8'
			&& ( bytes[ 4 ] == '7' || bytes[ 4 ] == '9' ) && bytes[ 5 ] == 'a' )
			return true;

		// BMP: BM
		if( bytes.Length >= 2 && bytes[ 0 ] == 'B' && bytes[ 1 ] == 'M' )
			return true;

		// WebP: RIFF....WEBP
		if( bytes.Length >= 12
			&& bytes[ 0 ] == 'R' && bytes[ 1 ] == 'I' && bytes[ 2 ] == 'F' && bytes[ 3 ] == 'F'
			&& bytes[ 8 ] == 'W' && bytes[ 9 ] == 'E' && bytes[ 10 ] == 'B' && bytes[ 11 ] == 'P' )
			return true;

		return false;
	}
}
