namespace HackPDM.Core.Odoo.Models;

public sealed class HpPDMCommit : HpRecord<HpPDMCommit>, IOdooModel<HpPDMCommit>
{
	public static string ModelName => "hp.pdm.commit";
	public static string[] AllFields { get; } =
	[
		"name", "job_uuid", "committed_at", "commit_finished_at",
		"committing", "committed", "errored", "message_exception",
		"commit_summary", "progress_total", "duration_seconds", "node_by", "staged_ids",
	];

	public static int CommitTimeoutMs { get; set; } = 15 * 60_000;

	public string? name { get; set; }
	public string? job_uuid { get; set; }
	public DateTime? committed_at { get; set; }
	public DateTime? commit_finished_at { get; set; }
	public bool? committing { get; set; }
	public bool? committed { get; set; }
	public bool? errored { get; set; }
	public string? message_exception { get; set; }
	public string? commit_summary { get; set; }
	public int? progress_total { get; set; }
	public float? duration_seconds { get; set; }
	public Many2One? node_by { get; set; }
	public One2Many? staged_ids { get; set; }

	public override bool TrySetField( string field, object? value )
	{
		switch( field )
		{
			case "name": name = OdooValues.ReadString( value ); return true;
			case "job_uuid": job_uuid = OdooValues.ReadString( value ); return true;
			case "committed_at": committed_at = OdooValues.ReadDateTime( value ); return true;
			case "commit_finished_at": commit_finished_at = OdooValues.ReadDateTime( value ); return true;
			case "committing": committing = OdooValues.ReadBool( value ); return true;
			case "committed": committed = OdooValues.ReadBool( value ); return true;
			case "errored": errored = OdooValues.ReadBool( value ); return true;
			case "message_exception": message_exception = OdooValues.ReadString( value ); return true;
			case "commit_summary": commit_summary = OdooValues.ReadString( value ); return true;
			case "progress_total": progress_total = OdooValues.ReadInt( value ); return true;
			case "duration_seconds": duration_seconds = OdooValues.ReadFloat( value ); return true;
			case "node_by": node_by = OdooValues.ReadMany2One( value ); return true;
			case "staged_ids": staged_ids = OdooValues.ReadOne2Many( value ); return true;
			default: return false;
		}
	}

	public override Dictionary<string, object?> ToOdoo() => new()
	{
		[ "name" ] = name,
		[ "job_uuid" ] = job_uuid,
		[ "committed_at" ] = OdooValues.OutDateTime( committed_at ),
		[ "commit_finished_at" ] = OdooValues.OutDateTime( commit_finished_at ),
		[ "committing" ] = OdooValues.OutBool( committing ),
		[ "committed" ] = OdooValues.OutBool( committed ),
		[ "errored" ] = OdooValues.OutBool( errored ),
		[ "message_exception" ] = message_exception,
		[ "commit_summary" ] = commit_summary,
		[ "progress_total" ] = progress_total,
		[ "duration_seconds" ] = OdooValues.OutFloat( duration_seconds ),
		[ "node_by" ] = node_by?.id,
		[ "staged_ids" ] = OdooValues.OutX2Many( staged_ids ),
	};

	public async Task<(bool Committed, string? Error)> ServerCommit()
	{
		Result<IReadOnlyList<OdooRow>> response = await OdooRpc.ExecuteAsync<IReadOnlyList<OdooRow>>(
			ModelName,
			"start_commit",
			[ id ],
			CommitTimeoutMs );
		if( !response.Ok )
		{
			Result<IReadOnlyList<OdooRow>> state = await OdooRpc.ReadAsync( ModelName, [ id ?? 0 ], [ "committed" ] );
			if( state.Value is [ var saved, .. ] && saved.Bool( "committed" ) )
			{
				committed = true;
				return ( true, null );
			}
			return ( false, response.Error );
		}
		if( response.Value is [ var row, .. ] )
		{
			bool done = row.Bool( "committed" );
			committed = done;
			errored = row.Bool( "errored" );
			message_exception = row.Text( "message_exception" );
			return done ? ( true, null ) : ( false, message_exception ?? "the server did not complete the commit" );
		}
		return ( false, "the server did not complete the commit" );
	}

	public async Task<bool> ServerClear()
		=> ( await OdooRpc.ExecuteAsync<bool>( ModelName, "clear_commit", [ id ] ) ).ValueOr( false );
}
