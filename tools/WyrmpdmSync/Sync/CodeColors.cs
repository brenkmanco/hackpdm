using HackPDM.Core.Theming;

namespace WyrmpdmSync.Sync;

/// <summary>The preset colors as the code defines them (<see cref="ThemePresets.Palettes"/>), in Penpot's hex form.</summary>
internal static class CodeColors
{
	/// <summary>The Penpot token name for a palette and role: "Light.Primary", "NavyBlue.SurfaceContainer".</summary>
	public static string TokenName( PaletteName palette, string role ) => $"{palette}.{role}";

	/// <summary>Every (palette, role) → color the code defines.</summary>
	public static IEnumerable<(PaletteName Palette, string Role, string Hex)> All()
	{
		foreach( AppPalette palette in ThemePresets.Palettes )
			foreach( string role in MaterialRoles.Names )
				yield return (palette.Name, role, Hex.FromArgb( palette.Scheme[ role ] ));
	}

	/// <summary>The code's current color for a link, or null when the palette or role no longer exists.</summary>
	public static string? Value( ColorLink link )
		=> Enum.TryParse( link.Palette, out PaletteName name ) && Enum.IsDefined( name ) && MaterialRoles.IsRole( link.Role )
			? Hex.FromArgb( ThemePresets.Palette( name ).Scheme[ link.Role ] )
			: null;
}

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
