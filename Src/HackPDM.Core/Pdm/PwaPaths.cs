using System.Diagnostics.CodeAnalysis;
using System.Text;

using HackPDM.Core.Configuration;

namespace HackPDM.Core.Pdm;

/// <summary>Converts between server names (root\a\b\file), server directories (root / a / b), and local paths.</summary>
public static class PwaPaths
{
	public const string Root = "root";
	public const char NameSeparator = '\\';
	public const string DirectorySeparator = " / ";

	public static readonly StringComparer PathComparer = OperatingSystem.IsWindows()
		? StringComparer.OrdinalIgnoreCase
		: StringComparer.Ordinal;

	public static readonly StringComparison PathComparison = OperatingSystem.IsWindows()
		? StringComparison.OrdinalIgnoreCase
		: StringComparison.Ordinal;

	private static readonly char[] AnySeparator = [ '\\', '/' ];
	private static VaultFullCache? _vaultFullCache;

	private sealed record VaultFullCache( string Raw, string Full );

	public static string Vault => HackDefaults.Instance.PwaPathAbsolute;

	public static string[] Segments( string? serverPath )
	{
		if( string.IsNullOrEmpty( serverPath ) )
			return [];
		string[] segments = serverPath.Contains( DirectorySeparator, StringComparison.Ordinal )
			? serverPath.Split( DirectorySeparator, StringSplitOptions.RemoveEmptyEntries )
			: serverPath.Split( AnySeparator, StringSplitOptions.RemoveEmptyEntries );
		return segments is [ var first, .. ] && IsRootName( first.AsSpan() ) ? segments[ 1.. ] : segments;
	}

	private static bool IsRootName( ReadOnlySpan<char> segment )
		=> segment.Equals( Root, StringComparison.OrdinalIgnoreCase )
			|| ( HackDefaults.Instance?.PwaPathRelative is { Length: > 0 } rel
				&& segment.Equals( rel, StringComparison.OrdinalIgnoreCase ) );

	private ref struct SegmentEnumerator
	{
		private ReadOnlySpan<char> _remaining;
		private readonly bool _useDirectorySeparator;
		private bool _firstChecked;

		public ReadOnlySpan<char> Current { get; private set; }

		public SegmentEnumerator( ReadOnlySpan<char> path )
		{
			_remaining = path;
			_useDirectorySeparator = path.Contains( DirectorySeparator, StringComparison.Ordinal );
			_firstChecked = false;
			Current = default;
		}

		public bool MoveNext()
		{
			while( !_remaining.IsEmpty )
			{
				ReadOnlySpan<char> seg;
				if( _useDirectorySeparator )
				{
					int idx = _remaining.IndexOf( DirectorySeparator, StringComparison.Ordinal );
					if( idx < 0 )
					{
						seg = _remaining;
						_remaining = default;
					}
					else
					{
						seg = _remaining[ ..idx ];
						_remaining = _remaining[ ( idx + DirectorySeparator.Length ).. ];
					}
				}
				else
				{
					int idx = _remaining.IndexOfAny( '\\', '/' );
					if( idx < 0 )
					{
						seg = _remaining;
						_remaining = default;
					}
					else
					{
						seg = _remaining[ ..idx ];
						_remaining = _remaining[ ( idx + 1 ).. ];
					}
				}

				if( seg.IsEmpty )
					continue;
				if( !_firstChecked )
				{
					_firstChecked = true;
					if( IsRootName( seg ) )
						continue;
				}
				Current = seg;
				return true;
			}
			return false;
		}
	}

	public static bool SameTreePath( string? a, string? b )
	{
		if( a is null || b is null )
			return false;
		if( string.Equals( a, b, StringComparison.OrdinalIgnoreCase ) )
			return true;

		SegmentEnumerator ea = new( a.AsSpan() );
		SegmentEnumerator eb = new( b.AsSpan() );
		while( true )
		{
			bool ha = ea.MoveNext();
			bool hb = eb.MoveNext();
			if( ha != hb )
				return false;
			if( !ha )
				return true;
			if( !ea.Current.Equals( eb.Current, StringComparison.OrdinalIgnoreCase ) )
				return false;
		}
	}

	public static bool IsSameOrAncestorTreePath( string? ancestor, string? descendant )
	{
		if( ancestor is null || descendant is null )
			return false;

		SegmentEnumerator ea = new( ancestor.AsSpan() );
		SegmentEnumerator ed = new( descendant.AsSpan() );
		while( ea.MoveNext() )
		{
			if( !ed.MoveNext() )
				return false;
			if( !ea.Current.Equals( ed.Current, StringComparison.OrdinalIgnoreCase ) )
				return false;
		}
		return true;
	}

	public static string ToRelative( string? serverPath )
	{
		if( string.IsNullOrEmpty( serverPath ) )
			return "";

		SegmentEnumerator e = new( serverPath.AsSpan() );
		if( !e.MoveNext() )
			return "";

		ReadOnlySpan<char> first = e.Current;
		if( !e.MoveNext() )
			return first.ToString();

		Span<char> stack = stackalloc char[ 260 ];
		StringBuilder? sb = null;
		int pos = 0;

		AppendSegment( first, ref stack, ref sb, ref pos, addSep: false );
		do
		{
			AppendSegment( e.Current, ref stack, ref sb, ref pos, addSep: true );
		}
		while( e.MoveNext() );

		return sb is not null ? sb.ToString() : new string( stack[ ..pos ] );
	}

