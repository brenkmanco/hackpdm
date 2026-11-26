namespace WyrmpdmSync.Sync;

/// <summary>Color text in Penpot's form: "#RRGGBB", or "#RRGGBBAA" when not opaque. Uppercase.</summary>
internal static class Hex
{
	public static string FromArgb( uint argb )
	{
		uint a = argb >> 24, rgb = argb & 0x00FFFFFF;
		return a == 0xFF ? $"#{rgb:X6}" : $"#{rgb:X6}{a:X2}";
	}

	/// <summary>Parses "#RGB", "#RRGGBB" or "#RRGGBBAA" (any case) to 0xAARRGGBB; null when it isn't a plain color.</summary>
	public static uint? ToArgb( string? text )
	{
		if( text is null )
			return null;
		string s = text.Trim().TrimStart( '#' );
		if( s.Length == 3 )
			s = string.Concat( s.Select( c => $"{c}{c}" ) );
		if( ( s.Length != 6 && s.Length != 8 ) || !uint.TryParse( s, System.Globalization.NumberStyles.HexNumber, null, out uint v ) )
			return null;
		return s.Length == 6 ? 0xFF000000 | v : ( v << 24 ) | ( v >> 8 );
	}

	/// <summary>The same color, written the canonical way; null when it isn't a plain color.</summary>
	public static string? Normalize( string? text ) => ToArgb( text ) is { } v ? FromArgb( v ) : null;
}
