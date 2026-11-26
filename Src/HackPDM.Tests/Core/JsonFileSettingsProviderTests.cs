using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;


using Xunit;

namespace HackPDM.Tests.Core;

/// <summary>The settings file that replaced the packaged app's LocalSettings.</summary>
public sealed class JsonFileSettingsProviderTests : IDisposable
{
	private readonly string _dir = Path.Combine( Path.GetTempPath(), $"hackpdm-settings-{Guid.NewGuid():N}" );
	private string FilePath => Path.Combine( _dir, "config", "settings.json" );

	public void Dispose()
	{
		if( Directory.Exists( _dir ) )
			Directory.Delete( _dir, recursive: true );
	}

	[Fact]
	public void Values_AreKeptAcrossRuns_WithTheirTypes()
	{
		JsonFileSettingsProvider first = new( FilePath );
		Assert.False( first.Existed );
		first.Set( "OdooAddress", "10.0.0.68" );
		first.Set( "OdooPort", "8069" );
		first.Set( "AreaFactor", 0.0254m ); // decimal: LocalSettings could not hold this one
		first.Set( "Retries", 3 );
		first.Set( "Enabled", true );

		JsonFileSettingsProvider second = new( FilePath ); // the next run
		Assert.True( second.Existed );
		Assert.Equal( "10.0.0.68", second.Get<string>( "OdooAddress" ) );
		Assert.Equal( "8069", second.Get<string>( "OdooPort" ) );
		Assert.Equal( 0.0254m, second.Get<decimal>( "AreaFactor" ) );
		Assert.Equal( 3, second.Get<int>( "Retries" ) );
		Assert.True( second.Get<bool>( "Enabled" ) );
	}

	[Fact]
	public void MissingOrMismatchedValues_GiveTheDefault()
	{
		JsonFileSettingsProvider settings = new( FilePath );
		settings.Set( "OdooPort", "not a number" );

		Assert.Equal( "fallback", settings.Get( "Nothing", "fallback" ) );
		Assert.Equal( 8069, settings.Get( "OdooPort", 8069 ) ); // saved as text, asked as a number
		Assert.Null( settings.Get<string>( "Nothing" ) );
	}

	[Fact]
	public void SettingNull_RemovesTheKey()
	{
		JsonFileSettingsProvider settings = new( FilePath );
		settings.Set( "SwLicenseKey", "abc" );
		settings.Set<string?>( "SwLicenseKey", null );

		Assert.False( settings.Contains( "SwLicenseKey" ) );
		Assert.False( new JsonFileSettingsProvider( FilePath ).Contains( "SwLicenseKey" ) );
	}

	[Fact]
	public void ImportMissing_BringsOldValuesOver_WithoutReplacingNewOnes()
	{
		JsonFileSettingsProvider settings = new( FilePath );
		settings.Set( "OdooDb", "new-db" );

		int added = settings.ImportMissing(
		[
			new( "OdooDb", "old-db" ),
			new( "OdooAddress", "old-host" ),
			new( "Empty", null ),
		] );

		Assert.Equal( 1, added );
		Assert.Equal( "new-db", settings.Get<string>( "OdooDb" ) );
		Assert.Equal( "old-host", new JsonFileSettingsProvider( FilePath ).Get<string>( "OdooAddress" ) );
	}

	[Fact]
	public void UnreadableFile_StartsWithDefaults_AndIsKeptAsACopy()
	{
		Directory.CreateDirectory( Path.GetDirectoryName( FilePath )! );
		File.WriteAllText( FilePath, "{ not json" );

		JsonFileSettingsProvider settings = new( FilePath );
		Assert.Null( settings.Get<string>( "OdooAddress" ) );
		Assert.Single( settings.Errors );
		Assert.Single( Directory.GetFiles( Path.GetDirectoryName( FilePath )!, "settings.json.unreadable-*" ) );

		settings.Set( "OdooAddress", "host" );
		Assert.Equal( "host", new JsonFileSettingsProvider( FilePath ).Get<string>( "OdooAddress" ) );
	}

