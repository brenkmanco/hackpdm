using System.Text.Json.Nodes;

using WyrmpdmSync;
using WyrmpdmSync.Penpot;
using WyrmpdmSync.Sync;

// wyrmpdm-sync: keeps the Penpot design file and the WyrmPDM code in step. See design/README.md.
try
{
	return await Cli.RunAsync( args );
}
catch( SyncException ex )
{
	Console.Error.WriteLine( $"error: {ex.Message}" );
	return 1;
}

internal static class Cli
{
	private const string Usage = """
		wyrmpdm-sync — sync the Penpot design file with the WyrmPDM code

		usage: wyrmpdm-sync <command> [options]

		commands:
		  whoami                 check the access token: show the Penpot account it signs in as
		  init [--file <id>]     create or refresh design/penpot-sync.json: link every preset color and
		                         shape-style value in ThemePresets.cs to its Penpot token, each
		                         component to its XAML style, and each marked view element (pp:Id)
		                         listed under "frames" to its Penpot layer
		  fetch                  download the Penpot file to design/.cache/penpot-file.json (for inspection)
		  status [--all]         show what differs between Penpot and the code, and which side changed
		  pull                   take changes made in Penpot into the code (ThemePresets.cs, XAML styles,
		                         views, including new layers inside synced containers)
		  push                   take changes made in the code to Penpot (tokens, library colors, bound
		                         shapes, components and their copies, view layers, new elements)

		options:
		  --dry-run              pull/push: show what would change, change nothing
		  --prefer penpot|code   pull/push: where both sides changed (or never agreed), take this side
		  --cached               init/status: use design/.cache/penpot-file.json instead of downloading again

		The access token comes from the PENPOT_TOKEN environment variable or design/.penpot-token (git-ignored).
		""";

	/// <summary>The design file this repository syncs with, unless the manifest says otherwise.</summary>
	private static readonly Guid DefaultFileId = Guid.Parse( "3db45ae8-1dfd-81ce-8009-0028caf8f15e" );

	public static async Task<int> RunAsync( string[] args )
	{
		if( args.Length == 0 || args[ 0 ] is "-h" or "--help" or "help" )
		{
			Console.WriteLine( Usage );
			return args.Length == 0 ? 1 : 0;
		}
		RepoPaths paths = RepoPaths.Find();
		Options options = Options.Parse( args[ 1.. ] );
		return args[ 0 ] switch
		{
			"whoami" => await WhoAmIAsync( paths ),
			"init" => await InitAsync( paths, options ),
			"fetch" => await FetchAsync( paths ),
			"status" => await StatusAsync( paths, options ),
			"pull" => await PullAsync( paths, options ),
			"push" => await PushAsync( paths, options ),
			_ => throw new SyncException( $"Unknown command '{args[ 0 ]}'. Run `wyrmpdm-sync help`." ),
		};
	}

	private static async Task<int> WhoAmIAsync( RepoPaths paths )
	{
		string baseUrl = File.Exists( paths.Manifest ) ? SyncManifest.Load( paths.Manifest ).Penpot.BaseUrl : new PenpotTarget().BaseUrl;
		using PenpotClient client = new( baseUrl, PenpotClient.ReadAccessToken( paths ) );
		JsonNode? profile = await client.GetProfileAsync();
		Console.WriteLine( $"Signed in to {baseUrl} as {profile?[ "fullname" ]} ({profile?[ "email" ]})." );
		return 0;
	}

