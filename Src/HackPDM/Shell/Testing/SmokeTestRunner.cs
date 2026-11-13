using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using HackPDM.Mvvm.ViewModels.Common;
using HackPDM.Core;
using HackPDM.Core.Graph;
using HackPDM.Core.Theming;
using HackPDM.Core.Hack;
using HackPDM.Infrastructure.Odoo;
using HackPDM.Shared.GlobalData;
using HackPDM.Mvvm.Views.Common.Controls;
using HackPDM.Mvvm.Views.Common.Theming;
using HackPDM.Shell;
using HackPDM.UI.Types;
using HackPDM.Mvvm.Views.Common.Types;

using Microsoft.UI.Xaml;
using HackPDM.Mvvm.Views.Hack;
using HackPDM.Mvvm.Views.Settings;

using EntryRow = HackPDM.Mvvm.Models.Hack.EntryRow;

namespace HackPDM.Shell.Testing;

/// <summary>
/// End-to-end smoke test of the real client against a live Odoo server.
/// Enabled with HACKPDM_SMOKE_TEST=1. It walks the same code paths a user triggers:
/// profile login, navigating to the file manager, loading the directory tree from Odoo,
/// selecting a directory (entries via JSON-RPC) and selecting an entry (history/versions).
/// Each step is timed and written to a JSON report; the process exits 0 on success,
/// 1 on a failed step or unhandled exception, 2 on the overall timeout.
/// </summary>
/// <remarks>
/// Environment:
///   HACKPDM_SMOKE_TEST          1/true to enable
///   HACKPDM_ODOO_ADDRESS        host (default localhost)
///   HACKPDM_ODOO_PORT           port (default 8069)
///   HACKPDM_ODOO_DB             database name (required)
///   HACKPDM_ODOO_USER           login (default admin)
///   HACKPDM_ODOO_PASSWORD       password (default admin)
///   HACKPDM_SMOKE_PWA           local working folder (default: a fresh temp dir)
///   HACKPDM_SMOKE_DIRECTORY     name of the directory node to open (default: first directory with entries)
///   HACKPDM_SMOKE_MIN_ENTRIES   minimum entries expected in that directory (default 1)
///   HACKPDM_SMOKE_REPORT        path of the JSON report (default smoke-report.json in the working dir)
///   HACKPDM_SMOKE_TIMEOUT       overall timeout in seconds (default 240)
/// </remarks>
internal static class SmokeTestRunner
{
	private static readonly List<StepResult> _steps = [];
	private static readonly List<string> _unhandled = [];
	private static readonly Stopwatch _total = new();
	private static int _finished;

	/// <summary>Whether HACKPDM_SMOKE_TEST is set to 1 or true.</summary>
	public static bool IsEnabled
		=> Environment.GetEnvironmentVariable( "HACKPDM_SMOKE_TEST" ) is "1" or "true" or "TRUE" or "True";

	private static string Env( string name, string fallback )
		=> Environment.GetEnvironmentVariable( name ) is { Length: > 0 } v ? v : fallback;

	private static TimeSpan StepTimeout => TimeSpan.FromSeconds( 60 );

	/// <summary>Starts the smoke test on the UI thread; the process exits when it finishes, fails or times out.</summary>
	/// <remarks>
	/// Call once the main window and <see cref="UiDispatcher.Current"/> are set up. Hooks unhandled-exception handlers (any
	/// unhandled exception ends the run with exit code 1), starts the overall timeout and the UI stall probe, then queues the
	/// steps on the UI thread and returns at once.
	/// </remarks>
	public static void Start()
	{
		_total.Start();
		Log( "smoke test enabled" );
		// surface Debug.WriteLine output (the app logs swallowed exceptions that way)
		Trace.Listeners.Add( new ConsoleTraceListener( useErrorStream: true ) );

		Application.Current.UnhandledException += ( _, e ) =>
		{
			_unhandled.Add( e.Exception?.ToString() ?? e.Message );
			Log( $"UNHANDLED (UI): {e.Exception}" );
			e.Handled = true;
			Finish( 1 );
		};
		AppDomain.CurrentDomain.UnhandledException += ( _, e ) =>
		{
			_unhandled.Add( e.ExceptionObject?.ToString() ?? "unknown" );
			Log( $"UNHANDLED (domain): {e.ExceptionObject}" );
			Finish( 1 );
		};

		int timeout = int.TryParse( Env( "HACKPDM_SMOKE_TIMEOUT", "240" ), out var t ) ? t : 240;
		_ = Task.Run( async () =>
		{
			await Task.Delay( TimeSpan.FromSeconds( timeout ) );
			Log( $"overall timeout of {timeout}s reached" );
			Finish( 2 );
		} );

		StartUiStallProbe();
		UiDispatcher.Post( async () =>
		{
			int code;
			try
			{
				code = await RunAsync() ? 0 : 1;
			}
			catch( Exception ex )
			{
				_unhandled.Add( ex.ToString() );
				Log( $"runner crashed: {ex}" );
				code = 1;
			}
			// let any exceptions queued on the UI thread surface before exiting
			await Task.Delay( 1000 );
			Finish( _unhandled.Count > 0 ? 1 : code );
		} );
	}

