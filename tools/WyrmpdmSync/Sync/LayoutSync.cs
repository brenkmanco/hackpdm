using System.Text.Json.Nodes;

using WyrmpdmSync.Penpot;

namespace WyrmpdmSync.Sync;

/// <summary>One synced property of a page element.</summary>
internal sealed record ElementReport( FrameLink Frame, ElementLink Element, string Property, string? Penpot, string? Code, SyncState State );

/// <summary>A view and its frames, resolved: the Penpot page, both frames, and the XAML.</summary>
internal sealed class FrameContext
{
	public required FrameLink Link { get; init; }
	public required PenpotPage Page { get; init; }
	public required JsonObject Frame { get; init; }
	public JsonObject? DarkFrame { get; init; }
	public required XamlView View { get; init; }
	public JsonObject Objects => Page.Objects;

	public JsonObject? Shape( Guid? id ) => id is { } g ? Objects[ g.ToString() ] as JsonObject : null;
}

/// <summary>
/// Keeps marked XAML elements and their Penpot layers in step: properties both ways (see <see cref="ElementValues"/>),
/// and, inside elements whose children are synced as a whole, new layers and new elements.
/// </summary>
internal static class LayoutSync
{
	public const string SourceTheme = ComponentSync.SourceTheme;

	/// <summary>Finds each frame link's Penpot frames and XAML view; null entries are reported by the caller.</summary>
	public static List<(FrameLink Link, FrameContext? Context, string? Problem)> Resolve( SyncManifest manifest, PenpotFile file, Func<string, XamlView> views )
	{
		List<(FrameLink, FrameContext?, string?)> result = [];
		foreach( FrameLink link in manifest.Frames )
		{
			(PenpotPage page, JsonObject frame)? found = FindFrame( file, link.FrameId, link.Frame );
			if( found is null )
			{
				result.Add( (link, null, $"no frame \"{link.Frame}\" in Penpot") );
				continue;
			}
			(PenpotPage page, JsonObject frame) = found.Value;
			JsonObject? dark = link.DarkFrame is null ? null : FindFrame( file, link.DarkFrameId, link.DarkFrame )?.Frame;
			XamlView view;
			try { view = views( link.View ); }
			catch( SyncException ex ) { result.Add( (link, null, ex.Message) ); continue; }
			link.FrameId = Guid.Parse( frame[ "id" ]!.ToString() );
			link.DarkFrameId = dark is null ? null : Guid.Parse( dark[ "id" ]!.ToString() );
			result.Add( (link, new FrameContext { Link = link, Page = page, Frame = frame, DarkFrame = dark, View = view }, null) );
		}
		return result;
	}

	/// <summary>
	/// Turns each element's path into layer ids (Light and Dark), and records the properties both sides define. A
	/// property whose two sides already match gets that value as its baseline.
	/// </summary>
	public static List<string> Link( FrameContext ctx, Func<Guid, string?> styleOf )
	{
		List<string> problems = [];
		foreach( ElementLink element in ctx.Link.Elements )
		{
			if( !ctx.View.Anchors.TryGetValue( element.Anchor, out XamlElement? xaml ) )
			{
				problems.Add( $"{element.Anchor}: no element with pp:Id=\"{element.Anchor}\" in {ctx.Link.View}" );
				continue;
			}
			JsonObject? shape = ctx.Shape( element.Shape );
			if( shape is null && element.Path is not null )
				shape = ShapeTree.Resolve( ctx.Objects, ctx.Frame, element.Path );
			if( shape is null )
			{
				problems.Add( $"{element.Anchor}: no layer {( element.Path is null ? $"with id {element.Shape}" : $"at \"{element.Path}\"" )} in \"{ctx.Link.Frame}\"" );
				continue;
			}
			element.Shape = Guid.Parse( shape[ "id" ]!.ToString() );
			element.Path = ShapeTree.PathOf( ctx.Objects, ctx.Frame, shape ) ?? element.Path;
			if( element.DarkShape is null && ctx.DarkFrame is not null && element.Path is not null
			    && ShapeTree.Resolve( ctx.Objects, ctx.DarkFrame, element.Path ) is { } dark )
				element.DarkShape = Guid.Parse( dark[ "id" ]!.ToString() );