	private static async Task<int> InitAsync( RepoPaths paths, Options options )
	{
		SyncManifest manifest = File.Exists( paths.Manifest ) ? SyncManifest.Load( paths.Manifest ) : new SyncManifest();
		if( options.FileId is { } id )
			manifest.Penpot.FileId = id;
		if( manifest.Penpot.FileId == Guid.Empty )
			manifest.Penpot.FileId = DefaultFileId;

		PenpotFile file = await LoadFileAsync( paths, manifest, cached: options.Cached );
		ThemePresetsSource code = ThemePresetsSource.Load( paths );
		manifest.Penpot.FileName = file.Name;
		XamlFiles xaml = new( paths );
		int added = TokenSync.Link( manifest, file, code ) + ComponentSync.Link( manifest, file, xaml.Get );
		manifest.Save( paths.Manifest );

		List<TokenLink> links = [ .. manifest.TokenLinks ];
		int linked = links.Count( c => c.TokenId is not null );
		Console.WriteLine( $"{paths.Relative( paths.Manifest )}: file \"{file.Name}\", {added} new link(s). {manifest.Colors.Count} colors and " +
		                   $"{manifest.Shapes.Count} shape values, {linked} linked to a Penpot token." );
		if( linked < links.Count )
			Console.WriteLine( $"{links.Count - linked} have no token in Penpot yet; `push` creates them." );
		Console.WriteLine( $"Components: {manifest.Components.Count( c => !c.IsReference )} linked to a style in this repository " +
		                   $"({manifest.Components.Sum( c => c.Properties.Count )} setters), {manifest.Components.Count( c => c.IsReference )} to Uno Material styles (reference only)." );
		foreach( ComponentLink missing in manifest.Components.Where( c => ComponentSync.Component( file, c, ComponentSync.SourceTheme ) is null ) )
			Console.WriteLine( $"  ? {missing.Label}: no such component in Penpot" );
		XamlViews views = new( paths );
		Func<Guid, string?> styleOf = LayoutSync.StyleLookup( manifest, file );
		foreach( (FrameLink frame, FrameContext? ctx, string? problem) in LayoutSync.Resolve( manifest, file, views.Get ) )
		{
			if( ctx is null )
			{
				Console.WriteLine( $"  ? {frame.View}: {problem}" );
				continue;
			}
			List<string> problems = LayoutSync.Link( ctx, styleOf );
			Console.WriteLine( $"View {frame.View} ↔ \"{frame.Frame}\": {frame.Elements.Count( e => e.Shape is not null )} of {frame.Elements.Count} elements linked, " +
			                   $"{frame.Elements.Sum( e => e.Properties.Count )} properties." );
			foreach( string line in problems )
				Console.WriteLine( $"  ? {line}" );
		}
		manifest.Save( paths.Manifest );
		return 0;
	}

	private static async Task<int> FetchAsync( RepoPaths paths )
	{
		SyncManifest manifest = File.Exists( paths.Manifest ) ? SyncManifest.Load( paths.Manifest ) : new SyncManifest { Penpot = { FileId = DefaultFileId } };
		PenpotFile file = await LoadFileAsync( paths, manifest, cached: false );
		Console.WriteLine( $"Saved \"{file.Name}\" (revision {file.Revn}) to {paths.Relative( paths.CachedFile )}: " +
		                   $"{file.Pages.Count} pages, {file.Tokens.Count} tokens in {file.TokenSets.Count} sets." );
		Console.WriteLine( $"Pages: {string.Join( ", ", file.Pages.Select( p => $"{p.Name} ({p.Objects.Count} shapes)" ) )}" );
		return 0;
	}

	private static async Task<int> StatusAsync( RepoPaths paths, Options options )
	{
		(SyncManifest manifest, PenpotFile file, ThemePresetsSource code) = await LoadAllAsync( paths, options.Cached );
		List<TokenReport> reports = TokenSync.Compare( manifest, file, code );
		List<ComponentReport> components = ComponentSync.Compare( manifest, file, new XamlFiles( paths ).Get );

		Console.WriteLine( $"Penpot file \"{file.Name}\", revision {file.Revn}." );
		Print( "Colors", reports.Where( r => r.Link is ColorLink ).ToList(), options.All );
		Print( "Shape values", reports.Where( r => r.Link is ShapeLink ).ToList(), options.All );
		Print( components, options.All );
		bool layoutsDiffer = PrintLayouts( paths, manifest, file, options.All );
		return reports.Any( r => r.State != SyncState.InSync ) || components.Any( r => r.State != SyncState.InSync ) || layoutsDiffer ? 2 : 0;
	}

