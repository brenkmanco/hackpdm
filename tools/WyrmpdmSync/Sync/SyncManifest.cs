using System.Text.Json;
using System.Text.Json.Serialization;

namespace WyrmpdmSync.Sync;

/// <summary>
/// design/penpot-sync.json: which Penpot file the repo syncs with, and every link between a Penpot item and the code
/// it belongs to. Each link keeps the value both sides agreed on at the last sync, so a later run can tell which side
/// changed (or that both did: a conflict).
/// </summary>
internal sealed class SyncManifest
{
	public const int CurrentVersion = 1;

	public int Version { get; set; } = CurrentVersion;
	public PenpotTarget Penpot { get; set; } = new();
	public List<ColorLink> Colors { get; set; } = [];

	public static readonly JsonSerializerOptions Json = new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		WriteIndented = true,
		DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
	};

	public static SyncManifest Load( string path )
	{
		if( !File.Exists( path ) )
			throw new SyncException( $"No sync manifest at {path}. Run `wyrmpdm-sync init` first." );
		SyncManifest manifest = JsonSerializer.Deserialize<SyncManifest>( File.ReadAllText( path ), Json )
		                        ?? throw new SyncException( $"{path} is empty." );
		if( manifest.Version > CurrentVersion )
			throw new SyncException( $"{path} is version {manifest.Version}; this tool reads up to {CurrentVersion}. Update the tool." );
		return manifest;
	}

	public void Save( string path )
	{
		Directory.CreateDirectory( Path.GetDirectoryName( path )! );
		// stable order keeps the manifest's git diffs readable
		Colors = [ .. Colors.OrderBy( c => c.Palette, StringComparer.Ordinal ).ThenBy( c => c.Role, StringComparer.Ordinal ) ];
		File.WriteAllText( path, JsonSerializer.Serialize( this, Json ) + "\n" );
	}
}

/// <summary>The Penpot file the repo syncs with.</summary>
internal sealed class PenpotTarget
{
	public string BaseUrl { get; set; } = "https://design.penpot.app";
	public Guid FileId { get; set; }
	public string? FileName { get; set; }
}

/// <summary>
/// One preset color: a Penpot color token (e.g. "Light.Primary" in the set "HackPDM colors/Light") and the matching
/// entry in <c>ThemePresets.Palettes</c> (palette Light, Material role Primary).
/// </summary>
internal sealed class ColorLink
{
	/// <summary>The Penpot token name, e.g. "NavyBlue.Primary".</summary>
	public required string Token { get; set; }
	/// <summary>The <c>PaletteName</c> in code, e.g. "NavyBlue".</summary>
	public required string Palette { get; set; }
	/// <summary>The <c>MaterialRoles</c> name, e.g. "Primary".</summary>
	public required string Role { get; set; }
	public Guid? TokenId { get; set; }
	public Guid? SetId { get; set; }
	/// <summary>The value both sides had at the last sync ("#RRGGBB" or "#RRGGBBAA"); null when they never agreed.</summary>
	public string? Synced { get; set; }
}
