using System.Text.Json.Nodes;

using WyrmpdmSync.Penpot;
using WyrmpdmSync.Sync;

using Xunit;

namespace WyrmpdmSync.Tests;

public class ComponentSyncTests
{
	private const string Xaml = """
		<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
			<!--  <Style x:Key="Commented" TargetType="Border"><Setter Property="Padding" Value="1" /></Style>  -->
			<Style x:Key="HackToolbarStyle" TargetType="Border">
				<Setter Property="Background" Value="{ThemeResource HackSurfaceContainerBrush}" />
				<Setter Property="Padding" Value="8,4,12,4" />
				<Setter Property="CornerRadius" Value="16" />
			</Style>
			<Style x:Key="HackPaneOutlinedStyle" TargetType="Border">
				<Setter Property="Background" Value="{ThemeResource SurfaceBrush}" />
				<Setter Property="BorderBrush" Value="{StaticResource HackPanelStrokeBrush}" />
			</Style>
		</ResourceDictionary>
		""";

	private static readonly Guid Page = Guid.Parse( "22222222-2222-2222-2222-222222222222" );
	private static readonly Guid LightMain = Guid.Parse( "aaaaaaaa-0000-0000-0000-00000000000a" );
	private static readonly Guid DarkMain = Guid.Parse( "aaaaaaaa-0000-0000-0000-00000000000b" );
	private static readonly Guid FreeCopy = Guid.Parse( "aaaaaaaa-0000-0000-0000-00000000000c" );
	private static readonly Guid PaddedCopy = Guid.Parse( "aaaaaaaa-0000-0000-0000-00000000000d" );

	/// <summary>A file with a Light and a Dark "Toolbar" component and two copies of the Light one (one with its own padding).</summary>
	private static PenpotFile File( int padTop = 4, string fillToken = "SurfaceContainer" )
	{
		JsonObject Toolbar( Guid id, string theme, JsonObject? extra = null )
		{
			JsonObject shape = new()
			{
				[ "id" ] = id.ToString(), [ "type" ] = "frame", [ "r1" ] = 16, [ "r2" ] = 16, [ "r3" ] = 16, [ "r4" ] = 16,
				[ "width" ] = 600, [ "height" ] = 48,
				[ "layout-padding" ] = new JsonObject { [ "p1" ] = padTop, [ "p2" ] = 12, [ "p3" ] = 4, [ "p4" ] = 8 },
				[ "fills" ] = new JsonArray( new JsonObject { [ "fill-color" ] = "#eeedf4", [ "fill-opacity" ] = 1 } ),
				[ "applied-tokens" ] = new JsonObject { [ "fill" ] = $"{theme}.{fillToken}" },
			};
			foreach( (string k, JsonNode? v) in extra ?? [] )
				shape[ k ] = v?.DeepClone();
			return shape;
		}
		JsonObject objects = new()
		{
			[ LightMain.ToString() ] = Toolbar( LightMain, "Light" ),
			[ DarkMain.ToString() ] = Toolbar( DarkMain, "Dark" ),
			[ FreeCopy.ToString() ] = Toolbar( FreeCopy, "Light", new JsonObject { [ "shape-ref" ] = LightMain.ToString() } ),
			[ PaddedCopy.ToString() ] = Toolbar( PaddedCopy, "Light", new JsonObject
			{
				[ "shape-ref" ] = LightMain.ToString(), [ "touched" ] = new JsonArray( "layout-padding" ),
			} ),
		};
		JsonObject Component( Guid main, string theme ) => new()
		{
			[ "id" ] = Guid.NewGuid().ToString(), [ "name" ] = "Toolbar", [ "main-instance-id" ] = main.ToString(),
			[ "main-instance-page" ] = Page.ToString(),
			[ "variant-properties" ] = new JsonArray( new JsonObject { [ "name" ] = "Theme", [ "value" ] = theme } ),
		};
		JsonObject Token( string name, string value ) => new() { [ "id" ] = Guid.NewGuid().ToString(), [ "name" ] = name, [ "type" ] = "color", [ "value" ] = value };
		return PenpotFile.Parse( new JsonObject
		{
			[ "id" ] = Fakes.FileId.ToString(), [ "name" ] = "test", [ "revn" ] = 1, [ "vern" ] = 0,
			[ "data" ] = new JsonObject
			{
				[ "pages" ] = new JsonArray( Page.ToString() ),
				[ "pages-index" ] = new JsonObject { [ Page.ToString() ] = new JsonObject { [ "name" ] = "Components", [ "objects" ] = objects } },
				[ "components" ] = new JsonObject { [ "c1" ] = Component( LightMain, "Light" ), [ "c2" ] = Component( DarkMain, "Dark" ) },
				[ "tokens-lib" ] = new JsonObject
				{
					[ "S-Light" ] = new JsonObject
					{
						[ "id" ] = Guid.NewGuid().ToString(), [ "name" ] = "HackPDM colors/Light",
						[ "tokens" ] = new JsonObject { [ "Light.SurfaceContainer" ] = Token( "Light.SurfaceContainer", "#EEEDF4" ), [ "Light.SurfaceContainerHigh" ] = Token( "Light.SurfaceContainerHigh", "#E8E7EF" ) },
					},
					[ "S-Dark" ] = new JsonObject
					{
						[ "id" ] = Guid.NewGuid().ToString(), [ "name" ] = "HackPDM colors/Dark",
						[ "tokens" ] = new JsonObject { [ "Dark.SurfaceContainer" ] = Token( "Dark.SurfaceContainer", "#1E1F25" ), [ "Dark.SurfaceContainerHigh" ] = Token( "Dark.SurfaceContainerHigh", "#282A2F" ) },
					},
				},
			},
		} );
	}

