using System.Text.Json.Nodes;

using WyrmpdmSync.Penpot;
using WyrmpdmSync.Sync;

using Xunit;

namespace WyrmpdmSync.Tests;

public class LayoutSyncTests
{
	private const string View = """
		<Page
			x:Class="Demo.View"
			xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
			xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
			xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
			xmlns:pp="urn:wyrmpdm:penpot-sync"
			mc:Ignorable="pp">
			<Grid>
				<!--  <Border pp:Id="commented" />  -->
				<StackPanel pp:Id="bar" Orientation="Horizontal" Spacing="8" Padding="8,4,12,4">
					<TextBlock pp:Id="bar.title" Text="Folders" />
					<Button pp:Id="bar.go" Content="Go" Style="{StaticResource FilledButtonStyle}" Command="{x:Bind Go}" />
				</StackPanel>
				<TextBlock pp:Id="bound" Text="{x:Bind Title}" />
			</Grid>
		</Page>
		""";

	private static readonly Guid PageId = Guid.Parse( "33333333-3333-3333-3333-333333333333" );

	/// <summary>
	/// A page with a "Screen — Light" frame: Bar (row flex, gap 8, padding 4/12/4/8) holding a "Title" text and a
	/// "Go" button copy, listed bottom first as Penpot does; and the "Button" component it copies.
	/// </summary>
	private static PenpotFile File( string title = "Folders", int gap = 8, params JsonObject[] extraBarChildren )
	{
		const string frame = "f0000000-0000-0000-0000-000000000001", bar = "f0000000-0000-0000-0000-000000000002",
			text = "f0000000-0000-0000-0000-000000000003", go = "f0000000-0000-0000-0000-000000000004",
			goLabel = "f0000000-0000-0000-0000-000000000005", main = "f0000000-0000-0000-0000-000000000006",
			mainLabel = "f0000000-0000-0000-0000-000000000007", component = "c0000000-0000-0000-0000-000000000001";
		JsonObject Text( string id, string parent, string value ) => new()
		{
			[ "id" ] = id, [ "type" ] = "text", [ "name" ] = value, [ "parent-id" ] = parent, [ "frame-id" ] = parent,
			[ "x" ] = 0, [ "y" ] = 0, [ "width" ] = 40, [ "height" ] = 20,
			[ "content" ] = new JsonObject
			{
				[ "type" ] = "root",
				[ "children" ] = new JsonArray( new JsonObject
				{
					[ "type" ] = "paragraph-set",
					[ "children" ] = new JsonArray( new JsonObject
					{
						[ "type" ] = "paragraph", [ "children" ] = new JsonArray( new JsonObject { [ "text" ] = value, [ "font-size" ] = "14" } ),
					} ),
				} ),
			},
		};
		JsonArray barChildren = [ .. extraBarChildren.Select( c => (JsonNode)c[ "id" ]!.ToString() ), go, text ];
		JsonObject objects = new()
		{
			[ frame ] = new JsonObject { [ "id" ] = frame, [ "type" ] = "frame", [ "name" ] = "Screen — Light", [ "parent-id" ] = Guid.Empty.ToString(), [ "x" ] = 0, [ "y" ] = 0, [ "shapes" ] = new JsonArray( bar ) },
			[ bar ] = new JsonObject
			{
				[ "id" ] = bar, [ "type" ] = "frame", [ "name" ] = "Bar", [ "parent-id" ] = frame, [ "x" ] = 10, [ "y" ] = 20,
				[ "layout" ] = "flex", [ "layout-flex-dir" ] = "row",
				[ "layout-gap" ] = new JsonObject { [ "row-gap" ] = 0, [ "column-gap" ] = gap },
				[ "layout-padding" ] = new JsonObject { [ "p1" ] = 4, [ "p2" ] = 12, [ "p3" ] = 4, [ "p4" ] = 8 },
				// Penpot lists layers bottom first: the title shows first
				[ "shapes" ] = barChildren,
			},
			[ text ] = Text( text, bar, title ),
			[ go ] = new JsonObject
			{
				[ "id" ] = go, [ "type" ] = "frame", [ "name" ] = "Button", [ "parent-id" ] = bar, [ "x" ] = 60, [ "y" ] = 20,
				[ "component-id" ] = component, [ "component-root" ] = true, [ "shape-ref" ] = main, [ "shapes" ] = new JsonArray( goLabel ),
			},
			[ goLabel ] = Text( goLabel, go, "Go" ),
			[ main ] = new JsonObject
			{
				[ "id" ] = main, [ "type" ] = "frame", [ "name" ] = "Button", [ "parent-id" ] = Guid.Empty.ToString(), [ "x" ] = 500, [ "y" ] = 500,
				[ "width" ] = 80, [ "height" ] = 40, [ "component-id" ] = component, [ "component-root" ] = true, [ "main-instance" ] = true,
				[ "variant-id" ] = Guid.NewGuid().ToString(), [ "variant-name" ] = "Light, Filled", [ "shapes" ] = new JsonArray( mainLabel ),
			},
			[ mainLabel ] = Text( mainLabel, main, "Button" ),
		};
		foreach( JsonObject extra in extraBarChildren )
			objects[ extra[ "id" ]!.ToString() ] = extra;
		return PenpotFile.Parse( new JsonObject
		{
			[ "id" ] = Fakes.FileId.ToString(), [ "name" ] = "test", [ "revn" ] = 1, [ "vern" ] = 0,
			[ "data" ] = new JsonObject
			{
				[ "pages" ] = new JsonArray( PageId.ToString() ),
				[ "pages-index" ] = new JsonObject { [ PageId.ToString() ] = new JsonObject { [ "name" ] = "Screen", [ "objects" ] = objects } },
				[ "components" ] = new JsonObject
				{
					[ "c" ] = new JsonObject
					{
						[ "id" ] = component, [ "name" ] = "Button", [ "main-instance-id" ] = main, [ "main-instance-page" ] = PageId.ToString(),
						[ "variant-properties" ] = new JsonArray( new JsonObject { [ "name" ] = "Theme", [ "value" ] = "Light" }, new JsonObject { [ "name" ] = "Type", [ "value" ] = "Filled" } ),
					},
				},
			},
		} );
	}

