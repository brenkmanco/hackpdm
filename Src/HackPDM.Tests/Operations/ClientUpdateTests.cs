using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;

using HackPDM.Infrastructure.Operations;

using Xunit;

namespace HackPDM.Tests.Operations;

public class ClientUpdateTests : IDisposable
{
	private readonly string _dir = Path.Combine( Path.GetTempPath(), $"hackpdm-update-test-{Guid.NewGuid():N}" );

	public ClientUpdateTests() => Directory.CreateDirectory( _dir );

	public void Dispose()
	{
		try { Directory.Delete( _dir, true ); } catch { }
	}

	[Theory]
	[InlineData( "1.1.0.0", "1.0.0.27", false )]
	[InlineData( "1.1.0.0", "1.1", false )]
	[InlineData( "1.1.0.0", "1.1.0", false )]
	[InlineData( "1.1.0.0", "1.1.0.1", true )]
	[InlineData( "1.1.0.0", "1.2", true )]
	[InlineData( "1.1.0.0", "2.0.0", true )]
	[InlineData( "1.1.0.0", "", false )]
	[InlineData( "1.1.0.0", null, false )]
	[InlineData( "1.1.0.0", "not a version", false )]
	[InlineData( "1.1.0.0", " 1.2.0 ", true )]
	public void IsOutdated_ComparesWithTheServersMinimum( string current, string? required, bool outdated )
		=> Assert.Equal( outdated, ClientUpdate.IsOutdated( Version.Parse( current ), required ) );

	[Fact]
	public void Current_IsTheBuildVersion()
	{
		Assert.True( ClientUpdate.Current >= new Version( 1, 1 ), ClientUpdate.Current.ToString() );
		Assert.StartsWith( $"{ClientUpdate.Current.Major}.{ClientUpdate.Current.Minor}.", ClientUpdate.CurrentText );
	}

	private (string Path, string Sha256) Installer( string name = "HackPDM-setup.exe" )
	{
		byte[] bytes = RandomNumberGenerator.GetBytes( 70_000 );
		string path = Path.Combine( _dir, "published", name );
		Directory.CreateDirectory( Path.GetDirectoryName( path )! );
		File.WriteAllBytes( path, bytes );
		return (path, Convert.ToHexStringLower( SHA256.HashData( bytes ) ));
	}

	[Fact]
	public async Task Download_FromAPathOrFileUri_ChecksTheHash()
	{
		var (published, sha) = Installer();
		foreach( string source in new[] { published, new Uri( published ).AbsoluteUri } )
		{
			string target = Path.Combine( _dir, Guid.NewGuid().ToString( "N" ) );
			var got = await ClientUpdate.DownloadInstallerAsync( source, sha.ToUpperInvariant(), target );
			Assert.True( got.Ok, got.Error );
			Assert.Equal( Path.Combine( target, "HackPDM-setup.exe" ), got.Value );
			Assert.Equal( File.ReadAllBytes( published ), File.ReadAllBytes( got.Value! ) );
		}
	}

	[Fact]
	public async Task Download_WithTheWrongHash_LeavesNothing()
	{
		var (published, _) = Installer();
		string target = Path.Combine( _dir, "out" );
		var got = await ClientUpdate.DownloadInstallerAsync( published, new string( 'a', 64 ), target );
		Assert.False( got.Ok );
		Assert.Contains( "does not match", got.Error );
		Assert.Empty( Directory.EnumerateFileSystemEntries( target ) );
	}

	[Theory]
	[InlineData( null, "x" )]
	[InlineData( "", "x" )]
	[InlineData( "C:/setup.exe", null )]
	[InlineData( "C:/setup.exe", "" )]
	[InlineData( "C:/setup.exe", "abc" )]
	[InlineData( "C:/setup.exe", "zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz" )]
	public async Task Download_NeedsAPlaceAndAHash( string? source, string? sha256 )
	{
		var got = await ClientUpdate.DownloadInstallerAsync( source, sha256, Path.Combine( _dir, "out" ) );
		Assert.False( got.Ok );
	}

	[Fact]
	public async Task Download_OfAMissingFile_FailsCleanly()
	{
		string target = Path.Combine( _dir, "out" );
		var got = await ClientUpdate.DownloadInstallerAsync( Path.Combine( _dir, "nope.exe" ), new string( 'a', 64 ), target );
		Assert.False( got.Ok );
		Assert.Contains( "could not download", got.Error );
		Assert.Empty( Directory.EnumerateFileSystemEntries( target ) );
	}
}
