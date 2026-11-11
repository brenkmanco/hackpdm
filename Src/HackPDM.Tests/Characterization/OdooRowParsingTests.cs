using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

using HackPDM.Infrastructure.Odoo;
using HackPDM.Infrastructure.Odoo.Models;

using Xunit;

namespace HackPDM.Tests.Characterization;

/// <summary>
/// How a JSON-RPC result becomes plain values (<see cref="OdooValues.FromJson"/>), typed results
/// (<see cref="OdooRpc.Convert{T}"/>), models (<see cref="Repository{T}.Populate"/>) and DTOs.
/// The expectations are the ones pinned before the typed seam replaced Hashtable/ArrayList.
/// </summary>
public class OdooRowParsingTests
{
	// what Odoo's "read" returns for one hp.entry
	private const string EntryRowJson = """
		{
			"id": 42,
			"name": "bracket.sldprt",
			"windows_complete_name": "root\\Projects\\Parts\\bracket.sldprt",
			"deleted": false,
			"checkout_date": "2026-01-02 03:04:05",
			"dir_id": [5, "Parts"],
			"latest_version_id": [10, "bracket.sldprt"],
			"checkout_user": false,
			"some_unmapped_field": "kept"
		}
		""";

	private static JsonElement Json( string json ) => JsonDocument.Parse( json ).RootElement.Clone();
	private static object? Parse( string json ) => OdooValues.FromJson( Json( json ) );

	private static IReadOnlyList<OdooRow> Rows( string json ) => OdooRpc.Convert<IReadOnlyList<OdooRow>>( Json( json ) ).Value!;

	[Fact]
	public void FromJson_MapsJsonKinds()
	{
		var row = Assert.IsType<Dictionary<string, object?>>( Parse( """{"i":1,"l":5000000000,"d":1.5,"s":"x","t":true,"f":false,"n":null,"a":[1,"two"],"o":{"k":2}}""" ) );

		Assert.Equal( 1, Assert.IsType<int>( row[ "i" ] ) );
		Assert.Equal( 5000000000L, Assert.IsType<long>( row[ "l" ] ) );
		Assert.Equal( 1.5, Assert.IsType<double>( row[ "d" ] ) );
		Assert.Equal( "x", row[ "s" ] );
		Assert.Equal( true, row[ "t" ] );
		Assert.Equal( false, row[ "f" ] );
		Assert.True( row.ContainsKey( "n" ) );
		Assert.Null( row[ "n" ] );

		var a = Assert.IsType<List<object?>>( row[ "a" ] );
		Assert.Equal( new object[] { 1, "two" }, a.ToArray() );
		Assert.Equal( 2, Assert.IsType<Dictionary<string, object?>>( row[ "o" ] )[ "k" ] );
	}

	[Fact]
	public void ReadResult_IsRows()
	{
		var rows = Rows( $"[{EntryRowJson},{EntryRowJson}]" );
		Assert.Equal( 2, rows.Count );
		Assert.All( rows, r => Assert.Equal( 42, r.Id ) );
	}

	[Fact]
	public void Populate_SetsMappedFields()
	{
		HpEntry entry = Repository<HpEntry>.Populate( Rows( $"[{EntryRowJson}]" ) ).Single();

		Assert.Equal( 42, entry.id );
		Assert.Equal( "bracket.sldprt", entry.name );
		Assert.Equal( @"root\Projects\Parts\bracket.sldprt", entry.windows_complete_name );
		Assert.False( entry.deleted );
		Assert.Equal( new DateTime( 2026, 1, 2, 3, 4, 5 ), entry.checkout_date );

		Assert.Equal( 5, entry.dir_id?.id );
		Assert.Equal( "Parts", entry.dir_id?.name );
		Assert.Equal( 10, entry.latest_version_id?.id );
		Assert.True( entry.IsRecord );
	}

	[Fact]
	public void Populate_FalseMany2One_BecomesNull()
	{
		// Odoo sends `false` for an empty many2one
		HpEntry entry = Repository<HpEntry>.Populate( Rows( $"[{EntryRowJson}]" ) ).Single();
		Assert.Null( entry.checkout_user );
	}

	[Fact]
	public void Populate_UnmappedFields_GoToHashedValues()
	{
		HpEntry entry = Repository<HpEntry>.Populate( Rows( $"[{EntryRowJson}]" ) ).Single();

		Assert.Equal( "kept", entry.HashedValues.GetValueOrDefault( "some_unmapped_field" ) );
		Assert.False( entry.HashedValues.ContainsKey( "name" ) );
	}