	// ------------------------------------------------------------------ steps

	private static async Task<bool> RunAsync()
	{
		HackFileManagerView? page = null;
		TreeData? directory = null;

		return await Step( "configure", () =>
			{
				string address = Env( "HACKPDM_ODOO_ADDRESS", "localhost" );
				string port = Env( "HACKPDM_ODOO_PORT", "8069" );
				string db = Environment.GetEnvironmentVariable( "HACKPDM_ODOO_DB" )
					?? throw new InvalidOperationException( "HACKPDM_ODOO_DB is not set" );

				var odoo = OdooDefaults.Instance;
				odoo.OdooAddress = address;
				odoo.OdooPort = port;
				odoo.OdooUrl = $"http://{address}:{port}"; // plain HTTP is accepted to this computer only
				odoo.OdooDb = db;
				odoo.OdooUser = Env( "HACKPDM_ODOO_USER", "admin" );
				odoo.OdooPass = Env( "HACKPDM_ODOO_PASSWORD", "admin" );
				odoo.OdooId = 0;

				string pwa = Env( "HACKPDM_SMOKE_PWA",
					Path.Combine( Path.GetTempPath(), $"hackpdm-smoke-{Guid.NewGuid():N}" ) );
				Directory.CreateDirectory( pwa );
				HackDefaults.Instance.PwaPathAbsolute = pwa;
				return Task.FromResult( $"{odoo.OdooUrl} db={db} user={odoo.OdooUser} pwa={pwa}" );
			} )
			&& await Step( "login", async () =>
			{
				// same call the Profile Manager "Login" button makes
				var pm = AppComposition.CreateProfileManager();
				pm.IsLoggedIn = await pm.AbleToLoginAsync();
				if( !pm.IsLoggedIn )
				{
					string reasons = string.Join( "; ", pm.OStatus.Select( s => s.Message ) );
					throw new InvalidOperationException(
						$"login rejected: {( reasons.Length > 0 ? reasons : "no status" )}; login: {OdooDefaults.Instance.LoginError}" );
				}
				var odoo = OdooDefaults.Instance;
				return $"uid={odoo.OdooId} node={odoo.MyNode?.name} root={odoo.HpDirectoryRoot?.name}";
			} )
			&& await Step( "navigate to file manager", async () =>
			{
				// same call the Profile Manager makes after a successful login
				AppComposition.Navigation.NavigateTo( NavigatePageMenu.HackFileManager );
				var frame = HackApp.HomePage?.ContentFrame
					?? throw new InvalidOperationException( "home page has no content frame" );

				await WaitUntil( () => frame.Content is HackFileManagerView { IsLoaded: true }, "HackFileManager page loaded",
					() => $"frame shows {frame.Content?.GetType().Name ?? "nothing"}" );
				page = (HackFileManagerView)frame.Content;

				// the window takes the file manager's size (its preset), not the Profile Manager's
				var preset = WindowConfig.Preset( "HackFileManager" ).PositionAndSize;
				var appWindow = HackApp.Window!.AppWindow;
				string Size() => $"{appWindow.Size.Width}x{appWindow.Size.Height}";
				await WaitUntil( () => appWindow.Size.Width >= preset.z * 0.9 && appWindow.Size.Height >= preset.w * 0.9,
					$"window resized to the file manager preset {preset.z}x{preset.w}", () => $"window is {Size()}" );
				return $"HackFileManager loaded, window {Size()}";
			} )
			&& await Step( "load directory tree", async () =>
			{
				var vm = page!.ViewModel;
				var tree = page.GetOdooDirectoryTree();
				string? wanted = Environment.GetEnvironmentVariable( "HACKPDM_SMOKE_DIRECTORY" );
				IEnumerable<TreeData> Nodes() => Flatten( tree.RootNodes.Select( n => n.LinkedData ) );
				string Dump() => $"[{string.Join( ", ", Nodes().Select( d => $"{d.FullPath}#{d.DirectoryId}" ) )}] treeLoaded={vm.IsTreeLoaded}";
				await WaitUntil( () => vm.IsTreeLoaded && Nodes().Any( d => d.DirectoryId is > 0
						&& ( wanted is not { Length: > 0 } || string.Equals( d.Name, wanted, StringComparison.OrdinalIgnoreCase ) ) ),
					"directory tree loaded from server", Dump );

				var all = Nodes().ToList();
				directory = wanted is { Length: > 0 }
					? all.FirstOrDefault( d => string.Equals( d.Name, wanted, StringComparison.OrdinalIgnoreCase ) )
					: all.FirstOrDefault( d => d.DirectoryId is > 0 && !d.HasChildren )
						?? all.FirstOrDefault( d => d.DirectoryId is > 0 );
				if( directory is null )
					throw new InvalidOperationException(
						$"directory '{wanted ?? "<any remote>"}' not in tree: [{string.Join( ", ", all.Select( d => $"{d.FullPath}#{d.DirectoryId}" ) )}]"
						+ $"; root={OdooDefaults.Instance.HpDirectoryRoot?.name}#{OdooDefaults.Instance.HpDirectoryRoot?.id}" );

				return $"{all.Count} nodes; opening {directory.FullPath} (id {directory.DirectoryId})";
			} )
			&& await Step( "open directory (entries)", async () =>
			{
				var vm = page!.ViewModel;
				int min = int.TryParse( Env( "HACKPDM_SMOKE_MIN_ENTRIES", "1" ), out var m ) ? m : 1;

				// selecting the node in the tree raises SelectionChanged -> ViewModel.LoadEntriesAsync
				var node = directory!.Node;
				directory.EnsureVisible( page.GetOdooDirectoryTree() );

				var master = vm.Entries;
				await WaitUntil( () => page.LastSelectedNode == node, "tree selection applied" );
				await WaitUntil( () => master.Count >= min && page.TreeSelectionTask is { IsCompleted: true },
					$">= {min} entries listed", () => $"{master.Count} listed" );
				await WaitStable( () => master.Count );
				// RestartEntries / SearchOdoo / the entry locator wait on this flag
				await WaitUntil( () => vm.IsListLoaded, "IsListLoaded set after the directory loaded" );

				var grid = page.GetOdooEntryList();
				string statuses = string.Join( " ", master.GroupBy( e => $"{e.Status}/{e.ReprType}" ).OrderBy( g => g.Key ).Select( g => $"{g.Key}={g.Count()}" ) );
				return $"{master.Count} entries [{statuses}] grid rows={( grid.ItemsSource as System.Collections.ICollection )?.Count}; first: {string.Join( ", ", master.Take( 5 ).Select( e => e.Name ) )}";
			} )
			&& await Step( "select entry (history)", async () =>
			{
				var vm = page!.ViewModel;
				var entry = vm.Entries.FirstOrDefault( e => e.Id is > 0 )
					?? throw new InvalidOperationException( "no entry that exists on the server" );

				page.LowerTabIndex = page.HistoryTabItem;
				page.GetOdooEntryList().SelectedItem = entry;

				await WaitUntil( () => vm.OHistories.Count > 0, $"history rows for {entry.Name}" );
				return $"{entry.Name}: {vm.OHistories.Count} history row(s)";
			} )
			&& await Step( "leave and come back (context kept)", async () =>
			{
				// a new page and view model each visit; the tree, folder, rows, selection and tab come back
				// from the session's FileManagerState without asking the server again
				var before = page!;
				var oldVm = before.ViewModel;
				string? folder = oldVm.LastSelectedNodePath;
				var selected = before.GetOdooEntryList().SelectedItem as EntryRow
					?? throw new InvalidOperationException( "no entry selected before leaving" );
				EntryRow firstRow = oldVm.Entries[ 0 ];
				var keptTree = AppComposition.FileManagerState.Tree;
				var frame = HackApp.HomePage!.ContentFrame;

				AppComposition.Navigation.NavigateTo( NavigatePageMenu.ProfileManager );
				await WaitUntil( () => frame.Content is HackPDM.Mvvm.Views.Settings.ProfileManagerView, "Profile Manager shown" );
				if( !oldVm.IsDisposed )
					throw new InvalidOperationException( "the file manager's view model was not disposed when its page was left" );

				var sw = Stopwatch.StartNew();
				AppComposition.Navigation.NavigateTo( NavigatePageMenu.HackFileManager );
				await WaitUntil( () => frame.Content is HackFileManagerView { IsLoaded: true } p && p != before, "file manager shown again" );
				page = (HackFileManagerView)frame.Content;
				var vm = page.ViewModel;
				if( ReferenceEquals( vm, oldVm ) )
					throw new InvalidOperationException( "the view model was reused instead of made for the new visit" );

				await WaitUntil( () => vm.IsListLoaded && page.LastSelectedNode?.LinkedData.FullPath == folder,
					$"folder {folder} selected again", () => $"selected {page.LastSelectedNode?.LinkedData.FullPath ?? "nothing"}" );
				await WaitUntil( () => ( page.GetOdooEntryList().SelectedItem as EntryRow )?.Name == selected.Name,
					$"{selected.Name} selected again" );
				await WaitUntil( () => vm.OHistories.Count > 0, "history tab filled again" );

				if( !ReferenceEquals( AppComposition.FileManagerState.Tree, keptTree ) )
					throw new InvalidOperationException( "the directory tree was fetched again instead of kept" );
				if( !vm.Entries.Any( r => ReferenceEquals( r, firstRow ) ) )
					throw new InvalidOperationException( "the folder's rows were fetched again instead of kept" );
				if( page.LowerTabIndex?.Name != page.HistoryTabItem.Name )
					throw new InvalidOperationException( $"detail tab not restored: {page.LowerTabIndex?.Name}" );

				return $"back in {sw.ElapsedMilliseconds} ms: {folder}, {vm.Entries.Count} kept rows, {selected.Name} selected, history tab";
			} )
			&& await Step( "live file changes (one row, tree badge)", async () =>
			{
				// the file watcher rebuilds just the changed file's row; other folders get a badge
				var vm = page!.ViewModel;
				string listed = PwaPaths.ToAbsolute( vm.LastSelectedNodePath )!;
				Directory.CreateDirectory( listed );
				EntryRow edited = vm.Entries.FirstOrDefault( e => e.Id is > 0 )
					?? throw new InvalidOperationException( "no remote row to edit" );
				List<EntryRow> others = [ .. vm.Entries.Where( e => e != edited ) ];

				// a local copy appears for a remote-only row: that row is rebuilt as local + remote
				await File.WriteAllTextAsync( Path.Combine( listed, edited.Name! ), "changed locally" );
				await WaitUntil( () => vm.Entries.Any( e => e.Name == edited.Name && !ReferenceEquals( e, edited ) && e.ReprType == EntryReprType.Both ),
					$"{edited.Name} rebuilt as a local + remote row",
					() => string.Join( ", ", vm.Entries.Select( e => $"{e.Name}:{e.ReprType}" ) ) );
				if( others.Any( o => !vm.Entries.Contains( o ) ) )
					throw new InvalidOperationException( "other rows were rebuilt too (the whole list reloaded)" );

				// a new file adds a row; deleting it removes the row
				string added = Path.Combine( listed, "smoke-live.txt" );
				await File.WriteAllTextAsync( added, "new" );
				await WaitUntil( () => vm.Entries.Any( e => e.Name == "smoke-live.txt" && e.ReprType == EntryReprType.Local ), "new local row" );
				File.Delete( added );
				await WaitUntil( () => vm.Entries.All( e => e.Name != "smoke-live.txt" ), "deleted file's row removed" );

				// a file in a folder that is not listed: a badge on that folder
				int badges = page.BadgesShown;
				await File.WriteAllTextAsync( Path.Combine( PwaPaths.Vault, "smoke-elsewhere.txt" ), "x" );
				await WaitUntil( () => page.BadgesShown > badges, "a change badge on the root folder" );

				return $"{edited.Name} rebuilt, {others.Count} other rows untouched, added + removed a row, badge shown";
			} )
			&& await Step( "dependency graph (children, parents, both, zoom)", DependencyGraphStepAsync );
	}

