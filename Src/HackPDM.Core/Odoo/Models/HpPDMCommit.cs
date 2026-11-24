namespace HackPDM.Core.Odoo.Models;

public sealed class HpPDMCommit : HpRecord<HpPDMCommit>, IOdooModel<HpPDMCommit>
{
	public static string ModelName => "hp.pdm.commit";
	public static string[] AllFields { get; } =
	[
		nameof( name ), nameof( committed_at ), nameof( commit_finished_at ),
		nameof( committed ), nameof( errored ), nameof( message_exception ),
		nameof( commit_summary ), nameof( node_by ), nameof( staged_ids ),
	];

	public static int CommitTimeoutMs { get; set; } = 15 * 60_000;

	public string? name { get; set; }
	public DateTime? committed_at { get; set; }
	public DateTime? commit_finished_at { get; set; }
	public bool? committed { get; set; }
	public bool? errored { get; set; }
	public string? message_exception { get; set; }
	public string? commit_summary { get; set; }
	public Many2One? node_by { get; set; }
	public One2Many? staged_ids { get; set; }

	public override bool TrySetField( string field, object? value )
	{
		switch( field )
		{
			case nameof( name ): name = OdooValues.ReadString( value ); return true;
			case nameof( committed_at ): committed_at = OdooValues.ReadDateTime( value ); return true;
			case nameof( commit_finished_at ): commit_finished_at = OdooValues.ReadDateTime( value ); return true;
			case nameof( committed ): committed = OdooValues.ReadBool( value ); return true;
			case nameof( errored ): errored = OdooValues.ReadBool( value ); return true;
			case nameof( message_exception ): message_exception = OdooValues.ReadString( value ); return true;
			case nameof( commit_summary ): commit_summary = OdooValues.ReadString( value ); return true;
			case nameof( node_by ): node_by = OdooValues.ReadMany2One( value ); return true;
			case nameof( staged_ids ): staged_ids = OdooValues.ReadOne2Many( value ); return true;
			default: return false;
		}
	}

	public override Dictionary<string, object?> ToOdoo() => new()
	{
		[ nameof( name ) ] = name,
		[ nameof( committed_at ) ] = OdooValues.OutDateTime( committed_at ),
		[ nameof( commit_finished_at ) ] = OdooValues.OutDateTime( commit_finished_at ),
		[ nameof( committed ) ] = OdooValues.OutBool( committed ),
		[ nameof( errored ) ] = OdooValues.OutBool( errored ),
		[ nameof( message_exception ) ] = message_exception,
		[ nameof( commit_summary ) ] = commit_summary,
		[ nameof( node_by ) ] = node_by?.id,
		[ nameof( staged_ids ) ] = OdooValues.OutX2Many( staged_ids ),
	};

	public async Task<(bool Committed, string? Error)> ServerCommit( CancellationToken token = default )
	{
		Result<IReadOnlyList<OdooRow>> response = await OdooRpc.ExecuteAsync<IReadOnlyList<OdooRow>>(
			ModelName,
			"start_commit",
			[ id ],
			CommitTimeoutMs,
			token );
		if( !response.Ok )
		{
			Result<IReadOnlyList<OdooRow>> state = await OdooRpc.ReadAsync( ModelName, [ id ?? 0 ], [ nameof( committed ) ], token: token );
			if( state.Value is [ var saved, .. ] && saved.Bool( nameof( committed ) ) )
			{
				committed = true;
				return ( true, null );
			}
			return ( false, response.Error );
		}
		if( response.Value is [ var row, .. ] )
		{
			bool done = row.Bool( nameof( committed ) );
			committed = done;
			errored = row.Bool( nameof( errored ) );
			message_exception = row.Text( nameof( message_exception ) );
			return done ? ( true, null ) : ( false, message_exception ?? "the server did not complete the commit" );
		}
		return ( false, "the server did not complete the commit" );
	}

	public async Task<bool> ServerClear( CancellationToken token = default )
		=> ( await OdooRpc.ExecuteAsync<bool>( ModelName, "clear_commit", [ id ], token: token ) ).ValueOr( false );
}
