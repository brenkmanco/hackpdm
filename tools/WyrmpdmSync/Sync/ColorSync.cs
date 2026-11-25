using HackPDM.Core.Theming;

using WyrmpdmSync.Penpot;

namespace WyrmpdmSync.Sync;

/// <summary>One color link's state.</summary>
internal sealed record ColorReport( ColorLink Link, string? Penpot, string? Code, SyncState State );

/// <summary>Links the preset colors to Penpot's color tokens and compares them.</summary>
internal static class ColorSync
{
	/// <summary>
	/// Adds a link for every preset color in the code and fills in the Penpot ids of the token with the same name.
	/// Existing links keep their last-synced value; a link whose two sides already match gets that value as its
	/// starting point. Returns how many links were added.
	/// </summary>
	public static int Link( SyncManifest manifest, PenpotFile file )
	{
		Dictionary<string, PenpotToken> byName = ColorTokens( file );
		Dictionary<string, ColorLink> links = manifest.Colors.ToDictionary( l => $"{l.Palette}.{l.Role}", StringComparer.Ordinal );
		int added = 0;
		foreach( (PaletteName palette, string role, string hex) in CodeColors.All() )
		{
			string name = CodeColors.TokenName( palette, role );
			if( !links.TryGetValue( name, out ColorLink? link ) )
			{
				link = new ColorLink { Token = name, Palette = palette.ToString(), Role = role };
				manifest.Colors.Add( link );
				links[ name ] = link;
				added++;
			}
			PenpotToken? token = link.TokenId is { } known ? file.Tokens.FirstOrDefault( t => t.Id == known ) : null;
			token ??= byName.GetValueOrDefault( name );
			if( token is not null )
			{
				// keep Penpot's spelling (e.g. "Navyblue.Primary"): shapes in the design are bound by that name
				link.Token = token.Name;
				link.TokenId = token.Id;
				link.SetId = token.SetId;
				if( link.Synced is null && Hex.Normalize( token.Value ) == hex )
					link.Synced = hex;
			}
		}
		return added;
	}

	/// <summary>The state of every color link.</summary>
	public static List<ColorReport> Compare( SyncManifest manifest, PenpotFile file )
	{
		Dictionary<Guid, PenpotToken> byId = file.Tokens.ToDictionary( t => t.Id );
		Dictionary<string, PenpotToken> byName = ColorTokens( file );
		List<ColorReport> reports = [];
		foreach( ColorLink link in manifest.Colors )
		{
			// the id survives a rename in Penpot; the name finds a token recreated with a new id
			PenpotToken? token = link.TokenId is { } id && byId.TryGetValue( id, out PenpotToken? t ) ? t
				: byName.GetValueOrDefault( link.Token );
			string? penpot = token is null ? null : Hex.Normalize( token.Value ) ?? token.Value;
			string? code = CodeColors.Value( link );
			reports.Add( new ColorReport( link, penpot, code, ThreeWay.Compare( penpot, code, link.Synced ) ) );
		}
		return reports;
	}

	/// <summary>Color tokens by name, ignoring case: the design file spells the Navy blue tokens "Navyblue.*".</summary>
	private static Dictionary<string, PenpotToken> ColorTokens( PenpotFile file )
	{
		Dictionary<string, PenpotToken> byName = new( StringComparer.OrdinalIgnoreCase );
		foreach( PenpotToken t in file.Tokens.Where( t => t.Type == "color" ) )
			byName.TryAdd( t.Name, t );
		return byName;
	}
}