	/// <summary>
	/// The dependency graph page on the seeded Projects\Assembly folder:
	/// frame.slddrw -> frame.sldasm -> arm.sldasm -> bolt.sldprt, plate.sldprt; frame.sldasm -> bolt.sldprt.
	/// </summary>
	private static async Task<string> DependencyGraphStepAsync()
	{
		AppComposition.Navigation.NavigateTo( NavigatePageMenu.DependencyGraph );
		var frame = HackApp.HomePage!.ContentFrame;
		await WaitUntil( () => frame.Content is DependencyGraphView { IsLoaded: true }, "dependency graph page loaded",
			() => $"frame shows {frame.Content?.GetType().Name ?? "nothing"}" );
		var page = (DependencyGraphView)frame.Content;
		var graph = page.GraphView;

		TreeData? folder = null;
		await WaitUntil( () => ( folder = Flatten( page.Folders.RootNodes.Select( n => n.LinkedData ) )
			.FirstOrDefault( d => d.Name == "Assembly" ) ) is not null, "Assembly folder in the tree" );
		await page.OpenFolderAsync( folder!.Node! );
		await WaitUntil( () => page.Entries.Count == 5, "5 files listed in Assembly",
			() => string.Join( ", ", page.Entries.Select( e => e.Name ) ) );

		HashSet<string> Shown() => [ .. graph.Layout.Nodes.Select( n => n.Node.Name ) ];
		string Dump() => $"[{string.Join( ", ", graph.Layout.Nodes.Select( n => $"{n.Node.Name}@{n.Layer}" ) )}]";
		double X( string name ) => graph.Layout.Nodes.First( n => n.Node.Name == name ).X;
		async Task Expect( string what, params string[] names )
		{
			await WaitUntil( () => Shown().SetEquals( names ) && page.PendingLoad is { IsCompleted: true }, what, Dump );
			await WaitStable( () => graph.Layout.Nodes.Count + page.Roots.Count * 1000 );
			if( !Shown().SetEquals( names ) )
				throw new InvalidOperationException( $"{what}: shows {Dump()}" );
		}

		// children (the default): the selected file leftmost
		page.SelectEntries( "frame.sldasm" );
		await Expect( "children of frame.sldasm", "frame.sldasm", "arm.sldasm", "bolt.sldprt", "plate.sldprt" );
		if( graph.Layout.Nodes.Any( n => n.Node.Name != "frame.sldasm" && n.X <= X( "frame.sldasm" ) ) )
			throw new InvalidOperationException( $"frame.sldasm is not leftmost: {Dump()}" );
		if( X( "bolt.sldprt" ) <= X( "arm.sldasm" ) )
			throw new InvalidOperationException( $"bolt is not right of arm (it is used through arm): {Dump()}" );
		if( graph.Layout.Edges.Count != 4 )
			throw new InvalidOperationException( $"expected 4 links, got {graph.Layout.Edges.Count}" );

		// parents: the selected file rightmost
		page.SetDirection( GraphDirection.Parents );
		page.SelectEntries( "bolt.sldprt" );
		await Expect( "parents of bolt.sldprt", "bolt.sldprt", "arm.sldasm", "frame.sldasm", "frame.slddrw" );
		if( graph.Layout.Nodes.Any( n => n.Node.Name != "bolt.sldprt" && n.X >= X( "bolt.sldprt" ) ) )
			throw new InvalidOperationException( $"bolt.sldprt is not rightmost: {Dump()}" );

		// both: the selected file in the middle
		page.SetDirection( GraphDirection.Both );
		page.SelectEntries( "arm.sldasm" );
		await Expect( "both around arm.sldasm", "arm.sldasm", "frame.sldasm", "frame.slddrw", "bolt.sldprt", "plate.sldprt" );
		if( !( X( "frame.sldasm" ) < X( "arm.sldasm" ) && X( "arm.sldasm" ) < X( "plate.sldprt" ) ) )
			throw new InvalidOperationException( $"arm.sldasm is not in the middle: {Dump()}" );

		// several files at once
		page.SetDirection( GraphDirection.Children );
		page.SetSelectAll( true );
		await WaitUntil( () => page.Roots.Count == 5 && page.PendingLoad is { IsCompleted: true }, "all 5 files as roots",
			() => $"{page.Roots.Count} roots" );

		// zoom and pan
		page.SelectEntries( "frame.slddrw" );
		await Expect( "children of frame.slddrw", "frame.slddrw", "frame.sldasm", "arm.sldasm", "bolt.sldprt", "plate.sldprt" );
		var before = graph.View;
		graph.ZoomBy( 2 );
		graph.PanBy( 30, -20 );
		var after = graph.View;
		if( Math.Abs( after.Zoom - Math.Min( before.Zoom * 2, NodeGraphView.MaxZoom ) ) > 1e-6 )
			throw new InvalidOperationException( $"zoom {before.Zoom} -> {after.Zoom}" );
		graph.Fit();
		if( Math.Abs( graph.View.Zoom - before.Zoom ) > 1e-6 )
			throw new InvalidOperationException( $"fit gave {graph.View.Zoom}, first fit was {before.Zoom}" );

		var sizes = graph.Layout.Nodes.OrderBy( n => n.Node.Size ).Select( n => n.Width ).ToList();
		if( !sizes.SequenceEqual( sizes.Order() ) || sizes[ 0 ] >= sizes[ ^1 ] )
			throw new InvalidOperationException( "boxes do not grow with the file size" );

		// clicking a box opens its details: an info card and the file manager's tables
		int frameAsm = graph.Layout.Nodes.First( n => n.Node.Name == "frame.sldasm" ).Node.Id;
		page.ShowDetailTab( "History" );
		graph.SelectNode( frameAsm );
		await WaitUntil( () => page.DetailsOpen && page.HistoryRows.Count == 1, "details of frame.sldasm with 1 history row",
			() => $"open={page.DetailsOpen} history={page.HistoryRows.Count}" );
		string card = page.DetailsText;
		foreach( string expected in new[] { "frame.sldasm", "SolidWorks assembly", "1 version", "Not checked out", @"root\Projects\Assembly", "2 files: arm.sldasm, bolt.sldprt", "frame.slddrw" } )
			if( !card.Contains( expected ) )
				throw new InvalidOperationException( $"info card lacks '{expected}': {card}" );
		page.ShowDetailTab( "Dependents" );
		await WaitUntil( () => page.ChildRows.Count == 2, "Dependents table: arm and bolt", () => $"{page.ChildRows.Count} rows" );
		page.ShowDetailTab( "Where Used" );
		await WaitUntil( () => page.ParentRows.Count == 1, "Where Used table: frame.slddrw", () => $"{page.ParentRows.Count} rows" );
		page.ShowDetailTab( "History" );
		await WaitUntil( () => page.HistoryRows.Count == 1, "History table again" );

		// the seeded files have no preview image: the card says so once the server answered
		await WaitUntil( () => page.DetailsText.StartsWith( "no preview" ), "preview looked up", () => page.DetailsText );

		string shot = await SaveScreenshotAsync( page );

		// themes: the app's colors and shapes, with the open details kept
		shot += await ThemeChecksAsync( page, frameAsm );

		graph.SelectNode( frameAsm );
		page.CloseDetailsForTest();
		if( page.DetailsOpen )
			throw new InvalidOperationException( "details did not close" );
		return $"children/parents/both laid out, 5 roots with select all, zoom {before.Zoom:0.00}->{after.Zoom:0.00}, details card + tables{shot}";
	}