	[Fact]
	public void SavedFile_IsPlainJson_ForPeopleToRead()
	{
		JsonFileSettingsProvider settings = new( FilePath );
		settings.Set( "OdooDb", "odoopdm" );

		string json = File.ReadAllText( FilePath );
		Assert.Contains( "\"OdooDb\": \"odoopdm\"", json );
		Assert.Empty( Directory.GetFiles( Path.GetDirectoryName( FilePath )!, "*.tmp" ) );
	}

	[Fact]
	public void ZeroReflectionPaths_SupportFloatJsonElementAndAllPrimitives()
	{
		JsonFileSettingsProvider settings = new( FilePath );
		settings.Set( "Scale", 1.25f );
		settings.Set( "Big", 9876543210L );
		settings.Set( "Ratio", 2.5d );

		using var doc = System.Text.Json.JsonDocument.Parse( "{\"A\":42,\"B\":\"ok\"}" );
		settings.Set( "Obj", doc.RootElement );

		int added = settings.ImportMissing(
		[
			new( "ImportedFloat", 3.5f ),
			new( "ImportedBool", true ),
			new( "ImportedInt", 7 ),
			new( "ImportedLong", 1234567890123L ),
			new( "ImportedDouble", 6.25d ),
			new( "ImportedDecimal", 0.125m ),
			new( "ImportedStrings", new[] { "x", "y" } ),
		] );

		Assert.Equal( 7, added );

		JsonFileSettingsProvider reloaded = new( FilePath );
		Assert.Equal( 1.25f, reloaded.Get<float>( "Scale" ) );
		Assert.Equal( 9876543210L, reloaded.Get<long>( "Big" ) );
		Assert.Equal( 2.5d, reloaded.Get<double>( "Ratio" ) );
		Assert.Equal( 3.5f, reloaded.Get<float>( "ImportedFloat" ) );
		Assert.True( reloaded.Get<bool>( "ImportedBool" ) );
		Assert.Equal( 7, reloaded.Get<int>( "ImportedInt" ) );
		Assert.Equal( 1234567890123L, reloaded.Get<long>( "ImportedLong" ) );
		Assert.Equal( 6.25d, reloaded.Get<double>( "ImportedDouble" ) );
		Assert.Equal( 0.125m, reloaded.Get<decimal>( "ImportedDecimal" ) );

		var obj = reloaded.Get<System.Text.Json.JsonElement>( "Obj" );
		Assert.Equal( System.Text.Json.JsonValueKind.Object, obj.ValueKind );
		Assert.Equal( 42, obj.GetProperty( "A" ).GetInt32() );
		Assert.Equal( "ok", obj.GetProperty( "B" ).GetString() );
	}

	[Theory]
	[InlineData( "null" )]
	[InlineData( "[1, 2, 3]" )]
	public void NonObjectRootJson_RecordsErrorAndBacksUpFile( string content )
	{
		Directory.CreateDirectory( Path.GetDirectoryName( FilePath )! );
		File.WriteAllText( FilePath, content );

		JsonFileSettingsProvider settings = new( FilePath );
		Assert.Single( settings.Errors );
		Assert.Single( Directory.GetFiles( Path.GetDirectoryName( FilePath )!, "settings.json.unreadable-*" ) );
		Assert.Null( settings.Get<string>( "OdooAddress" ) );
	}

	[Fact]
	public async Task SetManyAndAsyncSetters_BatchAndPersistValuesCleanly()
	{
		JsonFileSettingsProvider settings = new( FilePath );
		settings.SetMany(
		[
			new( "KeyA", "alpha" ),
			new( "KeyB", 42 ),
			new( "KeyC", true ),
		] );

		await settings.SetManyAsync(
		[
			new( "KeyD", "delta" ),
			new( "KeyB", null ),
		] );
		await settings.SetAsync( "KeyE", 99L );

		JsonFileSettingsProvider reloaded = new( FilePath );
		Assert.Equal( "alpha", reloaded.Get<string>( "KeyA" ) );
		Assert.Null( reloaded.Get<int?>( "KeyB" ) );
		Assert.True( reloaded.Get<bool>( "KeyC" ) );
		Assert.Equal( "delta", reloaded.Get<string>( "KeyD" ) );
		Assert.Equal( 99L, reloaded.Get<long>( "KeyE" ) );
	}
}