	private static async Task<int> PullAsync( RepoPaths paths, Options options )
	{
		(SyncManifest manifest, PenpotFile file, ThemePresetsSource code) = await LoadAllAsync( paths, cached: false );
		List<TokenReport> reports = TokenSync.Compare( manifest, file, code );
		List<TokenReport> pulled = TokenSync.ToPull( reports, options.Prefer );
		ReportSkipped( reports, pulled, options.Prefer, "pull" );
		XamlFiles xaml = new( paths );
		List<ComponentReport> componentReports = ComponentSync.Compare( manifest, file, xaml.Get );
		List<ComponentReport> pulledComponents = ComponentSync.ToPull( componentReports, options.Prefer );
		ReportSkipped( componentReports, pulledComponents, options.Prefer, "pull" );
		XamlViews views = new( paths );
		Func<Guid, string?> styleOf = LayoutSync.StyleLookup( manifest, file );
		List<(FrameContext Ctx, List<ElementReport> Pulled, List<LayoutCreate.NewXaml> Added)> layouts = [];
		foreach( (FrameLink _, FrameContext? ctx, string? _) in LayoutSync.Resolve( manifest, file, views.Get ) )
		{
			if( ctx is null )
				continue;
			List<ElementReport> elementReports = LayoutSync.Compare( ctx, styleOf );
			List<ElementReport> pulledElements = LayoutSync.ToPull( elementReports, options.Prefer );
			ReportSkipped( elementReports, pulledElements, options.Prefer, "pull" );
			List<string> notes = [];
			List<LayoutCreate.NewXaml> added = LayoutCreate.FromPenpot( ctx, file, styleOf, notes );
			foreach( string note in notes )
				Console.WriteLine( $"  ? {note}" );
			if( pulledElements.Count > 0 || added.Count > 0 )
				layouts.Add( (ctx, pulledElements, added) );
		}
		if( pulled.Count == 0 && pulledComponents.Count == 0 && layouts.Count == 0 )
		{
			Console.WriteLine( "Nothing to pull: the code has every change made in Penpot." );
			return 0;
		}
		foreach( (FrameContext ctx, List<ElementReport> pulledElements, List<LayoutCreate.NewXaml> added) in layouts )
		{
			foreach( ElementReport r in pulledElements )
				Console.WriteLine( $"  {r.Element.Anchor + "." + r.Property,-34} {r.Code ?? "—",-10} → {r.Penpot}" );
			foreach( LayoutCreate.NewXaml n in added )
				Console.WriteLine( $"  + {n.Description}" );
		}
		foreach( TokenReport r in pulled )
			Console.WriteLine( $"  {r.Link.CodeName,-34} {r.Code ?? "—",-10} → {r.Penpot}" );
		foreach( ComponentReport r in pulledComponents )
			Console.WriteLine( $"  {r.Link.Style + "." + r.Property,-34} {r.Code ?? "—",-10} → {r.Penpot}" );
		if( options.DryRun )
		{
			Console.WriteLine( $"Dry run: {pulled.Count + pulledComponents.Count + layouts.Sum( l => l.Pulled.Count + l.Added.Count )} change(s) would be made in the code." );
			return 0;
		}
		if( pulled.Count > 0 )
		{
			TokenSync.ApplyToCode( code, pulled );
			code.Save();
			foreach( TokenReport r in pulled )
				r.Link.Synced = r.Penpot;
		}
		if( pulledComponents.Count > 0 )
		{
			ComponentSync.ApplyToCode( pulledComponents, xaml.Get );
			xaml.SaveAll();
			foreach( ComponentReport r in pulledComponents )
				r.Link.Properties[ r.Property ] = r.Penpot;
		}
		foreach( (FrameContext ctx, List<ElementReport> pulledElements, List<LayoutCreate.NewXaml> added) in layouts )
		{
			List<XamlView.Edit> edits = LayoutSync.PullEdits( ctx, pulledElements );
			edits.AddRange( added.Select( a => a.Edit ) );
			if( added.Count > 0 )
				edits.AddRange( ctx.View.DeclareNamespace() );
			ctx.View.Apply( edits );
			foreach( ElementReport r in pulledElements )
				r.Element.Properties[ r.Property ] = r.Penpot;
			ctx.Link.Elements.AddRange( added.SelectMany( a => a.Links ) );
			// new elements start in sync: record what both sides now define
			LayoutSync.Link( ctx, styleOf );
		}
		views.SaveAll();
		manifest.Save( paths.Manifest );
		Console.WriteLine( $"Pulled {pulled.Count + pulledComponents.Count + layouts.Sum( l => l.Pulled.Count + l.Added.Count )} change(s) into the code. Rebuild the app to see them." );
		return 0;
	}

