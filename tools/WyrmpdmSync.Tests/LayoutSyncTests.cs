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

	[Fact]
	public void NewComponentLayers_GenerateMatchingXamlTags()
	{
		(string Component, string VariantKey, string VariantVal, string Style, string ExpectedTag, string? ExpectedLabelAttr)[] cases =
		[
			( "Icon button", "State", "Standard", "PaneIconButtonStyle", "Button", "Content" ),
			( "Icon button", "State", "Selected", "PaneIconToggleButtonStyle", "ToggleButton", "Content" ),
			( "Text field", "Kind", "Select", "HackOutlinedComboBoxStyle", "ComboBox", null ),
			( "Text field", "Kind", "Text", "OutlinedTextBoxStyle", "TextBox", null ),
			( "Chip", "Type", "Filter", "FilterChipStyle", "utu:Chip", "Content" ),
			( "File table row", "Kind", "Row", "CompactTableViewStyle", "tv:TableView", null ),
			( "File table row", "Kind", "Header", "CompactTableViewColumnHeaderStyle", "tv:TableViewColumnHeader", null ),
			( "Navigation item", "Selected", "No", "HackNavRailItemStyle", "common:NavRailItem", "Label" ),
			( "Pane card", "Kind", "Tinted", "HackPaneTintedStyle", "Border", null ),
		];

		const string barId = "f0000000-0000-0000-0000-000000000002";
		JsonObject raw = File().Raw;
		JsonObject objects = raw[ "data" ]![ "pages-index" ]![ PageId.ToString() ]![ "objects" ]!.AsObject();
		JsonObject components = raw[ "data" ]![ "components" ]!.AsObject();
		JsonArray barShapes = objects[ barId ]![ "shapes" ]!.AsArray();
		List<(Guid CopyId, string Style, string ExpectedTag, string? ExpectedLabelAttr)> expected = [];

		for( int i = 0; i < cases.Length; i++ )
		{
			(string component, string variantKey, string variantVal, string style, string expectedTag, string? expectedLabelAttr) = cases[ i ];
			string compId = $"c1000000-0000-0000-0000-00000000000{i + 1}";
			string mainId = $"a1000000-0000-0000-0000-00000000000{i + 1}";
			Guid copyId = Guid.Parse( $"b1000000-0000-0000-0000-00000000000{i + 1}" );
			string labelId = $"d1000000-0000-0000-0000-00000000000{i + 1}";

			components[ $"comp-{i}" ] = new JsonObject
			{
				[ "id" ] = compId, [ "name" ] = component, [ "main-instance-id" ] = mainId, [ "main-instance-page" ] = PageId.ToString(),
				[ "variant-properties" ] = new JsonArray(
					new JsonObject { [ "name" ] = "Theme", [ "value" ] = "Light" },
					new JsonObject { [ "name" ] = variantKey, [ "value" ] = variantVal } ),
			};
			objects[ mainId ] = new JsonObject
			{
				[ "id" ] = mainId, [ "type" ] = "frame", [ "name" ] = component, [ "parent-id" ] = Guid.Empty.ToString(),
				[ "component-id" ] = compId, [ "component-root" ] = true, [ "main-instance" ] = true,
			};
			objects[ copyId.ToString() ] = new JsonObject
			{
				[ "id" ] = copyId.ToString(), [ "type" ] = "frame", [ "name" ] = $"Item{i + 1}", [ "parent-id" ] = barId,
				[ "component-id" ] = compId, [ "component-root" ] = true, [ "shape-ref" ] = mainId,
				[ "shapes" ] = new JsonArray( labelId ),
			};
			objects[ labelId ] = new JsonObject
			{
				[ "id" ] = labelId, [ "type" ] = "text", [ "name" ] = "Label", [ "parent-id" ] = copyId.ToString(),
				[ "content" ] = new JsonObject
				{
					[ "type" ] = "root",
					[ "children" ] = new JsonArray( new JsonObject
					{
						[ "type" ] = "paragraph-set",
						[ "children" ] = new JsonArray( new JsonObject
						{
							[ "type" ] = "paragraph",
							[ "children" ] = new JsonArray( new JsonObject { [ "text" ] = "Sample" } ),
						} ),
					} ),
				},
			};
			barShapes.Insert( 0, copyId.ToString() );
			expected.Add( (copyId, style, expectedTag, expectedLabelAttr) );
		}

		PenpotFile file = PenpotFile.Parse( raw );
		(SyncManifest _, FrameContext ctx, Func<Guid, string?> styleOf) = Linked( file, children: true );
		List<string> notes = [];
		List<LayoutCreate.NewXaml> added = LayoutCreate.FromPenpot( ctx, file, styleOf, notes );

		Assert.Empty( notes );
		Assert.Equal( expected.Count, added.Count );
		foreach( (Guid copyId, string style, string expectedTag, string? expectedLabelAttr) in expected )
		{
			LayoutCreate.NewXaml item = added.Single( a => a.Links[ 0 ].Shape == copyId );
			string snippet = item.Edit.Text;
			Assert.Contains( $"<{expectedTag} ", snippet );
			Assert.Contains( $"Style=\"{{StaticResource {style}}}\"", snippet );
			if( expectedLabelAttr is not null )
			{
				Assert.Contains( $"{expectedLabelAttr}=\"Sample\"", snippet );
				if( expectedLabelAttr != "Content" )
					Assert.DoesNotContain( "Content=", snippet );
			}
			else
			{
				Assert.DoesNotContain( "Content=", snippet );
				Assert.DoesNotContain( "Label=", snippet );
			}
		}
	}

	[Fact]
	public void NewMarkedXaml_SerializesBorder_AndOnlyRecordsWrittenProperties()
	{
		JsonObject raw = File().Raw;
		raw[ "data" ]![ "tokens-lib" ] = new JsonObject
		{
			[ "S-Light" ] = new JsonObject
			{
				[ "id" ] = Guid.NewGuid().ToString(), [ "name" ] = "HackPDM colors/Light",
				[ "tokens" ] = new JsonObject
				{
					[ "Light.SurfaceContainer" ] = new JsonObject { [ "id" ] = Guid.NewGuid().ToString(), [ "name" ] = "Light.SurfaceContainer", [ "type" ] = "color", [ "value" ] = "#EEEDF4" },
					[ "Light.OutlineVariant" ] = new JsonObject { [ "id" ] = Guid.NewGuid().ToString(), [ "name" ] = "Light.OutlineVariant", [ "type" ] = "color", [ "value" ] = "#C5C6D0" },
				},
			},
		};
		PenpotFile file = PenpotFile.Parse( raw );
		string view = View.Replace( "Command=\"{x:Bind Go}\" />",
			"""
			Command="{x:Bind Go}" />
						<Border pp:Id="bar.card" Background="{ThemeResource HackSurfaceContainerBrush}" BorderBrush="{ThemeResource OutlineVariantBrush}" BorderThickness="2" CornerRadius="8" />
						<Button pp:Id="bar.stop" Content="Stop" Style="{StaticResource FilledButtonStyle}" Padding="16" CornerRadius="12" Width="120" />
			""" );
		(SyncManifest manifest, FrameContext ctx, Func<Guid, string?> _) = Linked( file, view, children: true );
		List<string> notes = [];

		List<LayoutCreate.NewLayer> added = LayoutCreate.FromCode( ctx, file, manifest, notes );

		Assert.Empty( notes );
		Assert.Equal( 2, added.Count );

		LayoutCreate.NewLayer card = added.Single( a => a.Link.Anchor == "bar.card" );
		JsonObject cardObj = card.Changes[ 0 ][ "obj" ]!.AsObject();
		Assert.Equal( "Light.SurfaceContainer", cardObj[ "applied-tokens" ]![ "fill" ]!.ToString() );
		Assert.Equal( "Light.OutlineVariant", cardObj[ "applied-tokens" ]![ "stroke-color" ]!.ToString() );
		JsonObject stroke = Assert.Single( cardObj[ "strokes" ]!.AsArray() )!.AsObject();
		Assert.Equal( "#c5c6d0", stroke[ "stroke-color" ]!.ToString() );
		Assert.Equal( 1.0, ShapeFactory.Num( stroke[ "stroke-opacity" ] ) );
		Assert.Equal( 2.0, ShapeFactory.Num( stroke[ "stroke-width" ] ) );
		Assert.Equal( "solid", stroke[ "stroke-style" ]!.ToString() );
		Assert.Equal( "inner", stroke[ "stroke-alignment" ]!.ToString() );
		Assert.Equal( 8.0, (double)cardObj[ "r1" ]! );
		Assert.Equal( "SurfaceContainer", card.Link.Properties[ "Background" ] );
		Assert.Equal( "OutlineVariant", card.Link.Properties[ "BorderBrush" ] );
		Assert.Equal( "8", card.Link.Properties[ "CornerRadius" ] );

		LayoutCreate.NewLayer stop = added.Single( a => a.Link.Anchor == "bar.stop" );
		Assert.Equal( "Stop", stop.Link.Properties[ "Text" ] );
		Assert.Equal( "FilledButtonStyle", stop.Link.Properties[ "Style" ] );
		Assert.False( stop.Link.Properties.ContainsKey( "Padding" ) );
		Assert.False( stop.Link.Properties.ContainsKey( "CornerRadius" ) );
		Assert.False( stop.Link.Properties.ContainsKey( "Width" ) );
	}

	[Fact]
	public void Push_BackgroundAndBorderBrush_AndRowColSpacing_MergeState_AndSkippedTextNotApplied()
	{
		const string frameId = "f1000000-0000-0000-0000-000000000001";
		const string panelId = "f1000000-0000-0000-0000-000000000002";
		const string richId = "f1000000-0000-0000-0000-000000000003";

		static JsonObject Token( string name, string value ) => new() { [ "id" ] = Guid.NewGuid().ToString(), [ "name" ] = name, [ "type" ] = "color", [ "value" ] = value };
		JsonObject objects = new()
		{
			[ frameId ] = new JsonObject { [ "id" ] = frameId, [ "type" ] = "frame", [ "name" ] = "Screen — Light", [ "parent-id" ] = Guid.Empty.ToString(), [ "shapes" ] = new JsonArray( panelId ) },
			[ panelId ] = new JsonObject
			{
				[ "id" ] = panelId, [ "type" ] = "frame", [ "name" ] = "Panel", [ "parent-id" ] = frameId,
				[ "layout" ] = "grid", [ "layout-gap" ] = new JsonObject { [ "row-gap" ] = 4, [ "column-gap" ] = 8 },
				[ "fills" ] = new JsonArray( new JsonObject { [ "fill-color" ] = "#eeedf4", [ "fill-opacity" ] = 1 } ),
				[ "strokes" ] = new JsonArray( new JsonObject { [ "stroke-color" ] = "#757780", [ "stroke-opacity" ] = 1, [ "stroke-width" ] = 1 } ),
				[ "applied-tokens" ] = new JsonObject { [ "fill" ] = "Light.SurfaceContainer", [ "stroke-color" ] = "Light.Outline" },
				[ "shapes" ] = new JsonArray( richId ),
			},
			[ richId ] = new JsonObject
			{
				[ "id" ] = richId, [ "type" ] = "text", [ "name" ] = "Rich", [ "parent-id" ] = panelId,
				[ "content" ] = new JsonObject
				{
					[ "type" ] = "root",
					[ "children" ] = new JsonArray( new JsonObject
					{
						[ "type" ] = "paragraph-set",
						[ "children" ] = new JsonArray( new JsonObject
						{
							[ "type" ] = "paragraph",
							[ "children" ] = new JsonArray(
								new JsonObject { [ "text" ] = "Hello ", [ "font-weight" ] = "700" },
								new JsonObject { [ "text" ] = "world", [ "font-weight" ] = "400" } ),
						} ),
					} ),
				},
			},
		};
		PenpotFile file = PenpotFile.Parse( new JsonObject
		{
			[ "id" ] = Fakes.FileId.ToString(), [ "name" ] = "test", [ "revn" ] = 1, [ "vern" ] = 0,
			[ "data" ] = new JsonObject
			{
				[ "pages" ] = new JsonArray( PageId.ToString() ),
				[ "pages-index" ] = new JsonObject { [ PageId.ToString() ] = new JsonObject { [ "name" ] = "Screen", [ "objects" ] = objects } },
				[ "tokens-lib" ] = new JsonObject
				{
					[ "S-Light" ] = new JsonObject
					{
						[ "id" ] = Guid.NewGuid().ToString(), [ "name" ] = "HackPDM colors/Light",
						[ "tokens" ] = new JsonObject
						{
							[ "Light.SurfaceContainer" ] = Token( "Light.SurfaceContainer", "#EEEDF4" ),
							[ "Light.SurfaceContainerHigh" ] = Token( "Light.SurfaceContainerHigh", "#E8E7EF" ),
							[ "Light.Outline" ] = Token( "Light.Outline", "#757780" ),
							[ "Light.OutlineVariant" ] = Token( "Light.OutlineVariant", "#C5C6D0" ),
						},
					},
				},
			},
		} );

		const string initialView = """
			<Page xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:pp="urn:wyrmpdm:penpot-sync">
				<Grid pp:Id="panel" RowSpacing="4" ColumnSpacing="8" Background="{ThemeResource HackSurfaceContainerBrush}" BorderBrush="{ThemeResource OutlineBrush}">
					<TextBlock pp:Id="rich" Text="Hello world" />
				</Grid>
			</Page>
			""";
		SyncManifest manifest = new() { Components = [ .. ComponentMap.Defaults ] };
		manifest.Frames.Add( new FrameLink
		{
			View = "view.xaml", Frame = "Screen — Light",
			Elements =
			[
				new ElementLink { Anchor = "panel", Path = "Panel" },
				new ElementLink { Anchor = "rich", Path = "Panel/Rich" },
			],
		} );
		Func<Guid, string?> styleOf = LayoutSync.StyleLookup( manifest, file );
		FrameContext linkedCtx = LayoutSync.Resolve( manifest, file, _ => XamlView.FromText( initialView ) ).Single().Context!;
		Assert.Empty( LayoutSync.Link( linkedCtx, styleOf ) );

		XamlView edited = XamlView.FromText( initialView
			.Replace( "RowSpacing=\"4\"", "RowSpacing=\"12\"" )
			.Replace( "ColumnSpacing=\"8\"", "ColumnSpacing=\"20\"" )
			.Replace( "HackSurfaceContainerBrush", "HackSurfaceContainerHighBrush" )
			.Replace( "OutlineBrush", "OutlineVariantBrush" )
			.Replace( "Text=\"Hello world\"", "Text=\"Updated\"" ) );
		FrameContext ctx = LayoutSync.Resolve( manifest, file, _ => edited ).Single().Context!;
		List<ElementReport> pushes = LayoutSync.ToPush( LayoutSync.Compare( ctx, styleOf ), Prefer.None );
		Assert.Equal( 5, pushes.Count );

		ChangeSet set = new();
		LayoutSync.PushChanges( ctx, file, pushes, set );

		JsonObject finalTokens = set.Changes.Last( c => c[ "id" ]!.ToString() == panelId && c[ "operations" ]![ 0 ]![ "attr" ]!.ToString() == "applied-tokens" )
			[ "operations" ]![ 0 ]![ "val" ]!.AsObject();
		Assert.Equal( "Light.SurfaceContainerHigh", finalTokens[ "fill" ]!.ToString() );
		Assert.Equal( "Light.OutlineVariant", finalTokens[ "stroke-color" ]!.ToString() );

		JsonObject finalGap = set.Changes.Last( c => c[ "id" ]!.ToString() == panelId && c[ "operations" ]![ 0 ]![ "attr" ]!.ToString() == "layout-gap" )
			[ "operations" ]![ 0 ]![ "val" ]!.AsObject();
		Assert.Equal( 12.0, (double)finalGap[ "row-gap" ]! );
		Assert.Equal( 20.0, (double)finalGap[ "column-gap" ]! );

		Assert.DoesNotContain( set.Changes, c => c[ "id" ]!.ToString() == richId );
		Assert.Equal( 4, set.AppliedElements.Count );
		Assert.DoesNotContain( set.AppliedElements, r => r.Element.Anchor == "rich" );
		Assert.Contains( set.Descriptions, d => d.Contains( "skipped rich Text" ) );
	}

	[Fact]
	public void Link_BackfillsNullBaseline_AndFindDriftDetectsDarkAndDetachedStyleDrift()
	{
		// Part 1: LayoutSync.Link backfills a null baseline once both sides agree, keeping non-null baselines
		PenpotFile baseFile = File();
		SyncManifest backfillManifest = new() { Components = [ .. ComponentMap.Defaults ] };
		ElementLink barLink = new()
		{
			Anchor = "bar", Path = "Bar",
			Properties = new() { [ "Spacing" ] = null, [ "Padding" ] = "1,1,1,1" },
		};
		backfillManifest.Frames.Add( new FrameLink { View = "view.xaml", Frame = "Screen — Light", Elements = [ barLink ] } );
		FrameContext backfillCtx = LayoutSync.Resolve( backfillManifest, baseFile, _ => XamlView.FromText( View ) ).Single().Context!;
		Assert.Empty( LayoutSync.Link( backfillCtx, LayoutSync.StyleLookup( backfillManifest, baseFile ) ) );
		Assert.Equal( "8", barLink.Properties[ "Spacing" ] );
		Assert.Equal( "1,1,1,1", barLink.Properties[ "Padding" ] );

		// Part 2: LayoutSync.FindDrift reports (a) missing Dark twin, (b) Dark twin mismatch, (c) detached styled frame drift (ignoring inline overrides)
		const string lightFrame = "f2000000-0000-0000-0000-000000000001", darkFrame = "f2000000-0000-0000-0000-000000000002";
		const string lightOnly = "f2000000-0000-0000-0000-000000000011";
		const string lightRow = "f2000000-0000-0000-0000-000000000012", darkRow = "f2000000-0000-0000-0000-000000000022";
		const string lightCard = "f2000000-0000-0000-0000-000000000013", darkCard = "f2000000-0000-0000-0000-000000000023";

		JsonObject objects = new()
		{
			[ lightFrame ] = new JsonObject
			{
				[ "id" ] = lightFrame, [ "type" ] = "frame", [ "name" ] = "Screen — Light", [ "parent-id" ] = Guid.Empty.ToString(),
				[ "shapes" ] = new JsonArray( lightCard, lightRow, lightOnly ),
			},
			[ darkFrame ] = new JsonObject
			{
				[ "id" ] = darkFrame, [ "type" ] = "frame", [ "name" ] = "Screen — Dark", [ "parent-id" ] = Guid.Empty.ToString(),
				[ "shapes" ] = new JsonArray( darkCard, darkRow ),
			},
			[ lightOnly ] = new JsonObject
			{
				[ "id" ] = lightOnly, [ "type" ] = "text", [ "name" ] = "LightOnly", [ "parent-id" ] = lightFrame,
				[ "content" ] = new JsonObject { [ "type" ] = "root", [ "children" ] = new JsonArray( new JsonObject { [ "children" ] = new JsonArray( new JsonObject { [ "children" ] = new JsonArray( new JsonObject { [ "text" ] = "Only Light" } ) } ) } ) },
			},
			[ lightRow ] = new JsonObject
			{
				[ "id" ] = lightRow, [ "type" ] = "frame", [ "name" ] = "Row", [ "parent-id" ] = lightFrame,
				[ "layout" ] = "flex", [ "layout-flex-dir" ] = "row",
				[ "layout-gap" ] = new JsonObject { [ "row-gap" ] = 0, [ "column-gap" ] = 8 },
			},
			[ darkRow ] = new JsonObject
			{
				[ "id" ] = darkRow, [ "type" ] = "frame", [ "name" ] = "Row", [ "parent-id" ] = darkFrame,
				[ "layout" ] = "flex", [ "layout-flex-dir" ] = "row",
				[ "layout-gap" ] = new JsonObject { [ "row-gap" ] = 0, [ "column-gap" ] = 16 },
			},
			[ lightCard ] = new JsonObject
			{
				[ "id" ] = lightCard, [ "type" ] = "frame", [ "name" ] = "Card", [ "parent-id" ] = lightFrame,
				[ "r1" ] = 16, [ "r2" ] = 16, [ "r3" ] = 16, [ "r4" ] = 16,
				[ "layout-padding" ] = new JsonObject { [ "p1" ] = 20, [ "p2" ] = 20, [ "p3" ] = 20, [ "p4" ] = 20 },
				[ "applied-tokens" ] = new JsonObject { [ "fill" ] = "Light.SurfaceContainerHigh" },
			},
			[ darkCard ] = new JsonObject
			{
				[ "id" ] = darkCard, [ "type" ] = "frame", [ "name" ] = "Card", [ "parent-id" ] = darkFrame,
				[ "r1" ] = 8, [ "r2" ] = 8, [ "r3" ] = 8, [ "r4" ] = 8,
				[ "layout-padding" ] = new JsonObject { [ "p1" ] = 20, [ "p2" ] = 20, [ "p3" ] = 20, [ "p4" ] = 20 },
				[ "applied-tokens" ] = new JsonObject { [ "fill" ] = "Dark.SurfaceContainer" },
			},
		};
		PenpotFile driftFile = PenpotFile.Parse( new JsonObject
		{
			[ "id" ] = Fakes.FileId.ToString(), [ "name" ] = "test", [ "revn" ] = 1, [ "vern" ] = 0,
			[ "data" ] = new JsonObject
			{
				[ "pages" ] = new JsonArray( PageId.ToString() ),
				[ "pages-index" ] = new JsonObject { [ PageId.ToString() ] = new JsonObject { [ "name" ] = "Screen", [ "objects" ] = objects } },
			},
		} );

		const string driftView = """
			<Page xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:pp="urn:wyrmpdm:penpot-sync">
				<Grid>
					<TextBlock pp:Id="el.missingDark" Text="Only Light" />
					<StackPanel pp:Id="el.mismatchDark" Orientation="Horizontal" Spacing="8" />
					<Border pp:Id="el.detached" Style="{StaticResource HackToolbarStyle}" Padding="20" />
				</Grid>
			</Page>
			""";
		SyncManifest driftManifest = new()
		{
			Components =
			[
				new ComponentLink
				{
					Component = "Toolbar", Style = "HackToolbarStyle", File = XamlStyles.HackControlsPath,
					Properties = new() { [ "Background" ] = "SurfaceContainer", [ "Padding" ] = "8,4,12,4", [ "CornerRadius" ] = "16" },
				},
			],
			Frames =
			[
				new FrameLink
				{
					View = "view.xaml", Frame = "Screen — Light", DarkFrame = "Screen — Dark",
					Elements =
					[
						new ElementLink { Anchor = "el.missingDark", Path = "LightOnly" },
						new ElementLink { Anchor = "el.mismatchDark", Path = "Row" },
						new ElementLink { Anchor = "el.detached", Path = "Card" },
					],
				},
			],
		};
		Func<Guid, string?> styleOf = LayoutSync.StyleLookup( driftManifest, driftFile );
		FrameContext driftCtx = LayoutSync.Resolve( driftManifest, driftFile, _ => XamlView.FromText( driftView ) ).Single().Context!;
		Assert.Empty( LayoutSync.Link( driftCtx, styleOf ) );

		List<string> drift = LayoutSync.FindDrift( driftCtx, driftManifest, styleOf );

		Assert.Equal( 4, drift.Count );
		Assert.Contains( drift, d => d.Contains( "el.missingDark" ) && d.Contains( "missing Dark twin" ) );
		Assert.Contains( drift, d => d.Contains( "el.mismatchDark.Spacing" ) && d.Contains( "16" ) && d.Contains( "8" ) );
		Assert.Contains( drift, d => d.Contains( "el.detached.Background" ) && d.Contains( "SurfaceContainerHigh" ) && d.Contains( "HackToolbarStyle" ) && d.Contains( "SurfaceContainer" ) );
		Assert.Contains( drift, d => d.Contains( "el.detached.CornerRadius" ) && d.Contains( "Dark" ) && d.Contains( "8" ) && d.Contains( "16" ) );
		Assert.DoesNotContain( drift, d => d.Contains( "el.detached.Padding" ) );
	}

	[Theory]
	[InlineData( "TextBlock", "Text", "Text" )]
	[InlineData( "NavRailItem", "Text", "Label" )]
	[InlineData( "common:NavRailItem", "Text", "Label" )]
	[InlineData( "Button", "Text", "Content" )]
	[InlineData( "common:NavRailItem", "Padding", "Padding" )]
	public void ElementValues_AttributeName_MapsTextPerControl( string tag, string property, string expected )
		=> Assert.Equal( expected, ElementValues.AttributeName( tag, property ) );

	[Fact]
	public void Push_PartialThemeFailure_DoesNotMarkElementApplied()
	{
		const string lightFrame = "f3000000-0000-0000-0000-000000000001", darkFrame = "f3000000-0000-0000-0000-000000000002";
		const string lightBox = "f3000000-0000-0000-0000-000000000011", darkBox = "f3000000-0000-0000-0000-000000000021";
		static JsonObject Token( string name, string value ) => new() { [ "id" ] = Guid.NewGuid().ToString(), [ "name" ] = name, [ "type" ] = "color", [ "value" ] = value };
		PenpotFile BuildFile( bool includeLightTarget, bool includeDarkTarget )
		{
			JsonObject objects = new()
			{
				[ lightFrame ] = new JsonObject { [ "id" ] = lightFrame, [ "type" ] = "frame", [ "name" ] = "Screen — Light", [ "parent-id" ] = Guid.Empty.ToString(), [ "shapes" ] = new JsonArray( lightBox ) },
				[ darkFrame ] = new JsonObject { [ "id" ] = darkFrame, [ "type" ] = "frame", [ "name" ] = "Screen — Dark", [ "parent-id" ] = Guid.Empty.ToString(), [ "shapes" ] = new JsonArray( darkBox ) },
				[ lightBox ] = new JsonObject
				{
					[ "id" ] = lightBox, [ "type" ] = "frame", [ "name" ] = "Box", [ "parent-id" ] = lightFrame,
					[ "fills" ] = new JsonArray( new JsonObject { [ "fill-color" ] = "#eeedf4", [ "fill-opacity" ] = 1 } ),
					[ "applied-tokens" ] = new JsonObject { [ "fill" ] = "Light.SurfaceContainer" },
				},
				[ darkBox ] = new JsonObject
				{
					[ "id" ] = darkBox, [ "type" ] = "frame", [ "name" ] = "Box", [ "parent-id" ] = darkFrame,
					[ "fills" ] = new JsonArray( new JsonObject { [ "fill-color" ] = "#1e1f25", [ "fill-opacity" ] = 1 } ),
					[ "applied-tokens" ] = new JsonObject { [ "fill" ] = "Dark.SurfaceContainer" },
				},
			};
			JsonObject lightTokens = new() { [ "Light.SurfaceContainer" ] = Token( "Light.SurfaceContainer", "#EEEDF4" ) };
			if( includeLightTarget )
				lightTokens[ "Light.Primary" ] = Token( "Light.Primary", "#485E92" );
			JsonObject darkTokens = new() { [ "Dark.SurfaceContainer" ] = Token( "Dark.SurfaceContainer", "#1E1F25" ) };
			if( includeDarkTarget )
				darkTokens[ "Dark.Primary" ] = Token( "Dark.Primary", "#B0C6FF" );
			return PenpotFile.Parse( new JsonObject
			{
				[ "id" ] = Fakes.FileId.ToString(), [ "name" ] = "test", [ "revn" ] = 1, [ "vern" ] = 0,
				[ "data" ] = new JsonObject
				{
					[ "pages" ] = new JsonArray( PageId.ToString() ),
					[ "pages-index" ] = new JsonObject { [ PageId.ToString() ] = new JsonObject { [ "name" ] = "Screen", [ "objects" ] = objects } },
					[ "tokens-lib" ] = new JsonObject
					{
						[ "S-Light" ] = new JsonObject { [ "id" ] = Guid.NewGuid().ToString(), [ "name" ] = "HackPDM colors/Light", [ "tokens" ] = lightTokens },
						[ "S-Dark" ] = new JsonObject { [ "id" ] = Guid.NewGuid().ToString(), [ "name" ] = "HackPDM colors/Dark", [ "tokens" ] = darkTokens },
					},
				},
			} );
		}
		const string initialView = """
			<Page xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:pp="urn:wyrmpdm:penpot-sync">
				<Border pp:Id="box" Background="{ThemeResource HackSurfaceContainerBrush}" />
			</Page>
			""";
		const string editedView = """
			<Page xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:pp="urn:wyrmpdm:penpot-sync">
				<Border pp:Id="box" Background="{ThemeResource PrimaryBrush}" />
			</Page>
			""";

		// Case 1: Light fails (missing Light.Primary), Dark succeeds
		PenpotFile lightFails = BuildFile( includeLightTarget: false, includeDarkTarget: true );
		SyncManifest m1 = new() { Frames = [ new FrameLink { View = "view.xaml", Frame = "Screen — Light", DarkFrame = "Screen — Dark", Elements = [ new ElementLink { Anchor = "box", Path = "Box" } ] } ] };
		Func<Guid, string?> s1 = LayoutSync.StyleLookup( m1, lightFails );
		Assert.Empty( LayoutSync.Link( LayoutSync.Resolve( m1, lightFails, _ => XamlView.FromText( initialView ) ).Single().Context!, s1 ) );
		FrameContext ctx1 = LayoutSync.Resolve( m1, lightFails, _ => XamlView.FromText( editedView ) ).Single().Context!;
		ChangeSet set1 = new();
		LayoutSync.PushChanges( ctx1, lightFails, LayoutSync.ToPush( LayoutSync.Compare( ctx1, s1 ), Prefer.None ), set1 );
		Assert.Contains( set1.Changes, c => c[ "id" ]!.ToString() == darkBox );
		Assert.DoesNotContain( set1.Changes, c => c[ "id" ]!.ToString() == lightBox );
		Assert.Empty( set1.AppliedElements );

		// Case 2: Light succeeds, Dark fails (missing Dark.Primary)
		PenpotFile darkFails = BuildFile( includeLightTarget: true, includeDarkTarget: false );
		SyncManifest m2 = new() { Frames = [ new FrameLink { View = "view.xaml", Frame = "Screen — Light", DarkFrame = "Screen — Dark", Elements = [ new ElementLink { Anchor = "box", Path = "Box" } ] } ] };
		Func<Guid, string?> s2 = LayoutSync.StyleLookup( m2, darkFails );
		Assert.Empty( LayoutSync.Link( LayoutSync.Resolve( m2, darkFails, _ => XamlView.FromText( initialView ) ).Single().Context!, s2 ) );
		FrameContext ctx2 = LayoutSync.Resolve( m2, darkFails, _ => XamlView.FromText( editedView ) ).Single().Context!;
		ChangeSet set2 = new();
		LayoutSync.PushChanges( ctx2, darkFails, LayoutSync.ToPush( LayoutSync.Compare( ctx2, s2 ), Prefer.None ), set2 );
		Assert.Contains( set2.Changes, c => c[ "id" ]!.ToString() == lightBox );
		Assert.DoesNotContain( set2.Changes, c => c[ "id" ]!.ToString() == darkBox );
		Assert.Empty( set2.AppliedElements );
	}

	[Fact]
	public void FindDrift_ReportsMissingDarkFrame_DetachedDarkWhenLightIsInstance_AndIgnoresMarkupOverrides()
	{
		// Part 1: missing DarkFrame reported by FindDrift
		const string lightFrame = "f4000000-0000-0000-0000-000000000001", darkFrame = "f4000000-0000-0000-0000-000000000002";
		const string lightCard = "f4000000-0000-0000-0000-000000000011", darkCard = "f4000000-0000-0000-0000-000000000021";
		const string lightBound = "f4000000-0000-0000-0000-000000000012", darkBound = "f4000000-0000-0000-0000-000000000022";
		const string compId = "c4000000-0000-0000-0000-000000000001";

		PenpotFile missingDarkFile = PenpotFile.Parse( new JsonObject
		{
			[ "id" ] = Fakes.FileId.ToString(), [ "name" ] = "test", [ "revn" ] = 1, [ "vern" ] = 0,
			[ "data" ] = new JsonObject
			{
				[ "pages" ] = new JsonArray( PageId.ToString() ),
				[ "pages-index" ] = new JsonObject
				{
					[ PageId.ToString() ] = new JsonObject
					{
						[ "name" ] = "Screen",
						[ "objects" ] = new JsonObject
						{
							[ lightFrame ] = new JsonObject { [ "id" ] = lightFrame, [ "type" ] = "frame", [ "name" ] = "Screen — Light", [ "parent-id" ] = Guid.Empty.ToString(), [ "shapes" ] = new JsonArray() },
						},
					},
				},
			},
		} );
		SyncManifest missingManifest = new()
		{
			Frames = [ new FrameLink { View = "view.xaml", Frame = "Screen — Light", DarkFrame = "Screen — Dark" } ],
		};
		FrameContext missingCtx = LayoutSync.Resolve( missingManifest, missingDarkFile, _ => XamlView.FromText( "<Page xmlns:pp=\"urn:wyrmpdm:penpot-sync\"><Grid /></Page>" ) ).Single().Context!;
		List<string> missingDrift = LayoutSync.FindDrift( missingCtx, missingManifest, LayoutSync.StyleLookup( missingManifest, missingDarkFile ) );
		Assert.Contains( missingDrift, d => d == "view.xaml: missing Dark frame \"Screen — Dark\" in Penpot" );

		// Part 2: LightShape is a component instance while DarkShape is detached & drifted; markup override ({x:Bind ...}) is ignored
		JsonObject objects = new()
		{
			[ lightFrame ] = new JsonObject
			{
				[ "id" ] = lightFrame, [ "type" ] = "frame", [ "name" ] = "Screen — Light", [ "parent-id" ] = Guid.Empty.ToString(),
				[ "shapes" ] = new JsonArray( lightBound, lightCard ),
			},
			[ darkFrame ] = new JsonObject
			{
				[ "id" ] = darkFrame, [ "type" ] = "frame", [ "name" ] = "Screen — Dark", [ "parent-id" ] = Guid.Empty.ToString(),
				[ "shapes" ] = new JsonArray( darkBound, darkCard ),
			},
			[ lightCard ] = new JsonObject
			{
				[ "id" ] = lightCard, [ "type" ] = "frame", [ "name" ] = "Card", [ "parent-id" ] = lightFrame,
				[ "component-id" ] = compId, [ "component-root" ] = true,
				[ "r1" ] = 16, [ "r2" ] = 16, [ "r3" ] = 16, [ "r4" ] = 16,
				[ "applied-tokens" ] = new JsonObject { [ "fill" ] = "Light.SurfaceContainer" },
			},
			[ darkCard ] = new JsonObject
			{
				[ "id" ] = darkCard, [ "type" ] = "frame", [ "name" ] = "Card", [ "parent-id" ] = darkFrame,
				[ "r1" ] = 4, [ "r2" ] = 4, [ "r3" ] = 4, [ "r4" ] = 4,
				[ "applied-tokens" ] = new JsonObject { [ "fill" ] = "Dark.SurfaceContainerHigh" },
			},
			[ lightBound ] = new JsonObject
			{
				[ "id" ] = lightBound, [ "type" ] = "frame", [ "name" ] = "Bound", [ "parent-id" ] = lightFrame,
				[ "r1" ] = 16, [ "r2" ] = 16, [ "r3" ] = 16, [ "r4" ] = 16,
				[ "applied-tokens" ] = new JsonObject { [ "fill" ] = "Light.Primary" },
			},
			[ darkBound ] = new JsonObject
			{
				[ "id" ] = darkBound, [ "type" ] = "frame", [ "name" ] = "Bound", [ "parent-id" ] = darkFrame,
				[ "r1" ] = 16, [ "r2" ] = 16, [ "r3" ] = 16, [ "r4" ] = 16,
				[ "applied-tokens" ] = new JsonObject { [ "fill" ] = "Dark.Primary" },
			},
		};
		PenpotFile file = PenpotFile.Parse( new JsonObject
		{
			[ "id" ] = Fakes.FileId.ToString(), [ "name" ] = "test", [ "revn" ] = 1, [ "vern" ] = 0,
			[ "data" ] = new JsonObject
			{
				[ "pages" ] = new JsonArray( PageId.ToString() ),
				[ "pages-index" ] = new JsonObject { [ PageId.ToString() ] = new JsonObject { [ "name" ] = "Screen", [ "objects" ] = objects } },
			},
		} );
		const string viewXaml = """
			<Page xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:pp="urn:wyrmpdm:penpot-sync">
				<Grid>
					<Border pp:Id="el.card" Style="{StaticResource HackToolbarStyle}" />
					<Border pp:Id="el.bound" Style="{StaticResource HackToolbarStyle}" Background="{x:Bind DynamicBg}" />
				</Grid>
			</Page>
			""";
		SyncManifest manifest = new()
		{
			Components =
			[
				new ComponentLink
				{
					Component = "Toolbar", Style = "HackToolbarStyle", File = XamlStyles.HackControlsPath,
					Properties = new() { [ "Background" ] = "SurfaceContainer", [ "CornerRadius" ] = "16" },
				},
			],
			Frames =
			[
				new FrameLink
				{
					View = "view.xaml", Frame = "Screen — Light", DarkFrame = "Screen — Dark",
					Elements =
					[
						new ElementLink { Anchor = "el.card", Path = "Card" },
						new ElementLink { Anchor = "el.bound", Path = "Bound" },
					],
				},
			],
		};
		Func<Guid, string?> styleOf = LayoutSync.StyleLookup( manifest, file );
		FrameContext ctx = LayoutSync.Resolve( manifest, file, _ => XamlView.FromText( viewXaml ) ).Single().Context!;
		Assert.Empty( LayoutSync.Link( ctx, styleOf ) );

		List<string> drift = LayoutSync.FindDrift( ctx, manifest, styleOf );

		Assert.Contains( drift, d => d.Contains( "el.card.Background" ) && d.Contains( "Dark layer" ) && d.Contains( "SurfaceContainerHigh" ) );
		Assert.Contains( drift, d => d.Contains( "el.card.CornerRadius" ) && d.Contains( "Dark layer" ) && d.Contains( "4" ) );
		Assert.DoesNotContain( drift, d => d.Contains( "el.bound.Background" ) );
	}

	[Fact]
	public void NewMarkedXaml_HandlesTranslucentTokens_MultiPartAndZeroBorderThickness_AndMissingComponent()
	{
		JsonObject raw = File().Raw;
		raw[ "data" ]![ "tokens-lib" ] = new JsonObject
		{
			[ "S-Light" ] = new JsonObject
			{
				[ "id" ] = Guid.NewGuid().ToString(), [ "name" ] = "HackPDM colors/Light",
				[ "tokens" ] = new JsonObject
				{
					[ "Light.SurfaceContainer" ] = new JsonObject { [ "id" ] = Guid.NewGuid().ToString(), [ "name" ] = "Light.SurfaceContainer", [ "type" ] = "color", [ "value" ] = "#EEEDF480" },
					[ "Light.OutlineVariant" ] = new JsonObject { [ "id" ] = Guid.NewGuid().ToString(), [ "name" ] = "Light.OutlineVariant", [ "type" ] = "color", [ "value" ] = "#C5C6D040" },
				},
			},
		};
		PenpotFile file = PenpotFile.Parse( raw );
		string view = View.Replace( "Command=\"{x:Bind Go}\" />",
			"""
			Command="{x:Bind Go}" />
						<Border pp:Id="bar.alpha" Background="{ThemeResource HackSurfaceContainerBrush}" BorderBrush="{ThemeResource OutlineVariantBrush}" BorderThickness="1,0,0,0" />
						<Border pp:Id="bar.pair" BorderBrush="{ThemeResource OutlineVariantBrush}" BorderThickness="2,2" />
						<Border pp:Id="bar.zero" BorderBrush="{ThemeResource OutlineVariantBrush}" BorderThickness="0,0,0,0" />
						<Border pp:Id="bar.missingComp" Style="{StaticResource HackToolbarStyle}" />
			""" );
		(SyncManifest manifest, FrameContext ctx, Func<Guid, string?> _) = Linked( file, view, children: true );
		List<string> notes = [];

		List<LayoutCreate.NewLayer> added = LayoutCreate.FromCode( ctx, file, manifest, notes );

		Assert.Equal( 3, added.Count );
		Assert.DoesNotContain( added, a => a.Link.Anchor == "bar.missingComp" );
		Assert.Contains( notes, n => n == "bar.missingComp: no Light component \"Toolbar\" in Penpot" );

		JsonObject alphaObj = added.Single( a => a.Link.Anchor == "bar.alpha" ).Changes[ 0 ][ "obj" ]!.AsObject();
		JsonObject fill = Assert.Single( alphaObj[ "fills" ]!.AsArray() )!.AsObject();
		Assert.Equal( "#eeedf4", (string?)fill[ "fill-color" ] );
		Assert.Equal( 0.502, ShapeFactory.Num( fill[ "fill-opacity" ] ) );
		JsonObject alphaStroke = Assert.Single( alphaObj[ "strokes" ]!.AsArray() )!.AsObject();
		Assert.Equal( "#c5c6d0", (string?)alphaStroke[ "stroke-color" ] );
		Assert.Equal( 0.251, ShapeFactory.Num( alphaStroke[ "stroke-opacity" ] ) );
		Assert.Equal( 1.0, ShapeFactory.Num( alphaStroke[ "stroke-width" ] ) );

		JsonObject pairObj = added.Single( a => a.Link.Anchor == "bar.pair" ).Changes[ 0 ][ "obj" ]!.AsObject();
		JsonObject pairStroke = Assert.Single( pairObj[ "strokes" ]!.AsArray() )!.AsObject();
		Assert.Equal( 2.0, ShapeFactory.Num( pairStroke[ "stroke-width" ] ) );

		JsonObject zeroObj = added.Single( a => a.Link.Anchor == "bar.zero" ).Changes[ 0 ][ "obj" ]!.AsObject();
		Assert.Empty( zeroObj[ "strokes" ]!.AsArray() );
		Assert.False( zeroObj[ "applied-tokens" ]!.AsObject().ContainsKey( "stroke-color" ) );
	}
}