	private static (SyncManifest, XamlStyles) Linked( PenpotFile file, string xaml = Xaml )
	{
		XamlStyles styles = XamlStyles.FromText( xaml );
		SyncManifest manifest = new();
		ComponentSync.Link( manifest, file, _ => styles );
		return (manifest, styles);
	}

	[Fact]
	public void Xaml_ReadsSettersWithPositions_AndIgnoresComments()
	{
		XamlStyles styles = XamlStyles.FromText( Xaml );
		Assert.DoesNotContain( "Commented", styles.Styles.Keys );
		Assert.Equal( "8,4,12,4", styles.Value( "HackToolbarStyle", "Padding" ) );
		SourceLiteral pad = styles.Styles[ "HackToolbarStyle" ][ "Padding" ];
		Assert.Equal( "8,4,12,4", Xaml.Substring( pad.Start, pad.Length ) );

		styles.Apply( [ (pad, "8,6,12,6") ] );
		Assert.Equal( "8,6,12,6", styles.Value( "HackToolbarStyle", "Padding" ) );
		Assert.Equal( Xaml.Replace( "8,4,12,4", "8,6,12,6" ), styles.Text );
	}

	[Theory]
	[InlineData( "Background", "{ThemeResource HackSurfaceContainerLowBrush}", "SurfaceContainerLow" )]
	[InlineData( "Background", "{ThemeResource SurfaceBrush}", "Surface" )]
	[InlineData( "BorderBrush", "{StaticResource HackPanelStrokeBrush}", "OutlineVariant" )]
	[InlineData( "Background", "Transparent", "Transparent" )]
	[InlineData( "Padding", "16,8", "16,8,16,8" )]
	[InlineData( "Padding", "0", "0,0,0,0" )]
	[InlineData( "CornerRadius", "16", "16" )]
	[InlineData( "CornerRadius", "4,4,0,0", null )]
	public void StyleValues_ReadXaml( string property, string xaml, string? expected )
		=> Assert.Equal( expected, StyleValues.FromXaml( property, xaml ) );

	[Theory]
	[InlineData( "Padding", "16,8,16,8", "1", "16,8" )]
	[InlineData( "Padding", "4,4,4,4", "1", "4" )]
	[InlineData( "Padding", "8,6,12,6", "1", "8,6,12,6" )]
	[InlineData( "Background", "SurfaceContainerHigh", "x", "{ThemeResource HackSurfaceContainerHighBrush}" )]
	[InlineData( "BorderBrush", "OutlineVariant", "{StaticResource HackPanelStrokeBrush}", "{StaticResource HackPanelStrokeBrush}" )]
	public void StyleValues_WriteXaml_Shortest_AndKeepEquivalentText( string property, string value, string current, string expected )
		=> Assert.Equal( expected, StyleValues.ToXaml( property, value, current ) );

