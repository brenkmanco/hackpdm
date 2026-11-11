using System;
using System.IO;

using HackPDM.Core.Hack;

using Xunit;

namespace HackPDM.Tests.Characterization;

/// <summary>
/// Pins what the different ways of building a <see cref="HackFile"/> fill in, before the five
/// construction paths are merged into one factory (refactor Phase 5).
/// </summary>
[Collection( SharedStateCollection.Name )]
public class HackFileConstructionTests
{
	private static string CreateFile( PwaScope pwa, string content = "hello" )
	{
		string dir = Path.Combine( pwa.Absolute, "Projects", "Parts" );
		Directory.CreateDirectory( dir );
		string path = Path.Combine( dir, "bracket.txt" );
		File.WriteAllText( path, content );
		return path;
	}

	[Fact]
	public void FromExistingPath_FillsFileInfoChecksumAndRelativePath()
	{
		using var pwa = new PwaScope( create: true );
		string path = CreateFile( pwa );

		var hack = new HackFile( path );

		Assert.Equal( "bracket.txt", hack.Name );
		Assert.Equal( path, hack.FullPath );
		Assert.Equal( Path.GetDirectoryName( path ), hack.DirectoryName );
		Assert.Equal( ".txt", hack.TypeExt );
		Assert.Equal( 5, hack.FileSize );
		Assert.NotNull( hack.Info );
		// SHA-1 of "hello"
		Assert.Equal( "aaf4c61ddcc5e8a2dabede0f3b482cd9aea9434d", hack.Checksum?.ToLowerInvariant() );
		// "root" + directory below the working folder, joined with the platform separator
		Assert.Equal( Path.Combine( "root", "Projects", "Parts" ), hack.RelativePath );
	}

	[Fact]
	public void FromMissingPath_Throws()
	{
		// KNOWN BUG (refactor Phase 2): the FileInfo constructor reads Length, which throws for a
		// missing file, so a HackFile can't be built for a file that isn't on disk yet.
		using var pwa = new PwaScope( create: true );
		Assert.Throws<FileNotFoundException>( () => new HackFile( Path.Combine( pwa.Absolute, "missing.txt" ) ) );
	}

	[Fact]
	public void GetFromPath_MissingFile_WithoutFileInfo_Works()
	{
		using var pwa = new PwaScope( create: true );
		string path = Path.Combine( pwa.Absolute, "Projects", "missing.txt" );

		var hack = HackFile.GetFromPath( path, initWithFileInfo: false );

		Assert.Equal( "missing.txt", hack!.Name );
		Assert.Equal( Path.Combine( "root", "Projects" ), hack.RelativePath );
	}

	[Fact]
	public void GetFromPath_WithoutFileInfo_DoesNotHashButSetsRelativePath()
	{
		using var pwa = new PwaScope( create: true );
		string path = CreateFile( pwa );

		var hack = HackFile.GetFromPath( path, initWithFileInfo: false );

		Assert.NotNull( hack );
		Assert.Equal( "bracket.txt", hack!.Name );
		Assert.Null( hack.Info );
		Assert.Null( hack.Checksum );
		Assert.Equal( Path.Combine( "root", "Projects", "Parts" ), hack.RelativePath );
	}

	[Fact]
	public void FromFileInfo_WithoutChecksum_HashesOnDemand()
	{
		using var pwa = new PwaScope( create: true );
		string path = CreateFile( pwa );

		var hack = new HackFile( new FileInfo( path ), computeChecksum: false );

		Assert.Null( hack.Checksum );
		// NOTE: two meanings of RelativePath. The path factories store "root/<dir>"; otherwise the
		// getter falls back to "<dir>/<file>" relative to the working folder (no "root", with file name).
		Assert.Equal( Path.Combine( "Projects", "Parts", "bracket.txt" ), hack.RelativePath );
		Assert.Equal( "aaf4c61ddcc5e8a2dabede0f3b482cd9aea9434d", hack.GetChecksum()?.ToLowerInvariant() );
	}

	[Fact]
	public void FromNullFileInfo_IsEmpty()
	{
		var hack = new HackFile( (FileInfo?)null );
		Assert.Null( hack.Name );
		Assert.Null( hack.FullPath );
		Assert.Null( hack.Info );
	}

	[Fact]
	public void FromFields_UsesBasePathAsDirectoryName()
	{
		var hack = new HackFile( "bracket.txt", fullPath: null, typeExt: ".txt", basePath: "dir", relativePath: "rel",
			hpVersionId: 10, hpEntryId: 42 );

		Assert.Equal( "bracket.txt", hack.Name );
		Assert.Equal( "dir", hack.DirectoryName );
		Assert.Equal( "rel", hack.RelativePath );
		Assert.Equal( 10, hack.HpVersionId );
		Assert.Equal( 42, hack.HpEntryId );
		Assert.Null( hack.Info );
	}
}
