using System;
using System.Collections.Generic;

namespace HackPDM.Mvvm.Models.Hack;

/// <summary>Sort and group keys for the entry list, keyed by <see cref="EntryRow"/> property name.</summary>
public static class EntryRowKeys
{
	private static readonly Dictionary<string, Func<EntryRow, IComparable?>> SortKeys = new()
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

	/// <summary>Group keys differ from sort keys only for dates: rows group by day.</summary>
	private static readonly Dictionary<string, Func<EntryRow, IComparable?>> GroupKeys = new( SortKeys )
	{
		[ nameof( EntryRow.RemoteDate ) ] = e => e.RemoteDate?.Date,
		[ nameof( EntryRow.LocalDate ) ] = e => e.LocalDate?.Date,
	};

	/// <summary>The fields the list can be sorted or grouped by.</summary>
	public static IReadOnlyCollection<string> Names => SortKeys.Keys;

	public static bool TryGetSortKey( string? fieldName, out Func<EntryRow, IComparable?> key ) => TryGet( SortKeys, fieldName, out key );

	public static bool TryGetGroupKey( string? fieldName, out Func<EntryRow, IComparable?> key ) => TryGet( GroupKeys, fieldName, out key );

	private static bool TryGet( Dictionary<string, Func<EntryRow, IComparable?>> keys, string? fieldName, out Func<EntryRow, IComparable?> key )
	{
		if( fieldName is not null && keys.TryGetValue( fieldName, out var found ) )
		{
			key = found;
			return true;
		}
		key = _ => null;
		return false;
	}
}
