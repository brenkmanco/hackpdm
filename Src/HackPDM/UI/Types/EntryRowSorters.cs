using System;
using System.Collections.Generic;

namespace HackPDM.UI.Types;

/// <summary>Sort keys for the entry list, keyed by <see cref="EntryRow"/> property name.</summary>
public static class EntryRowSorters
{
	private static readonly Dictionary<string, Func<EntryRow, IComparable?>> Keys = new()
	{
		[ nameof( EntryRow.Name ) ] = e => e.Name ?? "",
		[ nameof( EntryRow.Id ) ] = e => e.Id ?? 0,
		[ nameof( EntryRow.Checkout ) ] = e => e.Checkout?.name ?? "",
		[ nameof( EntryRow.Size ) ] = e => e.Size,
		[ nameof( EntryRow.Type ) ] = e => e.Type ?? "",
		[ nameof( EntryRow.Status ) ] = e => Enum.GetName( e.Status ) ?? "",
		[ nameof( EntryRow.LatestId ) ] = e => e.LatestId,
		[ nameof( EntryRow.RemoteDate ) ] = e => e.RemoteDate,
		[ nameof( EntryRow.LocalDate ) ] = e => e.LocalDate,
		[ nameof( EntryRow.Category ) ] = e => e.Category?.name ?? "",
		[ nameof( EntryRow.FullName ) ] = e => e.FullName ?? "",
	};

	public static bool TryGet( string? fieldName, out Func<EntryRow, IComparable?> key )
	{
		if( fieldName is not null && Keys.TryGetValue( fieldName, out var found ) )
		{
			key = found;
			return true;
		}
		key = _ => null;
		return false;
	}
}
