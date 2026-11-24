using System.Security.Cryptography;

using HackPDM.Core.Configuration;

namespace HackPDM.Core.Pdm;

/// <summary>SHA-1 (and optional MD5/SHA-256/SHA-512) file checksums in lowercase hex.</summary>
public static class Checksums
{
	public const string EmptySha1 = "da39a3ee5e6b4b0d3255bfef95601890afd80709";

	public static string? OfFile( string? path, ChecksumType type = ChecksumType.Sha1 )
	{
		if( string.IsNullOrEmpty( path ) || !File.Exists( path ) )
			return null;
		try
		{
			using FileStream stream = new(
				path,
				FileMode.Open,
				FileAccess.Read,
				FileShare.ReadWrite,
				4096,
				FileOptions.SequentialScan );
			Span<byte> hash = stackalloc byte[ SHA512.HashSizeInBytes ];
			int written = type switch
			{
				ChecksumType.Md5 => MD5.HashData( stream, hash ),
				ChecksumType.Sha256 => SHA256.HashData( stream, hash ),
				ChecksumType.Sha512 => SHA512.HashData( stream, hash ),
				_ => SHA1.HashData( stream, hash ),
			};
			return Convert.ToHexStringLower( hash[ ..written ] );
		}
		catch( Exception e ) when( e is FileNotFoundException or DirectoryNotFoundException )
		{
			return null;
		}
		catch( Exception e )
		{
			HackLogger.Warn( nameof( Checksums ), $"Checksum of '{path}' failed: {e.Message}", e );
			return null;
		}
	}

	public static bool Matches( string? path, string? expected, ChecksumType type = ChecksumType.Sha1 )
		=> OfFile( path, type ) is { Length: > 0 } actual && string.Equals( actual, expected, StringComparison.OrdinalIgnoreCase );
}