	private static async Task<int> PushAsync( RepoPaths paths, Options options )
	{
		(SyncManifest manifest, PenpotFile file, ThemePresetsSource code) = await LoadAllAsync( paths, cached: false );
		List<TokenReport> reports = TokenSync.Compare( manifest, file, code );
		List<TokenReport> pushed = TokenSync.ToPush( reports, options.Prefer );
		ReportSkipped( reports, pushed, options.Prefer, "push" );
		List<ComponentReport> componentReports = ComponentSync.Compare( manifest, file, new XamlFiles( paths ).Get );
		List<ComponentReport> pushedComponents = ComponentSync.ToPush( componentReports, options.Prefer );
		ReportSkipped( componentReports, pushedComponents, options.Prefer, "push" );
		foreach( ComponentReport r in componentReports.Where( r => r.State == SyncState.ChangedInCode && StyleValues.PullOnly.Contains( r.Property ) ) )
			Console.WriteLine( $"  ? {r.Link.Style}.{r.Property} changed in code ({r.Penpot} → {r.Code}); resize the component in Penpot instead, then pull." );
		ChangeSet changes = ChangeBuilder.ForTokens( file, pushed );
		ChangeSet componentChanges = ChangeBuilder.ForComponents( file, pushedComponents );
		changes.Changes.AddRange( componentChanges.Changes );
		changes.Descriptions.AddRange( componentChanges.Descriptions );
		Func<Guid, string?> styleOf = LayoutSync.StyleLookup( manifest, file );
		List<(FrameContext Ctx, List<ElementReport> Pushed, List<LayoutCreate.NewLayer> Added)> layouts = [];
		foreach( (FrameLink _, FrameContext? ctx, string? _) in LayoutSync.Resolve( manifest, file, new XamlViews( paths ).Get ) )
		{
			if( ctx is null )
				continue;
			List<ElementReport> elementReports = LayoutSync.Compare( ctx, styleOf );
			List<ElementReport> pushedElements = LayoutSync.ToPush( elementReports, options.Prefer );
			ReportSkipped( elementReports, pushedElements, options.Prefer, "push" );
			foreach( ElementReport r in elementReports.Where( r => r.State == SyncState.ChangedInCode && ElementValues.PullOnly.Contains( r.Property ) ) )
				Console.WriteLine( $"  ? {r.Element.Anchor}.{r.Property} changed in code ({r.Penpot} → {r.Code}); change it in Penpot instead, then pull." );
			LayoutSync.PushChanges( ctx, file, pushedElements, changes );
			List<string> notes = [];
			List<LayoutCreate.NewLayer> added = LayoutCreate.FromCode( ctx, file, manifest, notes );
			foreach( string note in notes )
				Console.WriteLine( $"  ? {note}" );
			foreach( LayoutCreate.NewLayer n in added )
			{
				changes.Changes.AddRange( n.Changes );
				changes.Descriptions.Add( $"+ {n.Description}" );
			}
			if( pushedElements.Count > 0 || added.Count > 0 )
				layouts.Add( (ctx, pushedElements, added) );
		}
		if( changes.IsEmpty )
		{
			Console.WriteLine( "Nothing to push: Penpot has every change made in the code." );
			return 0;
		}
		foreach( string line in changes.Descriptions )
			Console.WriteLine( $"  {line}" );
		if( options.DryRun )
		{
			Console.WriteLine( $"Dry run: {changes.Changes.Count} change(s) would be sent to Penpot." );
			return 0;
		}
		using PenpotClient client = new( manifest.Penpot.BaseUrl, PenpotClient.ReadAccessToken( paths ) );
		await client.UpdateFileAsync( file, changes.Changes );
		foreach( TokenReport r in pushed )
		{
			r.Link.Synced = r.Code;
			if( changes.Ids.TryGetValue( r.Link, out (Guid SetId, Guid TokenId) ids ) )
				(r.Link.SetId, r.Link.TokenId) = ids;
		}
		foreach( ComponentReport r in pushedComponents )
			r.Link.Properties[ r.Property ] = r.Code;
		foreach( (FrameContext ctx, List<ElementReport> pushedElements, List<LayoutCreate.NewLayer> added) in layouts )
		{
			foreach( ElementReport r in pushedElements )
				r.Element.Properties[ r.Property ] = r.Code;
			ctx.Link.Elements.AddRange( added.Select( a => a.Link ) );
		}
		manifest.Save( paths.Manifest );
		Console.WriteLine( $"Pushed {pushed.Count + pushedComponents.Count + layouts.Sum( l => l.Pushed.Count + l.Added.Count )} value(s) to Penpot ({changes.Changes.Count} change(s))." );
		return 0;
	}

