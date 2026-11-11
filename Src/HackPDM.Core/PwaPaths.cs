using System;
using System.Collections.Generic;
using System.IO;

using HackPDM.Core.Hack;

namespace HackPDM.Core;

/// <summary>
/// The one place that converts between the path forms HackPDM uses:
/// <list type="bullet">
/// <item>server names, <c>root\a\b\file.txt</c>: hp.entry / hp.version windows_complete_name, and
/// the directory tree's paths (<c>root\a\b</c>); backslashes on every OS;</item>
/// <item>server directory names, <c>root / a / b</c>: hp.directory complete_name;</item>
/// <item>local paths, absolute, under the working folder (<see cref="HackDefaults.PwaPathAbsolute"/>).</item>
/// </list>
/// The first segment of a server path is <see cref="Root"/>; the working folder's own name is
/// accepted in its place.
/// </summary>
public static class PwaPaths
{
	/// <summary>Name of the top directory on the server.</summary>
	public const string Root = "root";
	/// <summary>Separator in server names and tree paths.</summary>
	public const char NameSeparator = '\\';
	/// <summary>Separator in server directory names.</summary>
	public const string DirectorySeparator = " / ";

	private static readonly char[] AnySeparator = [ '\\', '/' ];

	/// <summary>The working folder (local root of the vault).</summary>
	public static string Vault => HackDefaults.Instance.PwaPathAbsolute;

	/// <summary>
	/// The segments below the root of a server path in any form. A directory name
	/// (<c>root / a / b</c>) splits only on " / ", so "a/b" there is one folder.
	/// </summary>
	public static string[] Segments( string? serverPath )
	{
		if( string.IsNullOrEmpty( serverPath ) )
			return [];
		string[] segments = serverPath.Contains( DirectorySeparator )
			? serverPath.Split( DirectorySeparator, StringSplitOptions.RemoveEmptyEntries )
			: serverPath.Split( AnySeparator, StringSplitOptions.RemoveEmptyEntries );
		return segments is [ var first, .. ] && IsRootName( first ) ? segments[ 1.. ] : segments;
	}

	private static bool IsRootName( string segment )
		=> segment == Root || segment == HackDefaults.Instance.PwaPathRelative;

	/// <summary>A server path as a path relative to the working folder (this OS's separators).</summary>
	public static string ToRelative( string? serverPath ) => string.Join( Path.DirectorySeparatorChar, Segments( serverPath ) );

	/// <summary>A server path as a local absolute path; null for null.</summary>
	public static string? ToAbsolute( string? serverPath )
		=> serverPath is null ? null : Path.Combine( Vault, ToRelative( serverPath ) );

	/// <summary>The folder part of a server name (<c>root\a\b\file</c> → <c>a/b</c>), relative to the working folder.</summary>
	public static string FolderOf( string? serverName )
		=> Segments( serverName ) is { Length: > 1 } segments
			? string.Join( Path.DirectorySeparatorChar, segments[ ..^1 ] )
			: "";

	/// <summary>
	/// The folders (and file) of a local path below the working folder; empty for the working
	/// folder itself, null when the path is outside it.
	/// </summary>
	public static string[]? LocalSegments( string? localPath )
	{
		string vault = Vault;
		if( string.IsNullOrEmpty( localPath ) || string.IsNullOrEmpty( vault ) || !Path.IsPathFullyQualified( localPath ) )
			return null;
		string relative = Path.GetRelativePath( vault, localPath );
		if( relative == "." )
			return [];
		if( relative == ".." || relative.StartsWith( ".." + Path.DirectorySeparatorChar ) || Path.IsPathRooted( relative ) )
			return null;
		return relative.Split( AnySeparator, StringSplitOptions.RemoveEmptyEntries );
	}

	/// <summary>Whether a local path is inside the working folder (not merely starting with its name).</summary>
	public static bool IsInside( string? localPath ) => LocalSegments( localPath ) is not null;

	/// <summary>A server name / tree path from segments below the root: <c>root\a\b</c>.</summary>
	public static string ToServerName( IEnumerable<string> segments ) => string.Join( NameSeparator, [ Root, .. segments ] );

	/// <summary>The server name of a local path (<c>root\a\b\file</c>), or null outside the working folder.</summary>
	public static string? ToServerName( string? localPath )
		=> LocalSegments( localPath ) is { } segments ? ToServerName( segments ) : null;

	/// <summary>The server directory name of a local path (<c>root / a / b</c>), or null outside the working folder.</summary>
	public static string? ToServerDirectory( string? localPath )
		=> LocalSegments( localPath ) is { } segments ? string.Join( DirectorySeparator, [ Root, .. segments ] ) : null;
}