	/// <summary>
	/// The theme presets. Normally: switched live (not saved) from Dark / Default: Navy blue recolors the running app,
	/// Light previews until a restart, every style redraws the graph and recolors the panels; then back to Dark / Default.
	/// With HACKPDM_SMOKE_THEME ("Palette:Form") the app started in that preset: what only startup sets is checked
	/// (base theme, density, corners, recolored brushes) and the graph, file manager and settings are pictured.
	/// </summary>
	private static async Task<string> ThemeChecksAsync( DependencyGraphView page, int fileId )
	{
		var theme = AppComposition.Theme;
		var graph = page.GraphView;
		var resources = Application.Current.Resources;
		var root = (FrameworkElement)HackApp.Window!.Content!;
		uint Argb( string key ) => ( (Microsoft.UI.Xaml.Media.SolidColorBrush)resources[ key ] ).Color is var c
			? (uint)c.A << 24 | (uint)c.R << 16 | (uint)c.G << 8 | c.B : 0;
		void Check( bool ok, string what )
		{
			if( !ok )
				throw new InvalidOperationException( $"{theme.Palette} + {theme.Form}: {what}" );
		}
		void CheckColors( AppPalette p, AppForm f )
		{
			foreach( var (key, argb) in p.Brushes )
				Check( Argb( key ) == argb, $"{key} is {Argb( key ):X8}, the preset says {argb:X8}" );
			Check( Argb( ThemeService.PanelStrokeKey ) == ThemePresets.PanelStroke( p, f ), "panel border color" );
		}

		string? startup = Environment.GetEnvironmentVariable( ThemeService.SmokeThemeVariable );
		if( startup is { Length: > 0 } )
		{
			AppPalette p = ThemePresets.Palette( theme.Palette );
			AppForm f = ThemePresets.Form( theme.Form );
			Check( $"{theme.Palette}:{theme.Form}" == startup, $"started as {theme.Palette}:{theme.Form}, asked for {startup}" );
			Check( Application.Current.RequestedTheme == ( p.IsDark ? ApplicationTheme.Dark : ApplicationTheme.Light ), "base theme" );
			Check( ThemeService.IsCompactDensity( resources ) == f.CompactDensity, $"compact density should be {f.CompactDensity}" );
			Check( ( (CornerRadius)resources[ ThemeService.PanelCornerKey ] ).TopLeft == f.PanelCornerRadius, "panel corners" );
			Check( ( (CornerRadius)resources[ "ControlCornerRadius" ] ).TopLeft == f.ControlCornerRadius, "control corners" );
			Check( !theme.NeedsRestart && !theme.StyleNeedsRestart, "a restart asked for right after starting" );
			CheckColors( p, f );
			Check( graph.Theme == theme.GraphTheme, "graph colors" );

			string tag = startup.Replace( ':', '-' ).ToLowerInvariant();
			string shots = await SaveScreenshotAsync( root, $"{tag}-graph" );
			var frame = HackApp.HomePage!.ContentFrame;
			AppComposition.Navigation.NavigateTo( NavigatePageMenu.HackFileManager );
			await WaitUntil( () => frame.Content is HackFileManagerView { IsLoaded: true } fm && fm.ViewModel.IsListLoaded, "file manager shown" );
			shots += await SaveScreenshotAsync( root, $"{tag}-files" );
			AppComposition.Navigation.NavigateTo( NavigatePageMenu.Settings );
			await WaitUntil( () => frame.Content is ApplicationSettingsView { IsLoaded: true }, "settings shown" );
			await Task.Delay( 1000 );
			shots += await SaveScreenshotAsync( root, $"{tag}-settings" );
			return $"; started as {startup}: density, corners, colors as the preset{shots}";
		}

		uint darkPage = Argb( "ApplicationPageBackgroundThemeBrush" );
		List<string> seen = [];
		foreach( var (palette, form) in new[] { (PaletteName.NavyBlue, FormName.Fancy), (PaletteName.Light, FormName.Modern), (PaletteName.Dark, FormName.Compact) } )
		{
			theme.SetPalette( palette );
			theme.SetForm( form );
			AppPalette p = ThemePresets.Palette( palette );
			AppForm f = ThemePresets.Form( form );
			await WaitUntil( () => graph.Theme == theme.GraphTheme && graph.Layout.Nodes.Count == 5, $"{palette} + {form} graph drawn",
				() => $"{graph.Theme.Palette.Name} + {graph.Theme.Form.Name}, {graph.Layout.Nodes.Count} boxes" );
			Check( graph.SelectedNode?.Id == fileId && page.DetailsOpen, "the clicked file's details were lost" );
			var box = graph.Layout.Find( fileId )!;
			Check( Math.Abs( box.Width - GraphLayout.BoxSize( box.Node.Size, graph.Theme.Form.Layout ).Width ) < 0.01, "graph box size" );
			bool live = p.IsDark == ( Application.Current.RequestedTheme == ApplicationTheme.Dark );
			Check( theme.NeedsRestart != live, live ? "asks for a restart" : "should ask for a restart (other base theme)" );
			if( live )
				CheckColors( p, f );
			else
				Check( Argb( ThemeService.PanelStrokeKey ) == ThemePresets.PanelStroke( p, f ), "panel border color" );
			Check( root.RequestedTheme == ( p.IsDark ? ElementTheme.Dark : ElementTheme.Light ), "window preview theme" );
			Check( theme.StyleNeedsRestart, "a style switch should ask for a restart (controls)" );
			seen.Add( $"{palette}+{form}{( live ? " live" : " preview" )}" + await SaveScreenshotAsync( root, $"live-{palette}-{form}".ToLowerInvariant() ) );
		}
		theme.SetPalette( PaletteName.Dark );
		theme.SetForm( FormName.Default );
		await WaitUntil( () => graph.Theme == GraphTheme.Default, "back to Dark + Default" );
		Check( Argb( "ApplicationPageBackgroundThemeBrush" ) == darkPage, "dark colors not restored" );
		Check( !theme.NeedsRestart && !theme.StyleNeedsRestart, "still asks for a restart after switching back" );
		return "; themes: " + string.Join( ", ", seen );
	}