			foreach( string property in ElementValues.All )
			{
				if( element.Properties.TryGetValue( property, out string? existing ) && existing is not null )
					continue;
				string? code = ElementValues.FromXaml( xaml, property );
				string? penpot = ElementValues.FromShape( property, shape, ctx.Objects, SourceTheme, styleOf );
				if( code is null || penpot is null )
					continue;
				element.Properties[ property ] = code == penpot ? code : null;
			}
		}
		return problems;
	}

	public static List<ElementReport> Compare( FrameContext ctx, Func<Guid, string?> styleOf )
	{
		List<ElementReport> reports = [];
		foreach( ElementLink element in ctx.Link.Elements )
		{
			ctx.View.Anchors.TryGetValue( element.Anchor, out XamlElement? xaml );
			JsonObject? shape = ctx.Shape( element.Shape );
			foreach( (string property, string? synced) in element.Properties )
			{
				string? code = xaml is null ? null : ElementValues.FromXaml( xaml, property );
				string? penpot = shape is null ? null : ElementValues.FromShape( property, shape, ctx.Objects, SourceTheme, styleOf );
				reports.Add( new ElementReport( ctx.Link, element, property, penpot, code, ThreeWay.Compare( penpot, code, synced ) ) );
			}
		}
		return reports;
	}

	public static List<ElementReport> ToPull( IEnumerable<ElementReport> reports, Prefer prefer )
		=> [ .. reports.Where( r => r.Penpot is not null && ( r.State == SyncState.ChangedInPenpot
		                             || ( prefer == Prefer.Penpot && r.State is SyncState.Conflict or SyncState.Unlinked ) ) ) ];

	public static List<ElementReport> ToPush( IEnumerable<ElementReport> reports, Prefer prefer )
		=> [ .. reports.Where( r => r.Code is not null && !ElementValues.PullOnly.Contains( r.Property )
		                             && ( r.State == SyncState.ChangedInCode
		                                  || ( prefer == Prefer.Code && r.State is SyncState.Conflict or SyncState.Unlinked ) ) ) ];

	/// <summary>The XAML edits for pulled values.</summary>
	public static List<XamlView.Edit> PullEdits( FrameContext ctx, IEnumerable<ElementReport> pulled )
	{
		List<XamlView.Edit> edits = [];
		foreach( ElementReport r in pulled )
		{
			if( !ctx.View.Anchors.TryGetValue( r.Element.Anchor, out XamlElement? xaml ) )
				continue;
			string attr = ElementValues.AttributeName( xaml.Tag, r.Property );
			edits.Add( ctx.View.SetAttribute( xaml, attr, ElementValues.ToXaml( r.Property, r.Penpot!, xaml.Attr( attr ) ) ) );
		}
		return edits;
	}

	/// <summary>The Penpot changes for pushed values: each element's Light layer and its Dark twin.</summary>
	public static void PushChanges( FrameContext ctx, PenpotFile file, IEnumerable<ElementReport> pushed, ChangeSet set )
	{
		Dictionary<string, JsonObject> gapState = [];
		foreach( ElementReport r in pushed )
		{
			bool lightApplied = false;
			bool anyFailed = false;
			bool emitted = false;
			(Guid? Id, string Theme, bool Expected)[] targets =
			[
				(r.Element.Shape, SourceTheme, true),
				(r.Element.DarkShape, "Dark", r.Element.DarkShape is not null),
			];
			foreach( (Guid? id, string theme, bool expected) in targets )
			{
				if( !expected )
					continue;
				if( ctx.Shape( id ) is not { } shape )
				{
					anyFailed = true;
					set.Descriptions.Add( $"  skipped {r.Element.Anchor} [{theme}] {r.Property}: layer is missing in Penpot" );
					continue;
				}
				JsonObject target = shape;
				JsonArray? ops;
				switch( r.Property )
				{
					case ElementValues.Spacing or ElementValues.RowSpacing or ElementValues.ColumnSpacing:
						string key = r.Property switch
						{
							ElementValues.RowSpacing => "row-gap",
							ElementValues.ColumnSpacing => "column-gap",
							_ => shape[ "layout-flex-dir" ]?.ToString() is "row" or "row-reverse" ? "column-gap" : "row-gap",
						};
						string shapeId = shape[ "id" ]!.ToString();
						if( !gapState.TryGetValue( shapeId, out JsonObject? gap ) )
							gapState[ shapeId ] = gap = shape[ "layout-gap" ]?.DeepClone() as JsonObject ?? new JsonObject { [ "row-gap" ] = 0, [ "column-gap" ] = 0 };
						gap[ key ] = StyleValues.Json( r.Code! );
						ops = [ ChangeBuilder.SetOp( "layout-gap", gap.DeepClone() ) ];
						break;
					case ElementValues.Text:
						if( ElementValues.TextLayer( shape, ctx.Objects ) is not { } textLayer || ShapeTree.WithText( textLayer, r.Code! ) is not { } content )
						{
							anyFailed = true;
							set.Descriptions.Add( $"  skipped {r.Element.Anchor} Text: the Penpot text has several styled runs; edit it in Penpot" );
							continue;
						}
						target = textLayer;
						// the rendered glyph positions belong to the old text; Penpot lays the new text out again
						ops = [ ChangeBuilder.SetOp( "content", content ), ChangeBuilder.SetOp( "position-data", null ) ];
						break;
					default:
						ops = ChangeBuilder.ShapeOps( file, shape, r.Property, r.Code!, theme, set, pageId: ctx.Page.Id )?.Ops;
						break;
				}
				if( ops is null )
				{
					anyFailed = true;
					continue;
				}
				set.Changes.Add( new JsonObject
				{
					[ "type" ] = "mod-obj", [ "id" ] = target[ "id" ]!.ToString(), [ "pageId" ] = ctx.Page.Id.ToString(), [ "operations" ] = ops,
				} );
				emitted = true;
				if( theme == SourceTheme )
					lightApplied = true;
			}
			if( lightApplied && !anyFailed )
				set.AppliedElements.Add( r );
			if( emitted )
				set.Descriptions.Add( $"{r.Element.Anchor} {r.Property}: {r.Penpot ?? "—"} → {r.Code}" );
		}
	}

	/// <summary>
	/// Reports drift inside a view's Penpot frames that normal Light-vs-code comparison doesn't see:
	/// Dark twin layers that are missing or differ from their Light layer, and detached styled frames
	/// whose styling differs from the synced component style their XAML element references.
	/// </summary>
	public static List<string> FindDrift( FrameContext ctx, SyncManifest manifest, Func<Guid, string?> styleOf )
	{
		List<string> drift = [];
		if( ctx.Link.DarkFrame is not null && ctx.DarkFrame is null )
		{
			drift.Add( $"{ctx.Link.View}: missing Dark frame \"{ctx.Link.DarkFrame}\" in Penpot" );
		}
		else if( ctx.DarkFrame is not null )
		{
			foreach( ElementLink element in ctx.Link.Elements )
			{
				if( ctx.Shape( element.Shape ) is not { } lightShape )
					continue;
				if( ctx.Shape( element.DarkShape ) is not { } darkShape )
				{
					drift.Add( $"{element.Anchor}: missing Dark twin layer in \"{ctx.Link.DarkFrame}\"" );
					continue;
				}
				foreach( string property in element.Properties.Keys )
				{
					string? lightVal = ElementValues.FromShape( property, lightShape, ctx.Objects, SourceTheme, styleOf );
					string? darkVal = ElementValues.FromShape( property, darkShape, ctx.Objects, "Dark", styleOf );
					if( lightVal != darkVal )
						drift.Add( $"{element.Anchor}.{property}: Dark layer has {darkVal ?? "—"}, Light has {lightVal ?? "—"}" );
				}
			}
		}
		foreach( ElementLink element in ctx.Link.Elements )
		{
			if( !ctx.View.Anchors.TryGetValue( element.Anchor, out XamlElement? xaml ) )
				continue;
			if( ElementValues.FromXaml( xaml, ElementValues.Style ) is not { } styleName )
				continue;
			if( manifest.Components.FirstOrDefault( c => !c.IsReference && c.Style == styleName ) is not { } comp )
				continue;
			JsonObject? lightShape = ctx.Shape( element.Shape );
			JsonObject? darkShape = ctx.DarkFrame is not null ? ctx.Shape( element.DarkShape ) : null;
			bool checkLight = lightShape is not null && ShapeTree.ComponentOf( lightShape ) is null;
			bool checkDark = ctx.DarkFrame is not null && darkShape is not null && ShapeTree.ComponentOf( darkShape ) is null;
			if( !checkLight && !checkDark )
				continue;
			foreach( (string prop, string? synced) in comp.Properties )
			{
				if( synced is null || StyleValues.PullOnly.Contains( prop ) || xaml.Attr( ElementValues.AttributeName( xaml.Tag, prop ) ) is not null )
					continue;
				if( checkLight )
				{
					string? lightActual = StyleValues.FromShape( prop, lightShape!, SourceTheme );
					if( lightActual != synced )
						drift.Add( $"{element.Anchor}.{prop}: Penpot layer has {lightActual ?? "—"}, {comp.Style} has {synced}" );
				}
				if( checkDark )
				{
					string? darkActual = StyleValues.FromShape( prop, darkShape!, "Dark" );
					if( darkActual != synced )
						drift.Add( $"{element.Anchor}.{prop}: Dark layer has {darkActual ?? "—"}, {comp.Style} has {synced}" );
				}
			}
		}
		return drift;
	}

	/// <summary>Maps a Penpot component id to the XAML style it is linked to (any theme).</summary>
	public static Func<Guid, string?> StyleLookup( SyncManifest manifest, PenpotFile file )
		=> id => file.Components.FirstOrDefault( c => c.Id == id ) is { } component
			? manifest.Components.FirstOrDefault( l => l.Component == component.Name
				&& l.Variant.All( kv => component.Variant.GetValueOrDefault( kv.Key ) == kv.Value ) )?.Style
			: null;

	private static (PenpotPage Page, JsonObject Frame)? FindFrame( PenpotFile file, Guid? id, string name )
	{
		if( id is { } g && file.FindShape( g ) is { } byId )
			return (byId.Page, byId.Shape);
		foreach( PenpotPage page in file.Pages )
			if( ShapeTree.Frame( page, name ) is { } frame )
				return (page, frame);
		return null;
	}
}
