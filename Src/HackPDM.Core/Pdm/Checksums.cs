using System.Security.Cryptography;

using HackPDM.Core.Configuration;
using HackPDM.Core.Odoo.Models;

namespace HackPDM.Core.Pdm;

/// <summary>SHA-1 (and optional MD5/SHA-256/SHA-512) file checksums in lowercase hex.</summary>
public static class Checksums
{
	public static HashAlgorithm Algorithm( ChecksumType type ) => type switch
	{
		ChecksumType.Md5 => MD5.Create(),
		ChecksumType.Sha256 => SHA256.Create(),
		ChecksumType.Sha512 => SHA512.Create(),
		_ => SHA1.Create(),
	};

	public static string? OfFile( string? path, ChecksumType type = ChecksumType.Sha1 )
	{
		if( string.IsNullOrEmpty( path ) )
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
			if( stream.Length == 0 )
				return "";
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
			HackLogger.Warn( "Checksums", $"Checksum of '{path}' failed: {e.Message}", e );
			return null;
		}
	}

	public static bool Matches( string? path, string? expected, ChecksumType type = ChecksumType.Sha1 )
		=> OfFile( path, type ) is { Length: > 0 } actual && actual == expected;

	public static bool MatchesLocalCopy( HpVersion version, ChecksumType type = ChecksumType.Sha1 )
		=> Matches( PwaPaths.ToAbsolute( version.windows_complete_name ), version.checksum, type );
}
