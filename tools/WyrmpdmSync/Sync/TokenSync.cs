using HackPDM.Core.Theming;

using WyrmpdmSync.Penpot;

namespace WyrmpdmSync.Sync;

/// <summary>One token link's state.</summary>
internal sealed record TokenReport( TokenLink Link, PenpotToken? Token, string? Penpot, string? Code, SyncState State );

/// <summary>Which side wins where both changed (or where they differ with no baseline).</summary>
internal enum Prefer { None, Penpot, Code }

/// <summary>
/// Links Penpot tokens to values in ThemePresets.cs (preset colors and shape-style values), compares them, and works
/// out what `pull` and `push` should change.
/// </summary>
internal static class TokenSync
{
	/// <summary>
	/// Adds a link for every color and shape value the code defines, and finds each one's Penpot token (by id, else by
	/// name ignoring case). Links keep Penpot's spelling of the name. A link whose two sides already match gets that
	/// value as its baseline. Returns how many links were added.
	/// </summary>
	public static int Link( SyncManifest manifest, PenpotFile file, ThemePresetsSource code )
	{
		int added = 0;
		Dictionary<string, ColorLink> colors = manifest.Colors.ToDictionary( l => l.CodeName, StringComparer.Ordinal );
		foreach( (PaletteName palette, string role) in code.Colors.Keys )
		{
			string name = $"{palette}.{role}";
			if( !colors.ContainsKey( name ) )
			{
				ColorLink link = new() { Token = name, Palette = palette.ToString(), Role = role };
				manifest.Colors.Add( link );
				colors[ name ] = link;
				added++;
			}
		}
		Dictionary<string, ShapeLink> shapes = manifest.Shapes.ToDictionary( l => l.CodeName, StringComparer.Ordinal );
		foreach( (FormName form, string property) in code.Forms.Keys )
		{
			string name = $"{form}.{property}";
			if( !shapes.ContainsKey( name ) )
			{
				ShapeLink link = new() { Token = name, Form = form.ToString(), Property = property };
				manifest.Shapes.Add( link );
				shapes[ name ] = link;
				added++;
			}
		}
		foreach( TokenLink link in manifest.TokenLinks )
			Attach( link, Find( link, file ), code );
		return added;
	}

	/// <summary>The state of every token link.</summary>
	public static List<TokenReport> Compare( SyncManifest manifest, PenpotFile file, ThemePresetsSource code )
	{
		List<TokenReport> reports = [];
		foreach( TokenLink link in manifest.TokenLinks )
		{
			PenpotToken? token = Find( link, file );
			string? penpot = token is null ? null : link.Normalize( token.Value ) ?? token.Value;
			string? value = CodeValue( link, code );
			reports.Add( new TokenReport( link, token, penpot, value, ThreeWay.Compare( penpot, value, link.Synced ) ) );
		}
		return reports;
	}

	/// <summary>What `pull` takes from Penpot into the code: changes made only in Penpot, plus the preferred side's conflicts.</summary>
	public static List<TokenReport> ToPull( IEnumerable<TokenReport> reports, Prefer prefer )
		=> [ .. reports.Where( r => r.State == SyncState.ChangedInPenpot
		                             || ( prefer == Prefer.Penpot && r.State is SyncState.Conflict or SyncState.Unlinked ) ) ];

	/// <summary>What `push` takes from the code to Penpot: changes made only in code, tokens Penpot lacks, plus the preferred side's conflicts.</summary>
	public static List<TokenReport> ToPush( IEnumerable<TokenReport> reports, Prefer prefer )
		=> [ .. reports.Where( r => r.State is SyncState.ChangedInCode or SyncState.MissingInPenpot
		                             || ( prefer == Prefer.Code && r.State is SyncState.Conflict or SyncState.Unlinked ) )
		                .Where( r => r.Code is not null ) ];

	/// <summary>Writes the Penpot values of <paramref name="pulled"/> into the code (in memory; call Save to write the file).</summary>
	public static void ApplyToCode( ThemePresetsSource code, IReadOnlyList<TokenReport> pulled )
	{
		List<(SourceLiteral, string)> edits = [];
		foreach( TokenReport r in pulled )
		{
			if( r.Penpot is null || Literal( r.Link, code ) is not { } literal )
				continue;
			edits.Add( (literal, r.Link is ColorLink ? ThemePresetsSource.ColorLiteral( r.Penpot ) : r.Penpot) );
		}
		code.Apply( edits );
	}

	/// <summary>The code's current value for a link, in Penpot's form; null when the code no longer defines it.</summary>
	public static string? CodeValue( TokenLink link, ThemePresetsSource code ) => link switch
	{
		ColorLink c when Enum.TryParse( c.Palette, out PaletteName p ) => code.Color( p, c.Role ),
		ShapeLink s when Enum.TryParse( s.Form, out FormName f ) => code.FormValue( f, s.Property ),
		_ => null,
	};

	private static SourceLiteral? Literal( TokenLink link, ThemePresetsSource code ) => link switch
	{
		ColorLink c when Enum.TryParse( c.Palette, out PaletteName p ) => code.Colors.GetValueOrDefault( (p, c.Role) ),
		ShapeLink s when Enum.TryParse( s.Form, out FormName f ) => code.Forms.GetValueOrDefault( (f, s.Property) ),
		_ => null,
	};

	/// <summary>The link's token: by id first (survives renames), else by name ignoring case (survives re-creation).</summary>
	private static PenpotToken? Find( TokenLink link, PenpotFile file )
	{
		if( link.TokenId is { } id && file.Tokens.FirstOrDefault( t => t.Id == id ) is { } byId )
			return byId;
		return file.Tokens.FirstOrDefault( t => t.Type == link.TokenType && string.Equals( t.Name, link.Token, StringComparison.OrdinalIgnoreCase ) )
		       ?? file.Tokens.FirstOrDefault( t => t.Type == link.TokenType && string.Equals( t.Name, link.CodeName, StringComparison.OrdinalIgnoreCase ) );
	}

	private static void Attach( TokenLink link, PenpotToken? token, ThemePresetsSource code )
	{
		if( token is null )
			return;
		// keep Penpot's spelling (e.g. "Navyblue.Primary"): shapes in the design are bound by that name
		link.Token = token.Name;
		link.TokenId = token.Id;
		link.SetId = token.SetId;
		if( link.Synced is null && link.Normalize( token.Value ) is { } value && value == CodeValue( link, code ) )
			link.Synced = value;
	}
}