	private static void ReportSkipped( List<TokenReport> reports, List<TokenReport> taken, Prefer prefer, string verb )
	{
		List<TokenReport> conflicts = [ .. reports.Where( r => r.State is SyncState.Conflict or SyncState.Unlinked && !taken.Contains( r ) ) ];
		if( conflicts.Count > 0 && prefer == Prefer.None )
			Console.WriteLine( $"Skipping {conflicts.Count} value(s) changed on both sides; rerun {verb} with --prefer penpot or --prefer code to settle them:" );
		foreach( TokenReport r in conflicts.Where( _ => prefer == Prefer.None ) )
			Console.WriteLine( $"  ! {r.Link.CodeName,-32} penpot {r.Penpot,-10} code {r.Code,-10} last synced {r.Link.Synced ?? "never"}" );
		foreach( TokenReport r in reports.Where( r => r.State == SyncState.MissingInCode ) )
			Console.WriteLine( $"  ? {r.Link.CodeName}: no longer in {ThemePresetsSource.RelativePath}; remove its token in Penpot or restore it in code." );
	}

	private static void ReportSkipped( List<ComponentReport> reports, List<ComponentReport> taken, Prefer prefer, string verb )
	{
		if( prefer != Prefer.None )
			return;
		List<ComponentReport> conflicts = [ .. reports.Where( r => r.State is SyncState.Conflict or SyncState.Unlinked && !taken.Contains( r ) ) ];
		if( conflicts.Count > 0 )
			Console.WriteLine( $"Skipping {conflicts.Count} style value(s) that differ with no clear newer side; rerun {verb} with --prefer penpot or --prefer code:" );
		foreach( ComponentReport r in conflicts )
			Console.WriteLine( $"  ! {r.Link.Style + "." + r.Property,-40} penpot {r.Penpot,-14} code {r.Code,-14} last synced {r.Link.Properties[ r.Property ] ?? "never"}" );
	}

	private static void ReportSkipped( List<ElementReport> reports, List<ElementReport> taken, Prefer prefer, string verb )
	{
		if( prefer != Prefer.None )
			return;
		List<ElementReport> conflicts = [ .. reports.Where( r => r.State is SyncState.Conflict or SyncState.Unlinked && !taken.Contains( r ) ) ];
		if( conflicts.Count > 0 )
			Console.WriteLine( $"Skipping {conflicts.Count} view value(s) that differ with no clear newer side; rerun {verb} with --prefer penpot or --prefer code:" );
		foreach( ElementReport r in conflicts )
			Console.WriteLine( $"  ! {r.Element.Anchor + "." + r.Property,-40} penpot {r.Penpot,-14} code {r.Code,-14} last synced {r.Element.Properties[ r.Property ] ?? "never"}" );
	}

