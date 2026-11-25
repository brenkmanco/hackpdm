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
	public List<ShapeLink> Shapes { get; set; } = [];

	/// <summary>Every token link, colors first.</summary>
	[JsonIgnore] public IEnumerable<TokenLink> TokenLinks => Colors.Cast<TokenLink>().Concat( Shapes );

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
		Shapes = [ .. Shapes.OrderBy( c => c.Form, StringComparer.Ordinal ).ThenBy( c => c.Property, StringComparer.Ordinal ) ];
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
/// A Penpot design token linked to one value in the code. Kept by token id (renames in Penpot don't break it) and
/// by name (a token recreated with a new id is found again).
/// </summary>
internal abstract class TokenLink
{
	/// <summary>The Penpot token name, as Penpot spells it (e.g. "Navyblue.Primary").</summary>
	[JsonPropertyOrder( -1 )] public required string Token { get; set; }
	[JsonPropertyOrder( 1 )] public Guid? TokenId { get; set; }
	[JsonPropertyOrder( 2 )] public Guid? SetId { get; set; }
	/// <summary>The value both sides had at the last sync, in Penpot's form; null when they never agreed.</summary>
	[JsonPropertyOrder( 3 )] public string? Synced { get; set; }

	/// <summary>The name the link would have if Penpot used the code's spelling ("NavyBlue.Primary").</summary>
	[JsonIgnore] public abstract string CodeName { get; }
	/// <summary>The token set the token belongs in ("HackPDM colors/Navy blue").</summary>
	[JsonIgnore] public abstract string SetName { get; }
	/// <summary>Penpot's token type ("color", "border-radius", "stroke-width").</summary>
	[JsonIgnore] public abstract string TokenType { get; }
	/// <summary>A Penpot value in canonical form, or null when it isn't a plain value (e.g. a reference to another token).</summary>
	public abstract string? Normalize( string? penpotValue );
}

/// <summary>
/// One preset color: a Penpot color token (e.g. "Light.Primary" in the set "HackPDM colors/Light") and the matching
/// argument of <c>MaterialRoles.Scheme( … )</c> in ThemePresets.cs (palette Light, Material role Primary).
/// </summary>
internal sealed class ColorLink : TokenLink
{
	/// <summary>The <c>PaletteName</c> in code, e.g. "NavyBlue".</summary>
	public required string Palette { get; set; }
	/// <summary>The <c>MaterialRoles</c> name, e.g. "Primary".</summary>
	public required string Role { get; set; }

	[JsonIgnore] public override string CodeName => $"{Palette}.{Role}";
	[JsonIgnore] public override string SetName => $"HackPDM colors/{( Enum.TryParse( Palette, out HackPDM.Core.Theming.PaletteName p ) && Enum.IsDefined( p ) ? HackPDM.Core.Theming.ThemePresets.Palette( p ).Title : Palette )}";
	[JsonIgnore] public override string TokenType => "color";
	public override string? Normalize( string? penpotValue ) => Hex.Normalize( penpotValue );
}

/// <summary>
/// One shape-style value: a Penpot token (e.g. "Default.PanelCornerRadius" in "HackPDM shapes/Default") and the
/// matching named argument of the form in ThemePresets.Forms.
/// </summary>
internal sealed class ShapeLink : TokenLink
{
	/// <summary>The <c>FormName</c> in code, e.g. "Default".</summary>
	public required string Form { get; set; }
	/// <summary>The <c>AppForm</c> property, e.g. "PanelCornerRadius".</summary>
	public required string Property { get; set; }

	[JsonIgnore] public override string CodeName => $"{Form}.{Property}";
	[JsonIgnore] public override string SetName => $"HackPDM shapes/{Form}";
	[JsonIgnore] public override string TokenType => ThemePresetsSource.FormProperties.FirstOrDefault( p => p.Property == Property ).TokenType ?? "dimensions";
	public override string? Normalize( string? penpotValue ) => Number.Normalize( penpotValue );
}