	public static string? ToAbsolute( string? serverPath )
		=> serverPath is null ? null : Path.Combine( Vault, ToRelative( serverPath ) );

	public static string FolderOf( string? serverName )
	{
		if( string.IsNullOrEmpty( serverName ) )
			return "";

		SegmentEnumerator e = new( serverName.AsSpan() );
		if( !e.MoveNext() )
			return "";
		ReadOnlySpan<char> prev = e.Current;
		if( !e.MoveNext() )
			return "";

		Span<char> stack = stackalloc char[ 260 ];
		StringBuilder? sb = null;
		int pos = 0;
		bool any = false;

		do
		{
			AppendSegment( prev, ref stack, ref sb, ref pos, addSep: any );
			any = true;
			prev = e.Current;
		}
		while( e.MoveNext() );

		return sb is not null ? sb.ToString() : new string( stack[ ..pos ] );
	}

	private static void AppendSegment(
		ReadOnlySpan<char> seg,
		ref Span<char> stack,
		ref StringBuilder? sb,
		ref int pos,
		bool addSep )
	{
		int needed = seg.Length + ( addSep ? 1 : 0 );
		if( sb is null && pos + needed <= stack.Length )
		{
			if( addSep )
				stack[ pos++ ] = Path.DirectorySeparatorChar;
			seg.CopyTo( stack[ pos.. ] );
			pos += seg.Length;
			return;
		}

		if( sb is null )
		{
			sb = new StringBuilder( pos + needed + 64 );
			sb.Append( stack[ ..pos ] );
		}
		if( addSep )
			sb.Append( Path.DirectorySeparatorChar );
		sb.Append( seg );
	}

	private static string? TryGetRelativeToVault( string? localPath )
	{
		string vault = Vault;
		if( string.IsNullOrWhiteSpace( localPath ) || string.IsNullOrWhiteSpace( vault )
			|| !Path.IsPathFullyQualified( localPath ) || !Path.IsPathFullyQualified( vault ) )
			return null;

		string normalizedVault;
		string normalizedPath;
		try
		{
			VaultFullCache? cached = _vaultFullCache;
			if( cached is not null && string.Equals( cached.Raw, vault, StringComparison.Ordinal ) )
				normalizedVault = cached.Full;
			else
			{
				normalizedVault = Path.GetFullPath( vault );
				_vaultFullCache = new VaultFullCache( vault, normalizedVault );
			}
			normalizedPath = Path.GetFullPath( localPath );
		}
		catch
		{
			return null;
		}

		string relative = Path.GetRelativePath( normalizedVault, normalizedPath );
		if( relative == "." )
			return "";
		if( relative == ".."
			|| ( relative.Length >= 3 && relative[ 0 ] == '.' && relative[ 1 ] == '.' && relative[ 2 ] == Path.DirectorySeparatorChar )
			|| Path.IsPathRooted( relative ) )
			return null;
		return relative;
	}

	public static string[]? LocalSegments( string? localPath )
	{
		string? relative = TryGetRelativeToVault( localPath );
		if( relative is null )
			return null;
		return relative.Length == 0
			? []
			: relative.Split( AnySeparator, StringSplitOptions.RemoveEmptyEntries );
	}

	public static bool IsInside( [NotNullWhen( true )] string? localPath )
		=> TryGetRelativeToVault( localPath ) is not null;

	public static string ToServerName( IEnumerable<string> segments )
	{
		StringBuilder sb = new( Root );
		foreach( string seg in segments )
			sb.Append( NameSeparator ).Append( seg );
		return sb.ToString();
	}

	public static string? ToServerName( string? localPath )
	{
		string? relative = TryGetRelativeToVault( localPath );
		if( relative is null )
			return null;
		ReadOnlySpan<char> trimmed = relative.AsSpan().Trim( AnySeparator );
		if( trimmed.IsEmpty )
			return Root;
		return trimmed.Contains( '/' )
			? string.Concat( Root, "\\", trimmed ).Replace( '/', '\\' )
			: string.Concat( Root, "\\", trimmed );
	}

	public static string? ToServerDirectory( string? localPath )
	{
		string? relative = TryGetRelativeToVault( localPath );
		if( relative is null )
			return null;
		ReadOnlySpan<char> trimmed = relative.AsSpan().Trim( AnySeparator );
		if( trimmed.IsEmpty )
			return Root;

		StringBuilder sb = new( Root.Length + trimmed.Length + 16 );
		sb.Append( Root );
		while( !trimmed.IsEmpty )
		{
			int idx = trimmed.IndexOfAny( '\\', '/' );
			ReadOnlySpan<char> seg;
			if( idx < 0 )
			{
				seg = trimmed;
				trimmed = default;
			}
			else
			{
				seg = trimmed[ ..idx ];
				trimmed = trimmed[ ( idx + 1 ).. ];
			}
			if( !seg.IsEmpty )
				sb.Append( DirectorySeparator ).Append( seg );
		}
		return sb.ToString();
	}
}
