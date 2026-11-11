using System.IO;

using HackPDM.Core;

using Xunit;

namespace HackPDM.Tests.Characterization;

/// <summary>
/// The path conversions between Odoo directory names ("root / a / b"), server names and tree
/// paths ("root\a\b"), and local paths under the working folder (PWA), all in <see cref="PwaPaths"/>.
/// These pinned the old FileOperations / HackDefaults conversions before Phase 3 merged them; the
/// expectations are unchanged except where noted.
/// </summary>
[Collection( SharedStateCollection.Name )]
public class PathConversionTests
{
	private static readonly char S = Path.DirectorySeparatorChar;

	// ---- Odoo directory name ("root / a / b") -> local path

	[Theory]
	[InlineData( "root / Projects / Parts", "Projects|Parts" )]
	[InlineData( "PWA / Projects / Parts", "Projects|Parts" )]      // PWA folder name counts as root too
	[InlineData( "Projects / Parts", "Projects|Parts" )]            // no root prefix: used as-is
	[InlineData( "root", "" )]
	public void DirectoryName_ToRelative( string odooPath, string expectedSegments )
	{
		using var pwa = new PwaScope();
		Assert.Equal( Join( expectedSegments ), PwaPaths.ToRelative( odooPath ) );
	}

	[Fact]
	public void DirectoryName_ToAbsolute_PrefixesWorkingFolder()
	{
		using var pwa = new PwaScope();
		Assert.Equal( Path.Combine( pwa.Absolute, "Projects", "Parts" ), PwaPaths.ToAbsolute( "root / Projects / Parts" ) );
	}

	[Fact]
	public void DirectoryName_OnlySplitsOnSpacedSlash()
	{
		using var pwa = new PwaScope();
		// "a/b" without spaces is one segment in Odoo complete names
		Assert.Equal( "a/b", PwaPaths.ToRelative( "root / a/b" ) );
	}

	// ---- server name / tree path ("root\a\b" or "root/a/b") -> local path

	[Theory]
	[InlineData( @"root\Projects\Parts", "Projects|Parts" )]
	[InlineData( "root/Projects/Parts", "Projects|Parts" )]
	[InlineData( @"PWA\Projects", "Projects" )]
	[InlineData( @"Projects\Parts", "Projects|Parts" )]
	[InlineData( "root", "" )]
	[InlineData( "", "" )]
	public void ServerName_ToRelative( string nodePath, string expectedSegments )
	{
		using var pwa = new PwaScope();
		Assert.Equal( Join( expectedSegments ), PwaPaths.ToRelative( nodePath ) );
	}

	[Fact]
	public void ServerName_ToAbsolute()
	{
		using var pwa = new PwaScope();
		Assert.Equal( Path.Combine( pwa.Absolute, "Projects" ), PwaPaths.ToAbsolute( @"root\Projects" ) );
		Assert.Equal( pwa.Absolute, PwaPaths.ToAbsolute( "root" ) );
		Assert.Null( PwaPaths.ToAbsolute( null ) );
	}

	[Theory]
	[InlineData( @"root\Projects\Parts\bracket.txt", "Projects|Parts" )]
	[InlineData( @"root\bracket.txt", "" )]
	[InlineData( "bracket.txt", "" )]
	public void FolderOf_DropsRootAndFileName( string serverName, string expectedSegments )
	{
		using var pwa = new PwaScope();
		Assert.Equal( Join( expectedSegments ), PwaPaths.FolderOf( serverName ) );
	}

	// ---- local path -> server forms

	[Fact]
	public void LocalPath_ToServerForms()
	{
		using var pwa = new PwaScope();
		string folder = Path.Combine( pwa.Absolute, "Projects", "Parts" );
		Assert.Equal( "root / Projects / Parts", PwaPaths.ToServerDirectory( folder ) );
		Assert.Equal( @"root\Projects\Parts\bracket.txt", PwaPaths.ToServerName( Path.Combine( folder, "bracket.txt" ) ) );
		Assert.Equal( "root", PwaPaths.ToServerName( pwa.Absolute ) );
		Assert.Equal( [ "Projects", "Parts" ], PwaPaths.LocalSegments( folder ) );
	}

	[Fact]
	public void LocalPath_OutsideWorkingFolder_HasNoServerForm()
	{
		using var pwa = new PwaScope();
		string elsewhere = Path.Combine( Path.GetTempPath(), "elsewhere", "x.txt" );
		Assert.Null( PwaPaths.ToServerName( elsewhere ) );
		Assert.Null( PwaPaths.ToServerDirectory( elsewhere ) );
		Assert.Null( PwaPaths.LocalSegments( elsewhere ) );
		Assert.Null( PwaPaths.LocalSegments( "relative/path" ) );
	}

	[Fact]
	public void IsInside_ChecksTheFolderNotJustThePrefix()
	{
		using var pwa = new PwaScope();
		Assert.True( PwaPaths.IsInside( Path.Combine( pwa.Absolute, "a.txt" ) ) );
		Assert.False( PwaPaths.IsInside( Path.Combine( Path.GetTempPath(), "a.txt" ) ) );
		// changed in Phase 3: the old prefix test (InPWAFolder) accepted a sibling folder "PWA2"
		Assert.False( PwaPaths.IsInside( pwa.Absolute + "2" + S + "a.txt" ) );
		Assert.False( PwaPaths.IsInside( null ) );
	}

	private static string Join( string segments )
		=> segments.Length == 0 ? "" : string.Join( S, segments.Split( '|' ) );
}