	/// <summary>Saves <paramref name="element"/> as a PNG to HACKPDM_SMOKE_SCREENSHOT, if set; says where (or why not).</summary>
	private static async Task<string> SaveScreenshotAsync( UIElement element, string? suffix = null )
	{
		string? path = Environment.GetEnvironmentVariable( "HACKPDM_SMOKE_SCREENSHOT" );
		if( path is not { Length: > 0 } )
			return "";
		if( suffix is not null )
			path = Path.Combine( Path.GetDirectoryName( path ) ?? "", $"{Path.GetFileNameWithoutExtension( path )}-{suffix}{Path.GetExtension( path )}" );
		try
		{
			await Task.Delay( 300 );
			var bitmap = new Microsoft.UI.Xaml.Media.Imaging.RenderTargetBitmap();
			await bitmap.RenderAsync( element );
			var pixels = await bitmap.GetPixelsAsync();
			byte[] bgra = System.Runtime.InteropServices.WindowsRuntime.WindowsRuntimeBufferExtensions.ToArray( pixels );
			Directory.CreateDirectory( Path.GetDirectoryName( Path.GetFullPath( path ) )! );
			await File.WriteAllBytesAsync( path, Png.FromBgra( bgra, bitmap.PixelWidth, bitmap.PixelHeight ) );
			return $"; screenshot {bitmap.PixelWidth}x{bitmap.PixelHeight} at {path}";
		}
		catch( Exception e )
		{
			return $"; no screenshot: {e.GetType().Name}: {e.Message}";
		}
	}

