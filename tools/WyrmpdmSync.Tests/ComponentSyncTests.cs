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
	[InlineData( "Background", "Transparent", "x", "Transparent" )]
	[InlineData( "BorderBrush", "Transparent", "{ThemeResource OutlineBrush}", "Transparent" )]
	[InlineData( "Background", "#112233", "x", "#112233" )]
	[InlineData( "BorderBrush", "#112233", "{ThemeResource OutlineBrush}", "#112233" )]
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
	public void RowHeightAndFontSize_ReadTheRowAndItsLargestText()
	{
		static JsonObject Text( string id, string size ) => new()
		{
			[ "id" ] = id, [ "type" ] = "text",
			[ "content" ] = new JsonObject { [ "children" ] = new JsonArray( new JsonObject { [ "children" ] = new JsonArray( new JsonObject { [ "children" ] = new JsonArray( new JsonObject { [ "text" ] = "x", [ "font-size" ] = size } ) } ) } ) },
		};
		JsonObject objects = new()
		{
			[ "row" ] = new JsonObject { [ "id" ] = "row", [ "type" ] = "frame", [ "height" ] = 36, [ "shapes" ] = new JsonArray( "value", "label" ) },
			[ "label" ] = Text( "label", "12" ),
			[ "value" ] = Text( "value", "16" ),
		};
		JsonObject row = objects[ "row" ]!.AsObject();
		Assert.Equal( "36", StyleValues.FromShape( StyleValues.RowHeight, row, "Light", objects ) );
		Assert.Equal( "16", StyleValues.FromShape( StyleValues.FontSize, row, "Light", objects ) );
		Assert.Contains( StyleValues.FontSize, StyleValues.PullOnly );
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

	private static PenpotFile BorderedFile( string strokeToken = "Outline", Guid? strokeCopyId = null )
	{
		JsonObject raw = File().Raw;
		JsonObject objects = raw[ "data" ]![ "pages-index" ]![ Page.ToString() ]![ "objects" ]!.AsObject();
		void AddBorder( string id, string theme, string role, string hex )
		{
			JsonObject shape = objects[ id ]!.AsObject();
			shape[ "strokes" ] = new JsonArray( new JsonObject { [ "stroke-color" ] = hex, [ "stroke-opacity" ] = 1, [ "stroke-width" ] = 1 } );
			shape[ "applied-tokens" ]![ "stroke-color" ] = $"{theme}.{role}";
		}
		AddBorder( LightMain.ToString(), "Light", strokeToken, "#757780" );
		AddBorder( DarkMain.ToString(), "Dark", strokeToken, "#8f909a" );
		AddBorder( FreeCopy.ToString(), "Light", strokeToken, "#757780" );
		AddBorder( PaddedCopy.ToString(), "Light", strokeToken, "#757780" );

		if( strokeCopyId is { } copyId )
		{
			JsonObject copy = (JsonObject)objects[ LightMain.ToString() ]!.DeepClone();
			copy[ "id" ] = copyId.ToString();
			copy[ "shape-ref" ] = LightMain.ToString();
			copy[ "touched" ] = new JsonArray( "stroke-group" );
			copy[ "applied-tokens" ]![ "stroke-color" ] = "Light.Primary";
			copy[ "strokes" ]![ 0 ]![ "stroke-color" ] = "#485e92";
			objects[ copyId.ToString() ] = copy;
		}

		static JsonObject Token( string name, string value ) => new() { [ "id" ] = Guid.NewGuid().ToString(), [ "name" ] = name, [ "type" ] = "color", [ "value" ] = value };
		JsonObject lightTokens = raw[ "data" ]![ "tokens-lib" ]![ "S-Light" ]![ "tokens" ]!.AsObject();
		lightTokens[ "Light.Outline" ] = Token( "Light.Outline", "#757780" );
		lightTokens[ "Light.OutlineVariant" ] = Token( "Light.OutlineVariant", "#C5C6D0" );
		lightTokens[ "Light.Primary" ] = Token( "Light.Primary", "#485E92" );

		JsonObject darkTokens = raw[ "data" ]![ "tokens-lib" ]![ "S-Dark" ]![ "tokens" ]!.AsObject();
		darkTokens[ "Dark.Outline" ] = Token( "Dark.Outline", "#8F909A" );
		darkTokens[ "Dark.OutlineVariant" ] = Token( "Dark.OutlineVariant", "#44464F" );
		darkTokens[ "Dark.Primary" ] = Token( "Dark.Primary", "#B0C6FF" );

		return PenpotFile.Parse( raw );
	}

	[Fact]
	public void Push_BackgroundAndBorderBrushTogether_MergesAppliedTokens_AndPreservesCopyTokens()
	{
		Guid strokeCopy = Guid.Parse( "aaaaaaaa-0000-0000-0000-00000000000e" );
		PenpotFile file = BorderedFile( strokeToken: "Outline", strokeCopyId: strokeCopy );
		const string initialXaml = """
			<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
				<Style x:Key="HackToolbarStyle" TargetType="Border">
					<Setter Property="Background" Value="{ThemeResource HackSurfaceContainerBrush}" />
					<Setter Property="BorderBrush" Value="{ThemeResource OutlineBrush}" />
				</Style>
			</ResourceDictionary>
			""";
		(SyncManifest manifest, XamlStyles _) = Linked( file, initialXaml );
		XamlStyles edited = XamlStyles.FromText( initialXaml
			.Replace( "HackSurfaceContainerBrush", "HackSurfaceContainerHighBrush" )
			.Replace( "OutlineBrush", "OutlineVariantBrush" ) );
		List<ComponentReport> pushes = ComponentSync.ToPush( ComponentSync.Compare( manifest, file, _ => edited ), Prefer.None );
		Assert.Equal( 2, pushes.Count );

		ChangeSet changes = ChangeBuilder.ForComponents( file, pushes );
		Assert.Equal( 2, changes.AppliedComponents.Count );

		// final applied-tokens on Light main contains BOTH pushed tokens
		JsonObject lightFinal = changes.Changes.Last( c => c[ "id" ]!.ToString() == LightMain.ToString() );
		JsonObject lightApplied = lightFinal[ "operations" ]![ 0 ]![ "val" ]!.AsObject();
		Assert.Equal( "Light.SurfaceContainerHigh", lightApplied[ "fill" ]!.ToString() );
		Assert.Equal( "Light.OutlineVariant", lightApplied[ "stroke-color" ]!.ToString() );

		// final applied-tokens on Dark main contains BOTH Dark tokens
		JsonObject darkFinal = changes.Changes.Last( c => c[ "id" ]!.ToString() == DarkMain.ToString() );
		JsonObject darkApplied = darkFinal[ "operations" ]![ 0 ]![ "val" ]!.AsObject();
		Assert.Equal( "Dark.SurfaceContainerHigh", darkApplied[ "fill" ]!.ToString() );
		Assert.Equal( "Dark.OutlineVariant", darkApplied[ "stroke-color" ]!.ToString() );

		// copy with overridden stroke-group gets the new fill while keeping its own stroke-color token
		JsonObject copyChange = Assert.Single( changes.Changes, c => c[ "id" ]!.ToString() == strokeCopy.ToString() );
		JsonObject copyApplied = copyChange[ "operations" ]![ 0 ]![ "val" ]!.AsObject();
		Assert.Equal( "Light.SurfaceContainerHigh", copyApplied[ "fill" ]!.ToString() );
		Assert.Equal( "Light.Primary", copyApplied[ "stroke-color" ]!.ToString() );
	}

	[Fact]
	public void Push_TransparentBorderBrush_ClearsStrokesAndToken()
	{
		PenpotFile file = BorderedFile( strokeToken: "OutlineVariant" );
		const string initialXaml = """
			<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
				<Style x:Key="HackToolbarStyle" TargetType="Border">
					<Setter Property="Background" Value="{ThemeResource HackSurfaceContainerBrush}" />
					<Setter Property="BorderBrush" Value="{ThemeResource OutlineVariantBrush}" />
				</Style>
			</ResourceDictionary>
			""";
		(SyncManifest manifest, XamlStyles _) = Linked( file, initialXaml );
		XamlStyles edited = XamlStyles.FromText( initialXaml.Replace( "{ThemeResource OutlineVariantBrush}", "Transparent" ) );
		List<ComponentReport> pushes = ComponentSync.ToPush( ComponentSync.Compare( manifest, file, _ => edited ), Prefer.None );
		ComponentReport push = Assert.Single( pushes );

		ChangeSet changes = ChangeBuilder.ForComponents( file, pushes );
		Assert.Contains( push, changes.AppliedComponents );

		foreach( (Guid mainId, string theme) in new[] { (LightMain, "Light"), (DarkMain, "Dark") } )
		{
			JsonObject change = changes.Changes.Single( c => c[ "id" ]!.ToString() == mainId.ToString() );
			JsonArray ops = change[ "operations" ]!.AsArray();
			JsonObject applied = ops.Single( o => (string?)o![ "attr" ] == "applied-tokens" )![ "val" ]!.AsObject();
			Assert.False( applied.ContainsKey( "stroke-color" ) );
			Assert.Equal( $"{theme}.SurfaceContainer", applied[ "fill" ]!.ToString() );

			JsonArray strokes = ops.Single( o => (string?)o![ "attr" ] == "strokes" )![ "val" ]!.AsArray();
			Assert.Empty( strokes );

			JsonObject updated = (JsonObject)file.FindShape( mainId )!.Value.Shape.DeepClone();
			foreach( JsonNode? op in ops )
				updated[ op![ "attr" ]!.ToString() ] = op[ "val" ]?.DeepClone();
			Assert.Equal( "Transparent", StyleValues.FromShape( StyleValues.BorderBrush, updated, theme ) );
		}
	}

	[Fact]
	public void Push_PropagatesToDetachedStyledViewLayers_UnlessOverriddenInline()
	{
		Guid lightFrame = Guid.Parse( "bbbbbbbb-0000-0000-0000-000000000001" );
		Guid darkFrame = Guid.Parse( "bbbbbbbb-0000-0000-0000-000000000002" );
		Guid detachedLight = Guid.Parse( "bbbbbbbb-0000-0000-0000-000000000011" );
		Guid overriddenLight = Guid.Parse( "bbbbbbbb-0000-0000-0000-000000000012" );
		Guid detachedDark = Guid.Parse( "bbbbbbbb-0000-0000-0000-000000000021" );
		Guid overriddenDark = Guid.Parse( "bbbbbbbb-0000-0000-0000-000000000022" );

		JsonObject raw = File().Raw;
		JsonObject objects = raw[ "data" ]![ "pages-index" ]![ Page.ToString() ]![ "objects" ]!.AsObject();
		static JsonObject DetachedLayer( Guid id, Guid parent, string name, string theme, int pad ) => new()
		{
			[ "id" ] = id.ToString(), [ "type" ] = "frame", [ "name" ] = name, [ "parent-id" ] = parent.ToString(),
			[ "r1" ] = 16, [ "r2" ] = 16, [ "r3" ] = 16, [ "r4" ] = 16,
			[ "layout-padding" ] = new JsonObject { [ "p1" ] = pad, [ "p2" ] = pad, [ "p3" ] = pad, [ "p4" ] = pad },
			[ "fills" ] = new JsonArray( new JsonObject { [ "fill-color" ] = theme == "Light" ? "#eeedf4" : "#1e1f25", [ "fill-opacity" ] = 1 } ),
			[ "applied-tokens" ] = new JsonObject { [ "fill" ] = $"{theme}.SurfaceContainer" },
		};
		objects[ lightFrame.ToString() ] = new JsonObject
		{
			[ "id" ] = lightFrame.ToString(), [ "type" ] = "frame", [ "name" ] = "Screen — Light",
			[ "parent-id" ] = Guid.Empty.ToString(), [ "shapes" ] = new JsonArray( overriddenLight.ToString(), detachedLight.ToString() ),
		};
		objects[ darkFrame.ToString() ] = new JsonObject
		{
			[ "id" ] = darkFrame.ToString(), [ "type" ] = "frame", [ "name" ] = "Screen — Dark",
			[ "parent-id" ] = Guid.Empty.ToString(), [ "shapes" ] = new JsonArray( overriddenDark.ToString(), detachedDark.ToString() ),
		};
		objects[ detachedLight.ToString() ] = DetachedLayer( detachedLight, lightFrame, "Inherited", "Light", 4 );
		objects[ overriddenLight.ToString() ] = DetachedLayer( overriddenLight, lightFrame, "Overridden", "Light", 16 );
		objects[ detachedDark.ToString() ] = DetachedLayer( detachedDark, darkFrame, "Inherited", "Dark", 4 );
		objects[ overriddenDark.ToString() ] = DetachedLayer( overriddenDark, darkFrame, "Overridden", "Dark", 16 );

		PenpotFile file = PenpotFile.Parse( raw );
		(SyncManifest manifest, XamlStyles _) = Linked( file );
		manifest.Frames.Add( new FrameLink
		{
			View = "view.xaml", Frame = "Screen — Light", DarkFrame = "Screen — Dark",
			Elements =
			[
				new ElementLink { Anchor = "tb.inherited", Path = "Inherited" },
				new ElementLink { Anchor = "tb.overridden", Path = "Overridden" },
			],
		} );
		const string viewXaml = """
			<Page xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:pp="urn:wyrmpdm:penpot-sync">
				<Grid>
					<Border pp:Id="tb.inherited" Style="{StaticResource HackToolbarStyle}" />
					<Border pp:Id="tb.overridden" Style="{StaticResource HackToolbarStyle}" Padding="16" />
				</Grid>
			</Page>
			""";
		XamlView view = XamlView.FromText( viewXaml );
		FrameContext ctx = LayoutSync.Resolve( manifest, file, _ => view ).Single().Context!;
		Assert.Empty( LayoutSync.Link( ctx, LayoutSync.StyleLookup( manifest, file ) ) );

		XamlStyles edited = XamlStyles.FromText( Xaml
			.Replace( "8,4,12,4", "8,6,12,6" )
			.Replace( "HackSurfaceContainerBrush", "HackSurfaceContainerHighBrush" ) );
		List<ComponentReport> pushes = ComponentSync.ToPush( ComponentSync.Compare( manifest, file, _ => edited ), Prefer.None );

		ChangeSet changes = ChangeBuilder.ForComponents( file, pushes, [ ctx ] );

		List<string> paddingTargets = [ .. changes.Changes.Where( c => c[ "operations" ]![ 0 ]![ "attr" ]!.ToString() == "layout-padding" ).Select( c => c[ "id" ]!.ToString() ) ];
		Assert.Contains( detachedLight.ToString(), paddingTargets );
		Assert.Contains( detachedDark.ToString(), paddingTargets );
		Assert.DoesNotContain( overriddenLight.ToString(), paddingTargets );
		Assert.DoesNotContain( overriddenDark.ToString(), paddingTargets );

		List<string> fillTargets = [ .. changes.Changes.Where( c => c[ "operations" ]![ 0 ]![ "attr" ]!.ToString() == "applied-tokens" ).Select( c => c[ "id" ]!.ToString() ) ];
		Assert.Contains( detachedLight.ToString(), fillTargets );
		Assert.Contains( detachedDark.ToString(), fillTargets );
		Assert.Contains( overriddenLight.ToString(), fillTargets );
		Assert.Contains( overriddenDark.ToString(), fillTargets );

		JsonObject darkDetachedFill = changes.Changes.Single( c => c[ "id" ]!.ToString() == detachedDark.ToString() && c[ "operations" ]![ 0 ]![ "attr" ]!.ToString() == "applied-tokens" );
		Assert.Equal( "Dark.SurfaceContainerHigh", darkDetachedFill[ "operations" ]![ 0 ]![ "val" ]![ "fill" ]!.ToString() );
		Assert.Equal( "#282a2f", darkDetachedFill[ "operations" ]![ 1 ]![ "val" ]![ 0 ]![ "fill-color" ]!.ToString() );
	}

	[Fact]
	public void Push_SkippedComponent_IsNotMarkedApplied()
	{
		PenpotFile file = File();
		(SyncManifest manifest, XamlStyles _) = Linked( file );
		ComponentLink toolbar = manifest.Components.Single( c => c.Style == "HackToolbarStyle" );
		ComponentReport borderless = new( toolbar, StyleValues.BorderBrush, "Transparent", "SurfaceContainerHigh", SyncState.ChangedInCode );
		ComponentReport unknownToken = new( toolbar, StyleValues.Background, "SurfaceContainer", "MissingRole", SyncState.ChangedInCode );

		ChangeSet changes = ChangeBuilder.ForComponents( file, [ borderless, unknownToken ] );

		Assert.Empty( changes.Changes );
		Assert.Empty( changes.AppliedComponents );
		Assert.Contains( changes.Descriptions, d => d.Contains( "skipped BorderBrush" ) );
		Assert.Contains( changes.Descriptions, d => d.Contains( "skipped Background" ) );
	}

	[Fact]
	public void Link_BackfillsNullBaseline_OnceBothSidesAgree()
	{
		JsonObject raw = File().Raw;
		raw[ "data" ]![ "pages-index" ]![ Page.ToString() ]![ "objects" ]![ LightMain.ToString() ]![ "height" ] = 32;
		PenpotFile file = PenpotFile.Parse( raw );
		const string styleXaml = """
			<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
				<Style x:Key="HackToolbarStyle" TargetType="Border">
					<Setter Property="Height" Value="32" />
					<Setter Property="CornerRadius" Value="20" />
				</Style>
			</ResourceDictionary>
			""";
		XamlStyles styles = XamlStyles.FromText( styleXaml );
		ComponentLink toolbar = new()
		{
			Component = "Toolbar", Style = "HackToolbarStyle", File = XamlStyles.HackControlsPath,
			Properties = new() { [ "Height" ] = null, [ "CornerRadius" ] = "16" },
		};
		SyncManifest manifest = new() { Components = [ toolbar ] };

		ComponentSync.Link( manifest, file, _ => styles );

		Assert.Equal( "32", toolbar.Properties[ "Height" ] );
		Assert.Equal( "16", toolbar.Properties[ "CornerRadius" ] );
		List<ComponentReport> reports = ComponentSync.Compare( manifest, file, _ => styles );
		Assert.Equal( SyncState.InSync, reports.Single( r => r.Link == toolbar && r.Property == "Height" ).State );
		Assert.Equal( SyncState.ChangedInCode, reports.Single( r => r.Link == toolbar && r.Property == "CornerRadius" ).State );
	}

	[Fact]
	public void FindThemeDrift_ReportsDarkVariantMismatch()
	{
		PenpotFile inSync = File();
		(SyncManifest manifest, XamlStyles _) = Linked( inSync );
		Assert.Empty( ComponentSync.FindThemeDrift( manifest, inSync ) );

		JsonObject raw = File().Raw;
		JsonObject dark = raw[ "data" ]![ "pages-index" ]![ Page.ToString() ]![ "objects" ]![ DarkMain.ToString() ]!.AsObject();
		dark[ "layout-padding" ]![ "p1" ] = 12;
		dark[ "applied-tokens" ]![ "fill" ] = "Dark.SurfaceContainerHigh";
		PenpotFile drifted = PenpotFile.Parse( raw );

		List<string> drift = ComponentSync.FindThemeDrift( manifest, drifted );

		Assert.Equal( 2, drift.Count );
		Assert.Contains( drift, d => d.Contains( "HackToolbarStyle.Padding" ) && d.Contains( "Dark" ) && d.Contains( "8,12,12,4" ) && d.Contains( "8,4,12,4" ) );
		Assert.Contains( drift, d => d.Contains( "HackToolbarStyle.Background" ) && d.Contains( "Dark" ) && d.Contains( "SurfaceContainerHigh" ) && d.Contains( "SurfaceContainer" ) );
	}

	[Fact]
	public void FindThemeDrift_ReportsMissingDarkVariantMainShape()
	{
		JsonObject raw = File().Raw;
		raw[ "data" ]![ "pages-index" ]![ Page.ToString() ]![ "objects" ]!.AsObject().Remove( DarkMain.ToString() );
		PenpotFile file = PenpotFile.Parse( raw );
		(SyncManifest manifest, XamlStyles _) = Linked( file );

		List<string> drift = ComponentSync.FindThemeDrift( manifest, file );

		Assert.Contains( drift, d => d == "HackToolbarStyle: Dark variant main shape is missing in Penpot" );
	}

	[Fact]
	public void Push_PartialThemeFailure_DoesNotMarkComponentApplied()
	{
		// Case 1: Light fails (missing Light token), Dark succeeds
		JsonObject lightFailRaw = File().Raw;
		lightFailRaw[ "data" ]![ "tokens-lib" ]![ "S-Light" ]![ "tokens" ]!.AsObject().Remove( "Light.SurfaceContainerHigh" );
		PenpotFile lightFailFile = PenpotFile.Parse( lightFailRaw );
		(SyncManifest manifest1, XamlStyles _) = Linked( lightFailFile );
		ComponentLink toolbar1 = manifest1.Components.Single( c => c.Style == "HackToolbarStyle" );
		ComponentReport push1 = new( toolbar1, StyleValues.Background, "SurfaceContainer", "SurfaceContainerHigh", SyncState.ChangedInCode );

		ChangeSet changes1 = ChangeBuilder.ForComponents( lightFailFile, [ push1 ] );

		Assert.Contains( changes1.Changes, c => c[ "id" ]!.ToString() == DarkMain.ToString() );
		Assert.DoesNotContain( changes1.Changes, c => c[ "id" ]!.ToString() == LightMain.ToString() );
		Assert.DoesNotContain( push1, changes1.AppliedComponents );

		// Case 2: Light succeeds, Dark fails (missing Dark main shape)
		JsonObject darkFailRaw = File().Raw;
		darkFailRaw[ "data" ]![ "pages-index" ]![ Page.ToString() ]![ "objects" ]!.AsObject().Remove( DarkMain.ToString() );
		PenpotFile darkFailFile = PenpotFile.Parse( darkFailRaw );
		(SyncManifest manifest2, XamlStyles _) = Linked( darkFailFile );
		ComponentLink toolbar2 = manifest2.Components.Single( c => c.Style == "HackToolbarStyle" );
		ComponentReport push2 = new( toolbar2, StyleValues.Background, "SurfaceContainer", "SurfaceContainerHigh", SyncState.ChangedInCode );

		ChangeSet changes2 = ChangeBuilder.ForComponents( darkFailFile, [ push2 ] );

		Assert.Contains( changes2.Changes, c => c[ "id" ]!.ToString() == LightMain.ToString() );
		Assert.DoesNotContain( changes2.Changes, c => c[ "id" ]!.ToString() == DarkMain.ToString() );
		Assert.DoesNotContain( push2, changes2.AppliedComponents );
	}

	[Fact]
	public void Push_SharedAppliedTokensState_DetachedStrokeFallback_AndSkipsMarkupOrComponentLayers()
	{
		Guid lightFrame = Guid.Parse( "bbbbbbbb-0000-0000-0000-000000000101" );
		Guid darkFrame = Guid.Parse( "bbbbbbbb-0000-0000-0000-000000000102" );
		Guid detachedLight = Guid.Parse( "bbbbbbbb-0000-0000-0000-000000000111" );
		Guid detachedDark = Guid.Parse( "bbbbbbbb-0000-0000-0000-000000000121" );
		Guid boundLight = Guid.Parse( "bbbbbbbb-0000-0000-0000-000000000112" );
		Guid otherCompLight = Guid.Parse( "bbbbbbbb-0000-0000-0000-000000000113" );
		Guid foreignCompId = Guid.Parse( "cccccccc-0000-0000-0000-000000000199" );

		JsonObject raw = BorderedFile( strokeToken: "Outline" ).Raw;
		JsonObject objects = raw[ "data" ]![ "pages-index" ]![ Page.ToString() ]![ "objects" ]!.AsObject();
		static JsonObject BorderlessLayer( Guid id, Guid parent, string name, string theme, Guid? compId = null )
		{
			JsonObject obj = new()
			{
				[ "id" ] = id.ToString(), [ "type" ] = "frame", [ "name" ] = name, [ "parent-id" ] = parent.ToString(),
				[ "fills" ] = new JsonArray( new JsonObject { [ "fill-color" ] = theme == "Light" ? "#eeedf4" : "#1e1f25", [ "fill-opacity" ] = 1 } ),
				[ "strokes" ] = new JsonArray(),
				[ "applied-tokens" ] = new JsonObject { [ "fill" ] = $"{theme}.SurfaceContainer" },
			};
			if( compId is { } cid )
				obj[ "component-id" ] = cid.ToString();
			return obj;
		}
		objects[ lightFrame.ToString() ] = new JsonObject
		{
			[ "id" ] = lightFrame.ToString(), [ "type" ] = "frame", [ "name" ] = "Screen — Light",
			[ "parent-id" ] = Guid.Empty.ToString(), [ "shapes" ] = new JsonArray( otherCompLight.ToString(), boundLight.ToString(), detachedLight.ToString() ),
		};
		objects[ darkFrame.ToString() ] = new JsonObject
		{
			[ "id" ] = darkFrame.ToString(), [ "type" ] = "frame", [ "name" ] = "Screen — Dark",
			[ "parent-id" ] = Guid.Empty.ToString(), [ "shapes" ] = new JsonArray( detachedDark.ToString() ),
		};
		objects[ detachedLight.ToString() ] = BorderlessLayer( detachedLight, lightFrame, "Detached", "Light" );
		objects[ detachedDark.ToString() ] = BorderlessLayer( detachedDark, darkFrame, "Detached", "Dark" );
		objects[ boundLight.ToString() ] = BorderlessLayer( boundLight, lightFrame, "Bound", "Light" );
		objects[ otherCompLight.ToString() ] = BorderlessLayer( otherCompLight, lightFrame, "Foreign", "Light", foreignCompId );

		PenpotFile file = PenpotFile.Parse( raw );
		const string styleXaml = """
			<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
				<Style x:Key="HackToolbarStyle" TargetType="Border">
					<Setter Property="Background" Value="{ThemeResource HackSurfaceContainerBrush}" />
					<Setter Property="BorderBrush" Value="{ThemeResource OutlineBrush}" />
				</Style>
			</ResourceDictionary>
			""";
		(SyncManifest manifest, XamlStyles _) = Linked( file, styleXaml );
		manifest.Frames.Add( new FrameLink
		{
			View = "view.xaml", Frame = "Screen — Light", DarkFrame = "Screen — Dark",
			Elements =
			[
				new ElementLink { Anchor = "tb.detached", Path = "Detached" },
				new ElementLink { Anchor = "tb.bound", Path = "Bound" },
				new ElementLink { Anchor = "tb.foreign", Path = "Foreign" },
			],
		} );
		const string initialView = """
			<Page xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:pp="urn:wyrmpdm:penpot-sync">
				<Grid>
					<Border pp:Id="tb.detached" Style="{StaticResource HackToolbarStyle}" Background="{ThemeResource HackSurfaceContainerBrush}" />
					<Border pp:Id="tb.bound" Style="{StaticResource HackToolbarStyle}" Background="{x:Bind DynamicBg}" BorderBrush="{x:Bind DynamicBorder}" />
					<Border pp:Id="tb.foreign" Style="{StaticResource HackToolbarStyle}" />
				</Grid>
			</Page>
			""";
		Func<Guid, string?> styleOf = LayoutSync.StyleLookup( manifest, file );
		FrameContext initialCtx = LayoutSync.Resolve( manifest, file, _ => XamlView.FromText( initialView ) ).Single().Context!;
		Assert.Empty( LayoutSync.Link( initialCtx, styleOf ) );

		// Edit style (Background -> SurfaceContainerHigh, BorderBrush -> OutlineVariant) and local element (Background -> Primary)
		XamlStyles editedStyles = XamlStyles.FromText( styleXaml
			.Replace( "HackSurfaceContainerBrush", "HackSurfaceContainerHighBrush" )
			.Replace( "OutlineBrush", "OutlineVariantBrush" ) );
		XamlView editedView = XamlView.FromText( initialView.Replace( "Background=\"{ThemeResource HackSurfaceContainerBrush}\"", "Background=\"{ThemeResource PrimaryBrush}\"" ) );
		FrameContext ctx = LayoutSync.Resolve( manifest, file, _ => editedView ).Single().Context!;

		List<ComponentReport> compPushes = ComponentSync.ToPush( ComponentSync.Compare( manifest, file, _ => editedStyles ), Prefer.None );
		List<ElementReport> elemPushes = LayoutSync.ToPush( LayoutSync.Compare( ctx, styleOf ), Prefer.None );

		ChangeSet shared = new();
		ChangeBuilder.ForComponents( file, compPushes, [ ctx ], shared );
		LayoutSync.PushChanges( ctx, file, elemPushes, shared );

		// Detached layer cloned main's stroke template on BorderBrush push AND merged both applied-tokens across ForComponents + PushChanges
		JsonObject detachedLightBorder = shared.Changes.First( c => c[ "id" ]!.ToString() == detachedLight.ToString() );
		JsonObject lightStroke = Assert.Single( detachedLightBorder[ "operations" ]!.AsArray().Single( o => (string?)o![ "attr" ] == "strokes" )![ "val" ]!.AsArray() )!.AsObject();
		Assert.Equal( "#c5c6d0", (string?)lightStroke[ "stroke-color" ] );
		Assert.Equal( 1.0, ShapeFactory.Num( lightStroke[ "stroke-width" ] ) );

		JsonObject detachedLightFinal = shared.Changes.Last( c => c[ "id" ]!.ToString() == detachedLight.ToString() );
		JsonObject lightApplied = detachedLightFinal[ "operations" ]!.AsArray().Single( o => (string?)o![ "attr" ] == "applied-tokens" )![ "val" ]!.AsObject();
		Assert.Equal( "Light.Primary", (string?)lightApplied[ "fill" ] );
		Assert.Equal( "Light.OutlineVariant", (string?)lightApplied[ "stroke-color" ] );

		JsonObject detachedDarkFinal = shared.Changes.Last( c => c[ "id" ]!.ToString() == detachedDark.ToString() );
		JsonObject darkApplied = detachedDarkFinal[ "operations" ]!.AsArray().Single( o => (string?)o![ "attr" ] == "applied-tokens" )![ "val" ]!.AsObject();
		Assert.Equal( "Dark.Primary", (string?)darkApplied[ "fill" ] );
		Assert.Equal( "Dark.OutlineVariant", (string?)darkApplied[ "stroke-color" ] );

		// Markup override ({x:Bind ...}) and foreign component instance were not overwritten by ForComponents
		Assert.DoesNotContain( shared.Changes, c => c[ "id" ]!.ToString() == boundLight.ToString() );
		Assert.DoesNotContain( shared.Changes, c => c[ "id" ]!.ToString() == otherCompLight.ToString() );
	}
}
