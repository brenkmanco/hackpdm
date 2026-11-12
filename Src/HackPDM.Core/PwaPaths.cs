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
	/// Splits a server path in any form into its segments below the root. A directory name
	/// (<c>root / a / b</c>) splits only on " / ", so "a/b" there is one folder; other paths split on
	/// both <c>\</c> and <c>/</c>.
	/// </summary>
	/// <param name="serverPath">A server name, tree path or directory name; may be <see langword="null"/>.</param>
	/// <returns>
	/// The segments with empty ones removed and a leading <see cref="Root"/> (or the working folder's name)
	/// dropped; an empty array for <see langword="null"/> or "".
	/// </returns>
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

	/// <summary>Converts a server path to a path relative to the working folder.</summary>
	/// <param name="serverPath">A server path in any form (see <see cref="Segments"/>).</param>
	/// <returns>The segments below the root joined with this OS's separator; "" for the root itself or <see langword="null"/>.</returns>
	public static string ToRelative( string? serverPath ) => string.Join( Path.DirectorySeparatorChar, Segments( serverPath ) );

	/// <summary>Converts a server path to a local absolute path under the working folder (<see cref="Vault"/>).</summary>
	/// <param name="serverPath">A server path in any form (see <see cref="Segments"/>).</param>
	/// <returns>The absolute local path, or <see langword="null"/> when <paramref name="serverPath"/> is <see langword="null"/>.</returns>
	/// <remarks>Does not touch the disk; the result is relative if <see cref="Vault"/> is empty.</remarks>
	public static string? ToAbsolute( string? serverPath )
		=> serverPath is null ? null : Path.Combine( Vault, ToRelative( serverPath ) );

	/// <summary>Gets the folder part of a server name (<c>root\a\b\file</c> → <c>a/b</c>), relative to the working folder.</summary>
	/// <param name="serverName">A server name that ends with the file name.</param>
	/// <returns>Every segment below the root except the last, joined with this OS's separator; "" when there is at most one segment.</returns>
	public static string FolderOf( string? serverName )
		=> Segments( serverName ) is { Length: > 1 } segments
			? string.Join( Path.DirectorySeparatorChar, segments[ ..^1 ] )
			: "";

	/// <summary>Splits a local absolute path into the folders (and file) below the working folder.</summary>
	/// <param name="localPath">A fully qualified local path.</param>
	/// <returns>
	/// The segments below the working folder; an empty array for the working folder itself; <see langword="null"/>
	/// when the path is empty, not fully qualified, outside the working folder, or no working folder is set.
	/// </returns>
	/// <remarks>Purely textual (<see cref="System.IO.Path.GetRelativePath(string, string)"/>); nothing is read from disk.</remarks>
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

	/// <summary>Tells whether a local path is inside the working folder (not merely starting with its name).</summary>
	/// <param name="localPath">A fully qualified local path.</param>
	/// <returns><see langword="true"/> for the working folder itself or anything below it; see <see cref="LocalSegments"/>.</returns>
	public static bool IsInside( string? localPath ) => LocalSegments( localPath ) is not null;

	/// <summary>Builds a server name or tree path from segments below the root.</summary>
	/// <param name="segments">The folder (and file) names below the root.</param>
	/// <returns><see cref="Root"/> and the segments joined with backslashes, for example <c>root\a\b</c>.</returns>
	public static string ToServerName( IEnumerable<string> segments ) => string.Join( NameSeparator, [ Root, .. segments ] );

	/// <summary>Converts a local absolute path to its server name (<c>root\a\b\file</c>).</summary>
	/// <param name="localPath">A fully qualified local path.</param>
	/// <returns>The server name, or <see langword="null"/> when the path is outside the working folder (see <see cref="LocalSegments"/>).</returns>
	public static string? ToServerName( string? localPath )
		=> LocalSegments( localPath ) is { } segments ? ToServerName( segments ) : null;

	/// <summary>Converts a local absolute path to the server's directory-name form (<c>root / a / b</c>).</summary>
	/// <param name="localPath">A fully qualified local path, normally a folder.</param>
	/// <returns>The directory name, or <see langword="null"/> when the path is outside the working folder.</returns>
	/// <remarks>Every segment is kept, so a file path's file name becomes the last segment.</remarks>
	public static string? ToServerDirectory( string? localPath )
		=> LocalSegments( localPath ) is { } segments ? string.Join( DirectorySeparator, [ Root, .. segments ] ) : null;
}
