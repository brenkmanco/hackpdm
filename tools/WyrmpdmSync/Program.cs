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
		  init [--file <id>]     create or refresh design/penpot-sync.json: link every preset color to its token
		  fetch                  download the Penpot file to design/.cache/penpot-file.json (for inspection)
		  status [--all]         show what differs between Penpot and the code, and which side changed

		options:
		  --cached               use design/.cache/penpot-file.json instead of downloading the file again

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
		manifest.Penpot.FileName = file.Name;
		int added = ColorSync.Link( manifest, file );
		manifest.Save( paths.Manifest );

		int linked = manifest.Colors.Count( c => c.TokenId is not null );
		Console.WriteLine( $"{paths.Relative( paths.Manifest )}: file \"{file.Name}\", {manifest.Colors.Count} colors " +
		                   $"({added} new), {linked} linked to a Penpot token." );
		if( linked < manifest.Colors.Count )
			Console.WriteLine( $"{manifest.Colors.Count - linked} colors have no token of the same name in Penpot yet; `push` will create them." );
		return 0;
	}

	private static async Task<int> FetchAsync( RepoPaths paths )
	{
		SyncManifest manifest = File.Exists( paths.Manifest ) ? SyncManifest.Load( paths.Manifest ) : new SyncManifest { Penpot = { FileId = DefaultFileId } };
		PenpotFile file = await LoadFileAsync( paths, manifest, cached: false );
		Console.WriteLine( $"Saved \"{file.Name}\" (revision {file.Revn}) to {paths.Relative( paths.CachedFile )}: " +
		                   $"{file.Pages.Count} pages, {file.Tokens.Count} tokens." );
		Console.WriteLine( $"Pages: {string.Join( ", ", file.Pages.Select( p => $"{p.Name} ({p.Objects.Count} shapes)" ) )}" );
		return 0;
	}

	private static async Task<int> StatusAsync( RepoPaths paths, Options options )
	{
		SyncManifest manifest = SyncManifest.Load( paths.Manifest );
		PenpotFile file = await LoadFileAsync( paths, manifest, cached: options.Cached );
		List<ColorReport> colors = ColorSync.Compare( manifest, file );

		Console.WriteLine( $"Penpot file \"{file.Name}\", revision {file.Revn}." );
		Console.WriteLine( $"Colors: {colors.Count} linked — " + string.Join( ", ",
			colors.GroupBy( c => c.State ).OrderBy( g => g.Key ).Select( g => $"{g.Count()} {Describe( g.Key )}" ) ) + "." );
		foreach( ColorReport r in colors.Where( r => options.All || r.State != SyncState.InSync ) )
			Console.WriteLine( $"  {Mark( r.State )} {r.Link.Token,-34} penpot {r.Penpot ?? "—",-10} code {r.Code ?? "—",-10} {Describe( r.State )}" );
		return colors.Any( r => r.State != SyncState.InSync ) ? 2 : 0;
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
		SyncState.MissingInPenpot => "missing in Penpot",
		SyncState.MissingInCode => "missing in code",
		_ => state.ToString(),
	};

	private static string Mark( SyncState state ) => state switch
	{
		SyncState.InSync => " ",
		SyncState.ChangedInPenpot => "<",
		SyncState.ChangedInCode => ">",
		SyncState.Conflict => "!",
		_ => "?",
	};

	private sealed record Options( Guid? FileId, bool Cached, bool All )
	{
		public static Options Parse( string[] args )
		{
			Guid? fileId = null;
			bool cached = false, all = false;
			for( int i = 0; i < args.Length; i++ )
			{
				switch( args[ i ] )
				{
					case "--file" when i + 1 < args.Length:
						fileId = Guid.TryParse( args[ ++i ], out Guid id ) ? id : throw new SyncException( $"'{args[ i ]}' is not a Penpot file id." );
						break;
					case "--cached": cached = true; break;
					case "--all": all = true; break;
					default: throw new SyncException( $"Unknown option '{args[ i ]}'." );
				}
			}
			return new Options( fileId, cached, all );
		}
	}
}
