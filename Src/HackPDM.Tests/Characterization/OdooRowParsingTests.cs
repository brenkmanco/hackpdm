using System;
using System.Collections;
using System.Text.Json;

using HackPDM.Infrastructure.Odoo;
using HackPDM.Infrastructure.Odoo.Models;

using Xunit;

namespace HackPDM.Tests.Characterization;

/// <summary>
/// Pins how a JSON-RPC result becomes Hashtable/ArrayList values (<see cref="OdooFieldHelpers.ToNativeObject"/>)
/// and how a row becomes a model (<see cref="HashConverter"/>), before both are replaced by typed
/// deserialization (refactor Phase 4).
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

	private static object? Parse( string json ) => JsonDocument.Parse( json ).RootElement.ToNativeObject();

	[Fact]
	public void ToNativeObject_MapsJsonKinds()
	{
		var row = Assert.IsType<Hashtable>( Parse( """{"i":1,"l":5000000000,"d":1.5,"s":"x","t":true,"f":false,"n":null,"a":[1,"two"],"o":{"k":2}}""" ) );

		Assert.Equal( 1, Assert.IsType<int>( row[ "i" ] ) );
		Assert.Equal( 5000000000L, Assert.IsType<long>( row[ "l" ] ) );
		Assert.Equal( 1.5, Assert.IsType<double>( row[ "d" ] ) );
		Assert.Equal( "x", row[ "s" ] );
		Assert.Equal( true, row[ "t" ] );
		Assert.Equal( false, row[ "f" ] );
		Assert.True( row.ContainsKey( "n" ) );
		Assert.Null( row[ "n" ] );

		var a = Assert.IsType<ArrayList>( row[ "a" ] );
		Assert.Equal( new object[] { 1, "two" }, a.ToArray() );
		Assert.Equal( 2, Assert.IsType<Hashtable>( row[ "o" ] )[ "k" ] );
	}

	[Fact]
	public void ToNativeObject_ReadResultIsArrayListOfHashtables()
	{
		var rows = Assert.IsType<ArrayList>( Parse( $"[{EntryRowJson},{EntryRowJson}]" ) );
		Assert.Equal( 2, rows.Count );
		Assert.All( rows.Cast<object>(), r => Assert.IsType<Hashtable>( r ) );
	}

	[Fact]
	public void ConvertToClass_PopulatesMappedFields()
	{
		var row = Assert.IsType<Hashtable>( Parse( EntryRowJson ) );
		HpEntry? entry = HashConverter.ConvertToClass<HpEntry>( row );

		Assert.NotNull( entry );
		Assert.Equal( 42, entry!.id );
		Assert.Equal( "bracket.sldprt", entry.name );
		Assert.Equal( @"root\Projects\Parts\bracket.sldprt", entry.windows_complete_name );
		Assert.False( entry.deleted );
		Assert.Equal( new DateTime( 2026, 1, 2, 3, 4, 5 ), entry.checkout_date );

		Assert.Equal( 5, entry.dir_id?.id );
		Assert.Equal( "Parts", entry.dir_id?.name );
		Assert.Equal( 10, entry.latest_version_id?.id );
	}

	[Fact]
	public void ConvertToClass_FalseMany2One_BecomesNull()
	{
		// Odoo sends `false` for an empty many2one
		var row = Assert.IsType<Hashtable>( Parse( EntryRowJson ) );
		HpEntry entry = HashConverter.ConvertToClass<HpEntry>( row )!;

		Assert.Null( entry.checkout_user );
	}

	[Fact]
	public void ConvertToClass_UnmappedFields_GoToHashedValues()
	{
		var row = Assert.IsType<Hashtable>( Parse( EntryRowJson ) );
		HpEntry entry = HashConverter.ConvertToClass<HpEntry>( row )!;

		Assert.Equal( "kept", entry.HashedValues[ "some_unmapped_field" ] );
		Assert.False( entry.HashedValues.ContainsKey( "name" ) );
	}

	[Fact]
	public void ConvertToClasses_ConvertsEveryRow()
	{
		var rows = Assert.IsType<ArrayList>( Parse( $"[{EntryRowJson},{EntryRowJson.Replace( "\"id\": 42", "\"id\": 43" )}]" ) );
		HpEntry[]? entries = HashConverter.ConvertToClasses<HpEntry>( rows.Cast<Hashtable>() );

		Assert.NotNull( entries );
		Assert.Equal( new int?[] { 42, 43 }, entries!.Select( e => e.id ).ToArray() );
	}
}
