using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

using HackPDM.Core.Hack;
using HackPDM.Infrastructure.Odoo;
using HackPDM.Shared.GlobalData;
using HackPDM.UI.Controls;
using HackPDM.UI.Forms;
using HackPDM.UI.Forms.Hack;
using HackPDM.UI.Forms.Settings;
using HackPDM.UI.Types;

using Microsoft.UI.Xaml;

namespace HackPDM.UI.Testing;

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

	public static bool IsEnabled
		=> Environment.GetEnvironmentVariable( "HACKPDM_SMOKE_TEST" ) is "1" or "true" or "TRUE" or "True";

	private static string Env( string name, string fallback )
		=> Environment.GetEnvironmentVariable( name ) is { Length: > 0 } v ? v : fallback;

	private static TimeSpan StepTimeout => TimeSpan.FromSeconds( 60 );

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

		HackApp.DispatcherQueue.TryEnqueue( async () =>
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
		HackFileManager? page = null;
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
				odoo.OdooUrl = $"http://{address}:{port}";
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
				var pm = InstanceManager.PM;
				ProfileManager.IsLoggedIn = await pm.AbleToLoginAsync();
				if( !ProfileManager.IsLoggedIn )
				{
					string reasons = string.Join( "; ", pm.OStatus.Select( s => s.Message ) );
					throw new InvalidOperationException(
						$"login rejected: {( reasons.Length > 0 ? reasons : "no status" )}; rpc: {OdooClient.LatestException}" );
				}
				var odoo = OdooDefaults.Instance;
				return $"uid={odoo.OdooId} node={odoo.MyNode?.name} root={odoo.HpDirectoryRoot?.name}";
			} )
			&& await Step( "navigate to file manager", async () =>
			{
				// same call the Profile Manager makes after a successful login
				Home.NavigateToPage( NavigatePageMenu.HackFileManager );
				var frame = HackApp.HomePage?.ContentFrame
					?? throw new InvalidOperationException( "home page has no content frame" );

				await WaitUntil( () => frame.Content is HackFileManager { IsLoaded: true }, "HackFileManager page loaded",
					() => $"frame shows {frame.Content?.GetType().Name ?? "nothing"}" );
				page = (HackFileManager)frame.Content;
				return "HackFileManager loaded";
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
						+ $"; root={OdooDefaults.Instance.HpDirectoryRoot?.name}#{OdooDefaults.Instance.HpDirectoryRoot?.id}; rpc: {OdooClient.LatestException}" );

				return $"{all.Count} nodes; opening {directory.FullPath} (id {directory.DirectoryId})";
			} )
			&& await Step( "open directory (entries)", async () =>
			{
				var vm = page!.ViewModel;
				int min = int.TryParse( Env( "HACKPDM_SMOKE_MIN_ENTRIES", "1" ), out var m ) ? m : 1;

				// selecting the node in the tree raises SelectionChanged -> TreeHelp.TreeSelectItem
				directory!.EnsureVisible( page.GetOdooDirectoryTree() );

				var master = vm.GroupedEntries?.Master
					?? throw new InvalidOperationException( "view model has no entry collection" );
				await WaitUntil( () => page.LastSelectedNode == directory.Node, "tree selection applied" );
				await WaitUntil( () => master.Count >= min && vm._treeItemChange is { IsCompleted: true },
					$">= {min} entries listed", () => $"{master.Count} listed" );
				await WaitStable( () => master.Count );

				var grid = page.GetOdooEntryList();
				return $"{master.Count} entries ({string.Join( ", ", master.Take( 10 ).Select( e => e.Name ) )}); grid rows={( grid.ItemsSource as System.Collections.ICollection )?.Count}";
			} )
			&& await Step( "select entry (history)", async () =>
			{
				var vm = page!.ViewModel;
				var entry = vm.GroupedEntries!.Master.FirstOrDefault( e => e.Id is > 0 )
					?? throw new InvalidOperationException( "no entry that exists on the server" );

				page.LowerTabIndex = page.HistoryTabItem;
				page.GetOdooEntryList().SelectedItem = entry;

				await WaitUntil( () => vm.OHistories.Count > 0, $"history rows for {entry.Name}" );
				return $"{entry.Name}: {vm.OHistories.Count} history row(s)";
			} );
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
		try
		{
			string detail = await body();
			_steps.Add( new( name, true, sw.ElapsedMilliseconds, detail ) );
			Log( $"PASS {name} ({sw.ElapsedMilliseconds} ms): {detail}" );
			return _unhandled.Count == 0;
		}
		catch( Exception ex )
		{
			_steps.Add( new( name, false, sw.ElapsedMilliseconds, ex.Message ) );
			Log( $"FAIL {name} ({sw.ElapsedMilliseconds} ms): {ex}" );
			return false;
		}
	}

	private static void Log( string message )
	{
		Console.WriteLine( $"[smoke {_total.Elapsed:mm\\:ss\\.fff}] {message}" );
		Console.Out.Flush();
	}

	private static void Finish( int exitCode )
	{
		if( System.Threading.Interlocked.Exchange( ref _finished, 1 ) == 1 )
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

	private sealed record StepResult( string Name, bool Passed, long DurationMs, string Detail );
}