	private static (SyncManifest, FrameContext, Func<Guid, string?>) Linked( PenpotFile file, string view = View, bool children = false )
	{
		SyncManifest manifest = new() { Components = [ .. ComponentMap.Defaults ] };
		manifest.Frames.Add( new FrameLink
		{
			View = "view.xaml", Frame = "Screen — Light",
			Elements =
			[
				new ElementLink { Anchor = "bar", Path = "Bar", Children = children },
				new ElementLink { Anchor = "bar.title", Path = "Bar/Folders" },
				new ElementLink { Anchor = "bar.go", Path = "Bar/Button" },
			],
		} );
		XamlView xaml = XamlView.FromText( view );
		FrameContext ctx = LayoutSync.Resolve( manifest, file, _ => xaml ).Single().Context!;
		Func<Guid, string?> styleOf = LayoutSync.StyleLookup( manifest, file );
		Assert.Empty( LayoutSync.Link( ctx, styleOf ) );
		return (manifest, ctx, styleOf);
	}

	[Fact]
	public void View_FindsAnchors_ParentsAndAttributePositions()
	{
		XamlView view = XamlView.FromText( View );
		Assert.Equal( [ "bar", "bar.title", "bar.go", "bound" ], view.Anchors.Keys );
		XamlElement go = view.Anchors[ "bar.go" ];
		Assert.Equal( "bar", go.AnchoredParent!.Anchor );
		SourceLiteral spacing = view.Anchors[ "bar" ].Attributes[ "Spacing" ].Value;
		Assert.Equal( "8", View.Substring( spacing.Start, spacing.Length ) );

		view.Apply( [ view.SetAttribute( view.Anchors[ "bar" ], "Spacing", "12" ), view.SetAttribute( view.Anchors[ "bar.title" ], "Foreground", "{ThemeResource PrimaryBrush}" ) ] );
		Assert.Contains( "Spacing=\"12\"", view.Text );
		Assert.Contains( "<TextBlock Foreground=\"{ThemeResource PrimaryBrush}\" pp:Id=\"bar.title\"", view.Text );
	}

	[Fact]
	public void View_DeclaresThePpNamespace_WhenMissing()
	{
		XamlView view = XamlView.FromText( "<Page xmlns:mc=\"x\" mc:Ignorable=\"d\">\n\t<Grid />\n</Page>" );
		Assert.False( view.DeclaresNamespace );
		view.Apply( view.DeclareNamespace() );
		Assert.True( view.DeclaresNamespace );
		Assert.Contains( "mc:Ignorable=\"d pp\"", view.Text );
	}

