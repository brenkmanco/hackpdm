namespace WyrmpdmSync.Sync;

/// <summary>How a linked value compares between Penpot, the code and the last sync.</summary>
internal enum SyncState
{
	/// <summary>Both sides hold the same value.</summary>
	InSync,
	/// <summary>Only Penpot changed since the last sync: <c>pull</c> takes it into the code.</summary>
	ChangedInPenpot,
	/// <summary>Only the code changed since the last sync: <c>push</c> takes it to Penpot.</summary>
	ChangedInCode,
	/// <summary>Both changed to different values: needs a choice (<c>--prefer penpot|code</c>).</summary>
	Conflict,
	/// <summary>They differ and never agreed, so there is no telling which is newer.</summary>
	Unlinked,
	/// <summary>The Penpot side is gone (deleted or renamed).</summary>
	MissingInPenpot,
	/// <summary>The code side is gone (deleted or renamed).</summary>
	MissingInCode,
}

/// <summary>Three-way comparison of one value: what Penpot has, what the code has, and what both had last time.</summary>
internal static class ThreeWay
{
	public static SyncState Compare( string? penpot, string? code, string? synced )
	{
		if( penpot is null )
			return SyncState.MissingInPenpot;
		if( code is null )
			return SyncState.MissingInCode;
		if( penpot == code )
			return SyncState.InSync;
		if( synced is null )
			return SyncState.Unlinked;
		bool penpotMoved = penpot != synced, codeMoved = code != synced;
		return (penpotMoved, codeMoved) switch
		{
			(true, false) => SyncState.ChangedInPenpot,
			(false, true) => SyncState.ChangedInCode,
			_ => SyncState.Conflict,
		};
	}
}