	// ------------------------------------------------------------------ helpers

	private static IEnumerable<TreeData> Flatten( IEnumerable<TreeData> nodes )
	{
		foreach( var node in nodes )
		{
			yield return node;
			foreach( var child in Flatten( node.Children ?? [] ) )
				yield return child;
		}
	}

	private static async Task WaitUntil( Func<bool> condition, string what, Func<string>? detail = null )
	{
		var sw = Stopwatch.StartNew();
		while( !condition() )
		{
			if( sw.Elapsed > StepTimeout )
				throw new TimeoutException( $"timed out waiting for {what}{( detail is null ? "" : $" ({detail()})" )}" );
			await Task.Delay( 100 );
		}
	}

	private static async Task WaitStable( Func<int> value, int quietMs = 750 )
	{
		int last = value();
		var quiet = Stopwatch.StartNew();
		var total = Stopwatch.StartNew();
		while( quiet.ElapsedMilliseconds < quietMs && total.Elapsed < StepTimeout )
		{
			await Task.Delay( 100 );
			int now = value();
			if( now != last )
			{
				last = now;
				quiet.Restart();
			}
		}
	}

	private static async Task<bool> Step( string name, Func<Task<string>> body )
	{
		var sw = Stopwatch.StartNew();
		Interlocked.Exchange( ref _maxUiStallMs, 0 );
		try
		{
			string detail = await body();
			long stall = Interlocked.Read( ref _maxUiStallMs );
			_steps.Add( new( name, true, sw.ElapsedMilliseconds, stall, detail ) );
			Log( $"PASS {name} ({sw.ElapsedMilliseconds} ms, max UI stall {stall} ms): {detail}" );
			return _unhandled.Count == 0;
		}
		catch( Exception ex )
		{
			long stall = Interlocked.Read( ref _maxUiStallMs );
			_steps.Add( new( name, false, sw.ElapsedMilliseconds, stall, ex.Message ) );
			Log( $"FAIL {name} ({sw.ElapsedMilliseconds} ms, max UI stall {stall} ms): {ex}" );
			return false;
		}
	}