	[Fact]
	public void ShapeTree_ReadsVisualOrder_AndResolvesPaths()
	{
		PenpotFile file = File();
		JsonObject objects = file.Pages[ 0 ].Objects;
		JsonObject frame = ShapeTree.Frame( file.Pages[ 0 ], "Screen — Light" )!;
		JsonObject bar = ShapeTree.Resolve( objects, frame, "Bar" )!;
		Assert.Equal( [ "Folders", "Button" ], ShapeTree.Children( objects, bar ).Select( c => c[ "name" ]!.ToString() ) );
		JsonObject go = ShapeTree.Resolve( objects, frame, "Bar/Button" )!;
		Assert.Equal( "Bar/Button", ShapeTree.PathOf( objects, frame, go ) );
		Assert.Equal( "Go", ShapeTree.Text( objects[ "f0000000-0000-0000-0000-000000000005" ]!.AsObject() ) );
		Assert.Null( ShapeTree.Resolve( objects, frame, "Bar/Button#2" ) );
	}

	[Fact]
	public void Link_RecordsPropertiesBothSidesDefine_SkippingBindings()
	{
		(SyncManifest manifest, FrameContext ctx, Func<Guid, string?> styleOf) = Linked( File() );
		ElementLink bar = ctx.Link.Elements[ 0 ], title = ctx.Link.Elements[ 1 ], go = ctx.Link.Elements[ 2 ];
		Assert.Equal( [ "Padding", "Spacing" ], bar.Properties.Keys );
		Assert.Equal( [ "Text" ], title.Properties.Keys );
		Assert.Equal( [ "Text", "Style" ], go.Properties.Keys );
		Assert.All( LayoutSync.Compare( ctx, styleOf ), r => Assert.Equal( SyncState.InSync, r.State ) );
	}

	[Fact]
	public void Pull_WritesPenpotChangesIntoTheView()
	{
		(SyncManifest manifest, FrameContext _, Func<Guid, string?> _) = Linked( File() );
		XamlView xaml = XamlView.FromText( View );
		PenpotFile edited = File( title: "Folder tree", gap: 12 );
		FrameContext ctx = LayoutSync.Resolve( manifest, edited, _ => xaml ).Single().Context!;
		List<ElementReport> pulled = LayoutSync.ToPull( LayoutSync.Compare( ctx, LayoutSync.StyleLookup( manifest, edited ) ), Prefer.None );
		Assert.Equal( [ "bar.Spacing", "bar.title.Text" ], pulled.Select( r => $"{r.Element.Anchor}.{r.Property}" ) );
		xaml.Apply( LayoutSync.PullEdits( ctx, pulled ) );
		Assert.Contains( "Spacing=\"12\"", xaml.Text );
		Assert.Contains( "Text=\"Folder tree\"", xaml.Text );
		Assert.Contains( "Command=\"{x:Bind Go}\"", xaml.Text );
	}

	[Fact]
	public void Push_WritesGapAndText_ToTheLayers()
	{
		PenpotFile file = File();
		(SyncManifest manifest, FrameContext _, Func<Guid, string?> styleOf) = Linked( file );
		XamlView edited = XamlView.FromText( View.Replace( "Spacing=\"8\"", "Spacing=\"12\"" ).Replace( "Content=\"Go\"", "Content=\"Go now\"" ) );
		FrameContext ctx = LayoutSync.Resolve( manifest, file, _ => edited ).Single().Context!;
		ChangeSet set = new();
		LayoutSync.PushChanges( ctx, file, LayoutSync.ToPush( LayoutSync.Compare( ctx, styleOf ), Prefer.None ), set );

		JsonObject gap = set.Changes.Single( c => c[ "id" ]!.ToString() == "f0000000-0000-0000-0000-000000000002" );
		Assert.Equal( 12.0, (double)gap[ "operations" ]![ 0 ]![ "val" ]![ "column-gap" ]! );
		// the button's label is its text layer
		JsonObject text = set.Changes.Single( c => c[ "id" ]!.ToString() == "f0000000-0000-0000-0000-000000000005" );
		Assert.Equal( "Go now", text[ "operations" ]![ 0 ]![ "val" ]![ "children" ]![ 0 ]![ "children" ]![ 0 ]![ "children" ]![ 0 ]![ "text" ]!.ToString() );
		Assert.Equal( "position-data", text[ "operations" ]![ 1 ]![ "attr" ]!.ToString() );
	}

