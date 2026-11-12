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
	/// <summary>Creates the hash algorithm for a checksum type.</summary>
	/// <param name="type">The checksum type; any value other than Md5, Sha256 or Sha512 gives SHA-1.</param>
	/// <returns>A new <see cref="System.Security.Cryptography.HashAlgorithm"/>; the caller disposes it.</returns>
	public static HashAlgorithm Algorithm( ChecksumType type ) => type switch
	{
		ChecksumType.Md5    => MD5.Create(),
		ChecksumType.Sha256 => SHA256.Create(),
		ChecksumType.Sha512 => SHA512.Create(),
		_                   => SHA1.Create(),
	};

	/// <summary>Computes a file's checksum, in the lowercase hex form the server stores.</summary>
	/// <param name="path">The file's path.</param>
	/// <param name="type">The hash algorithm; SHA-1 by default, as the server uses.</param>
	/// <returns>
	/// The checksum; "" for an empty file; <see langword="null"/> when the path is empty, the file doesn't exist, or it
	/// can't be read (the error is only written to the debug output).
	/// </returns>
	/// <remarks>Reads the whole file.</remarks>
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

	/// <summary>Tells whether a file exists, isn't empty and has the given checksum.</summary>
	/// <param name="path">The file's path.</param>
	/// <param name="expected">The checksum to compare with, in lowercase hex (the comparison is case-sensitive).</param>
	/// <param name="type">The hash algorithm.</param>
	/// <returns><see langword="true"/> when the file's checksum equals <paramref name="expected"/>.</returns>
	public static bool Matches( string? path, string? expected, ChecksumType type = ChecksumType.Sha1 )
		=> OfFile( path, type ) is { Length: > 0 } actual && actual == expected;

	/// <summary>Tells whether the local copy of a version is identical to that version.</summary>
	/// <param name="version">The version; its <c>windows_complete_name</c> locates the local copy (via <see cref="PwaPaths.ToAbsolute"/>) and its <c>checksum</c> is compared.</param>
	/// <param name="type">The hash algorithm.</param>
	/// <returns><see langword="true"/> when the local copy exists, isn't empty and its checksum matches.</returns>
	public static bool MatchesLocalCopy( IHpVersionModel version, ChecksumType type = ChecksumType.Sha1 )
		=> Matches( PwaPaths.ToAbsolute( version.windows_complete_name ), version.checksum, type );
}