	/// <summary>Each linked view: its elements' properties, and new layers or elements waiting on either side. True when anything differs.</summary>
	private static bool PrintLayouts( RepoPaths paths, SyncManifest manifest, PenpotFile file, bool all )
	{
		bool differs = false;
		Func<Guid, string?> styleOf = LayoutSync.StyleLookup( manifest, file );
		foreach( (FrameLink frame, FrameContext? ctx, string? problem) in LayoutSync.Resolve( manifest, file, new XamlViews( paths ).Get ) )
		{
			if( ctx is null )
			{
				Console.WriteLine( $"View {frame.View}: {problem}" );
				differs = true;
				continue;
			}
			List<ElementReport> reports = LayoutSync.Compare( ctx, styleOf );
			Console.WriteLine( $"View {frame.View} ↔ \"{frame.Frame}\": {reports.Count} properties — " + string.Join( ", ",
				reports.GroupBy( c => c.State ).OrderBy( g => g.Key ).Select( g => $"{g.Count()} {Describe( g.Key )}" ) ) + "." );
			foreach( ElementReport r in reports.Where( r => all || r.State != SyncState.InSync ) )
				Console.WriteLine( $"  {Mark( r.State )} {r.Element.Anchor + "." + r.Property,-40} penpot {Short( r.Penpot ),-14} code {Short( r.Code ),-14} {Describe( r.State )}" );
			List<string> notes = [];
			foreach( LayoutCreate.NewXaml n in LayoutCreate.FromPenpot( ctx, file, styleOf, notes ) )
				Console.WriteLine( $"  < new in Penpot (pull adds it): {n.Description}" );
			foreach( LayoutCreate.NewLayer n in LayoutCreate.FromCode( ctx, file, manifest, notes ) )
				Console.WriteLine( $"  > new in code (push adds it): {n.Description}" );
			foreach( string note in notes )
				Console.WriteLine( $"  ? {note}" );
			foreach( ElementLink gone in frame.Elements.Where( e => ctx.Shape( e.Shape ) is null ) )
				Console.WriteLine( $"  ? {gone.Anchor}: its Penpot layer is gone; remove the element in XAML (or relink it) and its entry in the manifest" );
			foreach( ElementLink gone in frame.Elements.Where( e => !ctx.View.Anchors.ContainsKey( e.Anchor ) ) )
				Console.WriteLine( $"  ? {gone.Anchor}: no longer in {frame.View}; delete its layer in Penpot (or restore it) and its entry in the manifest" );
			differs |= reports.Any( r => r.State != SyncState.InSync ) || notes.Count > 0;
		}
		return differs;
	}

	private static string Short( string? value ) => value is null ? "—" : value.Length > 14 ? value[ ..13 ] + "…" : value;

	private static void Print( List<ComponentReport> reports, bool all )
	{
		Console.WriteLine( $"Component styles: {reports.Count} setters — " + string.Join( ", ",
			reports.GroupBy( c => c.State ).OrderBy( g => g.Key ).Select( g => $"{g.Count()} {Describe( g.Key )}" ) ) + "." );
		foreach( ComponentReport r in reports.Where( r => all || r.State != SyncState.InSync ) )
			Console.WriteLine( $"  {Mark( r.State )} {r.Link.Style + "." + r.Property,-40} penpot {r.Penpot ?? "—",-14} code {r.Code ?? "—",-14} {Describe( r.State )}" );
	}

	private static void Print( string title, List<TokenReport> reports, bool all )
	{
		Console.WriteLine( $"{title}: {reports.Count} linked — " + string.Join( ", ",
			reports.GroupBy( c => c.State ).OrderBy( g => g.Key ).Select( g => $"{g.Count()} {Describe( g.Key )}" ) ) + "." );
		foreach( TokenReport r in reports.Where( r => all || r.State != SyncState.InSync ) )
			Console.WriteLine( $"  {Mark( r.State )} {r.Link.Token,-34} penpot {r.Penpot ?? "—",-10} code {r.Code ?? "—",-10} {Describe( r.State )}" );
	}

	private static async Task<(SyncManifest, PenpotFile, ThemePresetsSource)> LoadAllAsync( RepoPaths paths, bool cached )
	{
		SyncManifest manifest = SyncManifest.Load( paths.Manifest );
		PenpotFile file = await LoadFileAsync( paths, manifest, cached );
		return (manifest, file, ThemePresetsSource.Load( paths ));
	}