	[Fact]
	public void NewLayerInPenpot_BecomesMarkedXaml_InTheRightPlace()
	{
		static JsonObject Hint() => new()
		{
			[ "id" ] = "f0000000-0000-0000-0000-0000000000aa", [ "type" ] = "text", [ "name" ] = "Hint", [ "parent-id" ] = "f0000000-0000-0000-0000-000000000002",
			[ "applied-tokens" ] = new JsonObject { [ "fill" ] = "Light.OnSurfaceVariant" },
			[ "content" ] = new JsonObject { [ "type" ] = "root", [ "children" ] = new JsonArray( new JsonObject { [ "children" ] = new JsonArray( new JsonObject { [ "children" ] = new JsonArray( new JsonObject { [ "text" ] = "3 files" } ) } ) } ) },
		};
		// listed first = shown last (after the button)
		PenpotFile file = File( extraBarChildren: Hint() );
		(SyncManifest _, FrameContext ctx, Func<Guid, string?> styleOf) = Linked( file, children: true );
		List<string> notes = [];
		LayoutCreate.NewXaml added = Assert.Single( LayoutCreate.FromPenpot( ctx, file, styleOf, notes ) );
		Assert.Empty( notes );
		ctx.View.Apply( [ added.Edit ] );
		string text = ctx.View.Text;
		Assert.Contains( "<TextBlock pp:Id=\"bar.hint\" Foreground=\"{ThemeResource OnSurfaceVariantBrush}\" Text=\"3 files\" />", text );
		Assert.True( text.IndexOf( "bar.hint", StringComparison.Ordinal ) > text.IndexOf( "bar.go", StringComparison.Ordinal ) );
		Assert.True( text.IndexOf( "bar.hint", StringComparison.Ordinal ) < text.IndexOf( "</StackPanel>", StringComparison.Ordinal ) );
		Assert.Equal( "f0000000-0000-0000-0000-0000000000aa", Assert.Single( added.Links ).Shape.ToString() );
	}

	[Fact]
	public void NewMarkedXaml_BecomesAComponentCopy_WithoutVariantFields()
	{
		PenpotFile file = File();
		string view = View.Replace( "Command=\"{x:Bind Go}\" />", "Command=\"{x:Bind Go}\" />\n\t\t\t\t<Button pp:Id=\"bar.stop\" Content=\"Stop\" Style=\"{StaticResource FilledButtonStyle}\" />" );
		(SyncManifest manifest, FrameContext ctx, Func<Guid, string?> _) = Linked( file, view, children: true );
		List<string> notes = [];
		LayoutCreate.NewLayer added = Assert.Single( LayoutCreate.FromCode( ctx, file, manifest, notes ) );

		List<JsonObject> adds = added.Changes;
		JsonObject root = adds[ 0 ][ "obj" ]!.AsObject();
		Assert.Equal( "f0000000-0000-0000-0000-000000000006", root[ "shape-ref" ]!.ToString() );
		Assert.Equal( "f0000000-0000-0000-0000-000000000002", adds[ 0 ][ "parentId" ]!.ToString() );
		Assert.True( root[ "component-root" ]!.GetValue<bool>() );
		Assert.False( root.ContainsKey( "main-instance" ) || root.ContainsKey( "variant-id" ) || root.ContainsKey( "variant-name" ) );
		// after the title and the Go button: the last of three, so first in Penpot's bottom-first list
		Assert.Equal( 0, (int)adds[ 0 ][ "index" ]! );
		JsonObject label = adds[ 1 ][ "obj" ]!.AsObject();
		Assert.Equal( "Stop", ShapeTree.Text( label ) );
		Assert.Equal( root[ "id" ]!.ToString(), label[ "parent-id" ]!.ToString() );
		Assert.Equal( "Stop", added.Link.Properties[ "Text" ] );
	}

	[Fact]
	public void TransitWriter_KeepsTextValuesAsStrings_AndRecordsTagged()
	{
		JsonObject op = new()
		{
			[ "type" ] = "set", [ "attr" ] = "content",
			[ "val" ] = new JsonObject { [ "type" ] = "root", [ "children" ] = new JsonArray( new JsonObject { [ "type" ] = "paragraph-set" } ) },
		};
		string text = TransitWriter.Encode( op );
		Assert.Contains( "\"~:type\",\"root\"", text );
		Assert.Contains( "\"~:type\",\"paragraph-set\"", text );
		Assert.Contains( "\"~:type\",\"~:set\"", text );

		JsonObject shape = new() { [ "~tag" ] = "shape", [ "type" ] = "frame", [ "transform" ] = ShapeFactory.Matrix(), [ "touched" ] = new JsonArray( "fill-group" ) };
		string encoded = TransitWriter.Encode( shape );
		Assert.StartsWith( "[\"~#shape\",[\"^ \"", encoded );
		Assert.Contains( "[\"~#matrix\",[\"^ \",\"~:a\",1", encoded );
		Assert.Contains( "\"~:touched\",[\"~#set\",[\"~:fill-group\"]]", encoded );
		Assert.DoesNotContain( "~tag", encoded );
	}
}
