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
			if( !File.Exists( path ) )
				return null;
			using FileStream stream = new(
				path,
				FileMode.Open,
				FileAccess.Read,
				FileShare.ReadWrite,
				131072,
				FileOptions.SequentialScan );
			if( stream.Length == 0 )
				return "";
			if( type == ChecksumType.Sha1 )
				return Convert.ToHexStringLower( SHA1.HashData( stream ) );
			using HashAlgorithm algorithm = Algorithm( type );
			return Convert.ToHexStringLower( algorithm.ComputeHash( stream ) );
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