	[Fact]
	public void Link_FindsTheStyleSetters_AndStartsInSync()
	{
		(SyncManifest manifest, XamlStyles styles) = Linked( File() );
		ComponentLink toolbar = manifest.Components.Single( c => c.Style == "HackToolbarStyle" );
		Assert.Equal( [ "Background", "Padding", "CornerRadius" ], toolbar.Properties.Keys );
		Assert.All( ComponentSync.Compare( manifest, File(), _ => styles ).Where( r => r.Link == toolbar ), r => Assert.Equal( SyncState.InSync, r.State ) );
		Assert.True( manifest.Components.Single( c => c.Style == "FilledButtonStyle" ).IsReference );
	}

	[Fact]
	public void Pull_WritesPenpotsValueIntoTheStyle()
	{
		(SyncManifest manifest, XamlStyles styles) = Linked( File() );
		List<ComponentReport> reports = ComponentSync.Compare( manifest, File( padTop: 6, fillToken: "SurfaceContainerHigh" ), _ => styles );
		List<ComponentReport> pulled = ComponentSync.ToPull( reports, Prefer.None );
		Assert.Equal( [ "Background", "Padding" ], pulled.Select( r => r.Property ) );

		ComponentSync.ApplyToCode( pulled, _ => styles );
		Assert.Equal( "8,6,12,4", styles.Value( "HackToolbarStyle", "Padding" ) );
		Assert.Equal( "{ThemeResource HackSurfaceContainerHighBrush}", styles.Value( "HackToolbarStyle", "Background" ) );
	}

	[Fact]
	public void Push_WritesEveryTheme_AndCopiesThatKeptTheValue()
	{
		PenpotFile file = File();
		(SyncManifest manifest, XamlStyles _) = Linked( file );
		XamlStyles edited = XamlStyles.FromText( Xaml.Replace( "8,4,12,4", "8,6,12,6" ).Replace( "HackSurfaceContainerBrush", "HackSurfaceContainerHighBrush" ) );
		List<ComponentReport> pushes = ComponentSync.ToPush( ComponentSync.Compare( manifest, file, _ => edited ), Prefer.None );

		ChangeSet changes = ChangeBuilder.ForComponents( file, pushes );
		List<string> paddingTargets = [ .. changes.Changes.Where( c => c[ "operations" ]![ 0 ]![ "attr" ]!.ToString() == "layout-padding" ).Select( c => c[ "id" ]!.ToString() ) ];
		// both themes' main shapes and the copy that didn't override its padding; not the one that did
		Assert.Equal( [ LightMain.ToString(), FreeCopy.ToString(), DarkMain.ToString() ], paddingTargets );
		JsonObject padding = (JsonObject)changes.Changes.First( c => c[ "id" ]!.ToString() == LightMain.ToString() && c[ "operations" ]![ 0 ]![ "attr" ]!.ToString() == "layout-padding" )[ "operations" ]![ 0 ]![ "val" ]!;
		Assert.Equal( (6.0, 12.0, 6.0, 8.0), ((double)padding[ "p1" ]!, (double)padding[ "p2" ]!, (double)padding[ "p3" ]!, (double)padding[ "p4" ]!) );

		// colors use each theme's own token
		JsonObject darkFill = changes.Changes.First( c => c[ "id" ]!.ToString() == DarkMain.ToString() && c[ "operations" ]![ 0 ]![ "attr" ]!.ToString() == "applied-tokens" );
		Assert.Equal( "Dark.SurfaceContainerHigh", darkFill[ "operations" ]![ 0 ]![ "val" ]![ "fill" ]!.ToString() );
		Assert.Equal( "#282a2f", darkFill[ "operations" ]![ 1 ]![ "val" ]![ 0 ]![ "fill-color" ]!.ToString() );
	}

	[Fact]
	public void SizeChangedInCode_IsNotPushed()
	{
		const string sized = """<ResourceDictionary><Style x:Key="PaneIconButtonStyle"><Setter Property="Width" Value="32" /></Style></ResourceDictionary>""";
		Assert.Contains( "Width", StyleValues.PullOnly );
		ComponentReport report = new( new ComponentLink { Component = "Icon button", Style = "PaneIconButtonStyle", File = "x" }, "Width", "32", "40", SyncState.ChangedInCode );
		Assert.Empty( ComponentSync.ToPush( [ report ], Prefer.Code ) );
		Assert.Equal( "32", XamlStyles.FromText( sized ).Value( "PaneIconButtonStyle", "Width" ) );
	}
}
