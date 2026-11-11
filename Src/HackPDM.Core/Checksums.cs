using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;

using HackPDM.Domain.OdooModels.Models;
using HackPDM.Shared.GlobalData;

namespace HackPDM.Core;

/// <summary>File checksums, as the server stores them: lowercase hex.</summary>
public static class Checksums
{
	public static HashAlgorithm Algorithm( ChecksumType type ) => type switch
	{
		ChecksumType.Md5    => MD5.Create(),
		ChecksumType.Sha256 => SHA256.Create(),
		ChecksumType.Sha512 => SHA512.Create(),
		_                   => SHA1.Create(),
	};

	/// <summary>
	/// The file's checksum in lowercase hex; "" for an empty file, null when it can't be read.
	/// </summary>
	public static string? OfFile( string? path, ChecksumType type = ChecksumType.Sha1 )
	{
		if( string.IsNullOrEmpty( path ) )
			return null;
		try
		{
			FileInfo file = new( path );
			if( !file.Exists )
				return null;
			using FileStream stream = file.OpenRead();
			if( stream.Length == 0 )
				return "";
			using HashAlgorithm algorithm = Algorithm( type );
			return Convert.ToHexStringLower( algorithm.ComputeHash( stream ) );
		}
		catch( Exception e )
		{
			Debug.WriteLine( $"checksum of {path} failed: {e.Message}" );
			return null;
		}
	}

	/// <summary>Whether the file exists, isn't empty and has this checksum.</summary>
	public static bool Matches( string? path, string? expected, ChecksumType type = ChecksumType.Sha1 )
		=> OfFile( path, type ) is { Length: > 0 } actual && actual == expected;

	/// <summary>Whether the version's local copy (at its server name under the working folder) is this version.</summary>
	public static bool MatchesLocalCopy( IHpVersionModel version, ChecksumType type = ChecksumType.Sha1 )
		=> Matches( PwaPaths.ToAbsolute( version.windows_complete_name ), version.checksum, type );
}