	[Fact]
	public void Populate_ConvertsEveryRow()
	{
		var rows = Rows( $"[{EntryRowJson},{EntryRowJson.Replace( "\"id\": 42", "\"id\": 43" )}]" );
		Assert.Equal( new int?[] { 42, 43 }, Repository<HpEntry>.Populate( rows ).Select( e => e.id ).ToArray() );
	}

	[Fact]
	public void Populate_Many2Many_KeepsItsIds()
	{
		// a many2many used to read as empty: its list ignored Add
		HpVersion version = Repository<HpVersion>.Populate( Rows( """[{"id": 7, "parent_ids": [3, 4], "child_ids": []}]""" ) ).Single();

		Assert.Equal( [ 3, 4 ], (int[])version.parent_ids! );
		Assert.Empty( (int[])version.child_ids! );
	}

	[Fact]
	public void Many2Many_FromIds_DoesNotRecurse()
	{
		// the int[] conversion used to call itself until the stack overflowed
		Many2Many ids = new[] { 1, 2 };
		Assert.Equal( [ 1, 2 ], ids.IdValues );
	}

	[Fact]
	public void Convert_Scalars()
	{
		Assert.True( OdooRpc.Convert<bool>( Json( "true" ) ).Value );
		Assert.False( OdooRpc.Convert<bool>( Json( "false" ) ).Value );
		Assert.Equal( 12, OdooRpc.Convert<int>( Json( "12" ) ).Value );
		Assert.Equal( 0, OdooRpc.Convert<int>( Json( "false" ) ).Value );
		Assert.Equal( [ 1, 2, 3 ], OdooRpc.Convert<IReadOnlyList<int>>( Json( "[1, 2, 3]" ) ).Value );
		Assert.Null( OdooRpc.Convert<OdooRow>( Json( "false" ) ).Value );
	}

	[Fact]
	public void Many2OneHelpers_HandleFalse()
	{
		Assert.Equal( 5, OdooValues.Many2OneId( Parse( """[5, "Parts"]""" ) ) );
		Assert.Equal( "Parts", OdooValues.Many2OneName( Parse( """[5, "Parts"]""" ) ) );
		Assert.Null( OdooValues.Many2OneId( false ) );
		Assert.Null( OdooValues.Many2OneName( false ) );
	}

	[Fact]
	public void EntriesMap_BecomesDtos()
	{
		// hp.directory.get_entries: { name: entry }; an entry without versions has false for its ids, checksum and date
		var map = (IReadOnlyDictionary<string, object?>)Parse( """
			{
				"bracket.txt": {"id": 3, "type": "txt", "checkout": 2, "category": "Misc", "fullname": "root / Parts / bracket.txt",
					"size": 37, "latest": 9, "release": false, "latest_checksum": "abc", "latest_date": "2026-01-02 03:04:05", "deleted": false},
				"empty.txt": {"id": 4, "checkout": 0, "category": false, "fullname": "root / Parts / empty.txt",
					"size": false, "latest": false, "release": false, "latest_checksum": false, "latest_date": false, "deleted": true}
			}
			""" )!;

		var entries = RemoteEntryDto.ListFrom( map );

		Assert.Equal( new RemoteEntryDto( "bracket.txt", 3, "txt", 2, "Misc", "root / Parts / bracket.txt", 37, 9, null, "abc",
			new DateTime( 2026, 1, 2, 3, 4, 5 ), false ), entries[ 0 ] );
		Assert.Equal( new RemoteEntryDto( "empty.txt", 4, null, null, null, "root / Parts / empty.txt", 0, null, null, null, null, true ), entries[ 1 ] );
	}

	[Fact]
	public void DirectoryTree_BecomesDtos()
	{
		var row = (IReadOnlyDictionary<string, object?>)Parse( """
			{"id": 1, "name": "root", "entries": {},
			 "directories": {"Parts": {"id": 5, "name": "Parts", "directories": {}, "entries": {}}}}
			""" )!;

		SubdirectoryDto tree = SubdirectoryDto.FromRow( row );

		Assert.Equal( 1, tree.Id );
		Assert.Equal( "Parts", Assert.Single( tree.Directories ).Name );
		Assert.Equal( 5, tree.Directories[ 0 ].Id );
	}
}