	/// <summary>Downloads the file (or reads the cached copy) and parses it. Downloads also refresh the cache.</summary>
	private static async Task<PenpotFile> LoadFileAsync( RepoPaths paths, SyncManifest manifest, bool cached )
	{
		JsonObject raw;
		if( cached )
		{
			if( !File.Exists( paths.CachedFile ) )
				throw new SyncException( $"No cached file at {paths.Relative( paths.CachedFile )}; run `wyrmpdm-sync fetch` first." );
			raw = JsonNode.Parse( await File.ReadAllTextAsync( paths.CachedFile ) ) as JsonObject
			      ?? throw new SyncException( "The cached Penpot file is not a JSON object." );
		}
		else
		{
			using PenpotClient client = new( manifest.Penpot.BaseUrl, PenpotClient.ReadAccessToken( paths ) );
			raw = await client.GetFileAsync( manifest.Penpot.FileId );
			Directory.CreateDirectory( paths.CacheDir );
			await File.WriteAllTextAsync( paths.CachedFile, raw.ToJsonString( new() { WriteIndented = true } ) );
		}
		return PenpotFile.Parse( raw );
	}

	private static string Describe( SyncState state ) => state switch
	{
		SyncState.InSync => "in sync",
		SyncState.ChangedInPenpot => "changed in Penpot (pull)",
		SyncState.ChangedInCode => "changed in code (push)",
		SyncState.Conflict => "changed on both sides",
		SyncState.Unlinked => "differ, no baseline",
		SyncState.MissingInPenpot => "missing in Penpot (push creates it)",
		SyncState.MissingInCode => "missing in code",
		_ => state.ToString(),
	};

	private static string Mark( SyncState state ) => state switch
	{
		SyncState.InSync => " ",
		SyncState.ChangedInPenpot => "<",
		SyncState.ChangedInCode or SyncState.MissingInPenpot => ">",
		SyncState.Conflict => "!",
		_ => "?",
	};

	private sealed record Options( Guid? FileId, bool Cached, bool All, bool DryRun, Prefer Prefer )
	{
		public static Options Parse( string[] args )
		{
			Guid? fileId = null;
			bool cached = false, all = false, dryRun = false;
			Prefer prefer = Prefer.None;
			for( int i = 0; i < args.Length; i++ )
			{
				switch( args[ i ] )
				{
					case "--file" when i + 1 < args.Length:
						fileId = Guid.TryParse( args[ ++i ], out Guid id ) ? id : throw new SyncException( $"'{args[ i ]}' is not a Penpot file id." );
						break;
					case "--prefer" when i + 1 < args.Length:
						prefer = args[ ++i ] switch
						{
							"penpot" => Prefer.Penpot,
							"code" => Prefer.Code,
							_ => throw new SyncException( "--prefer takes 'penpot' or 'code'." ),
						};
						break;
					case "--cached": cached = true; break;
					case "--all": all = true; break;
					case "--dry-run": dryRun = true; break;
					default: throw new SyncException( $"Unknown option '{args[ i ]}'." );
				}
			}
			return new Options( fileId, cached, all, dryRun, prefer );
		}
	}
}

/// <summary>XAML files loaded once per run (several links share HackControls.xaml), saved together.</summary>
internal sealed class XamlFiles( RepoPaths paths )
{
	private readonly Dictionary<string, XamlStyles> _files = new( StringComparer.OrdinalIgnoreCase );

	public XamlStyles Get( string relativePath )
	{
		if( !_files.TryGetValue( relativePath, out XamlStyles? styles ) )
			_files[ relativePath ] = styles = XamlStyles.Load( Path.Combine( paths.Root, relativePath ) );
		return styles;
	}

	public void SaveAll()
	{
		foreach( XamlStyles styles in _files.Values )
			styles.Save();
	}
}

/// <summary>XAML views loaded once per run, saved together.</summary>
internal sealed class XamlViews( RepoPaths paths )
{
	private readonly Dictionary<string, XamlView> _views = new( StringComparer.OrdinalIgnoreCase );

	public XamlView Get( string relativePath )
	{
		if( !_views.TryGetValue( relativePath, out XamlView? view ) )
			_views[ relativePath ] = view = XamlView.Load( Path.Combine( paths.Root, relativePath ) );
		return view;
	}

	public void SaveAll()
	{
		foreach( XamlView view in _views.Values )
			view.Save();
	}
}
