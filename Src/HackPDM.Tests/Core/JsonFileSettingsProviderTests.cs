using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using HackPDM.Core.Configuration;

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
}