	private static long _maxUiStallMs;

	/// <summary>
	/// Measures UI responsiveness: every 16 ms a background loop posts a no-op to the UI
	/// dispatcher and records how long it waited to run. The worst wait during a step is
	/// the longest time the UI thread was blocked (a frozen window from the user's view).
	/// </summary>
	private static void StartUiStallProbe()
		=> _ = Task.Run( async () =>
		{
			while( Volatile.Read( ref _finished ) == 0 )
			{
				var sw = Stopwatch.StartNew();
				var ran = new TaskCompletionSource( TaskCreationOptions.RunContinuationsAsynchronously );
				if( !UiDispatcher.Post( () => ran.TrySetResult() ) )
					return;
				await ran.Task;
				long waited = sw.ElapsedMilliseconds;
				long current;
				while( waited > ( current = Interlocked.Read( ref _maxUiStallMs ) )
					&& Interlocked.CompareExchange( ref _maxUiStallMs, waited, current ) != current ) { }
				await Task.Delay( 16 );
			}
		} );

	private static void Log( string message )
	{
		Console.WriteLine( $"[smoke {_total.Elapsed:mm\\:ss\\.fff}] {message}" );
		Console.Out.Flush();
	}

	private static void Finish( int exitCode )
	{
		if( Interlocked.Exchange( ref _finished, 1 ) == 1 )
			return;

		string path = Env( "HACKPDM_SMOKE_REPORT", Path.Combine( Environment.CurrentDirectory, "smoke-report.json" ) );
		try
		{
			var report = new
			{
				passed = exitCode == 0,
				exitCode,
				totalMs = _total.ElapsedMilliseconds,
				steps = _steps,
				unhandledExceptions = _unhandled,
			};
			File.WriteAllText( path, JsonSerializer.Serialize( report, new JsonSerializerOptions { WriteIndented = true } ) );
			Log( $"report written to {path}" );
		}
		catch( Exception ex )
		{
			Log( $"could not write report: {ex.Message}" );
		}
		Log( exitCode == 0 ? "SMOKE TEST PASSED" : $"SMOKE TEST FAILED (exit {exitCode})" );
		Environment.Exit( exitCode );
	}

	private sealed record StepResult( string Name, bool Passed, long DurationMs, long MaxUiStallMs, string Detail );
}
