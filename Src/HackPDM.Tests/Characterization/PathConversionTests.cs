using System.IO;

using HackPDM.Core;
using HackPDM.Core.Hack;

using Xunit;

namespace HackPDM.Tests.Characterization;

/// <summary>
/// Pins today's behaviour of the path conversions between Odoo paths ("root / a / b"),
/// tree/node paths ("root\a\b") and local paths under the working folder (PWA), before they are
/// consolidated into one PwaPaths class (refactor Phase 3). A test that fails after a refactor
/// means behaviour changed; update it only if the change was intended.
/// </summary>
[Collection( SharedStateCollection.Name )]
public class PathConversionTests
{
	private static readonly char S = Path.DirectorySeparatorChar;

	// ---- Odoo complete name ("root / a / b") -> local path

	[Theory]
	[InlineData( "root / Projects / Parts", "Projects|Parts" )]
	[InlineData( "PWA / Projects / Parts", "Projects|Parts" )]      // PWA folder name counts as root too
	[InlineData( "Projects / Parts", "Projects|Parts" )]            // no root prefix: used as-is
	[InlineData( "root", "" )]
	public void ConvertToWindowsPath_Relative( string odooPath, string expectedSegments )
	{
		using var pwa = new PwaScope();
		Assert.Equal( Join( expectedSegments ), FileOperations.ConvertToWindowsPath( odooPath, withAbsolutePath: false ) );
	}

	[Fact]
	public void ConvertToWindowsPath_Absolute_PrefixesWorkingFolder()
	{
		using var pwa = new PwaScope();
		Assert.Equal( Path.Combine( pwa.Absolute, "Projects", "Parts" ),
			FileOperations.ConvertToWindowsPath( "root / Projects / Parts", withAbsolutePath: true ) );
	}

	[Fact]
	public void ConvertToWindowsPath_OnlySplitsOnSpacedSlash()
	{
		using var pwa = new PwaScope();
		// "a/b" without spaces is one segment in Odoo complete names
		Assert.Equal( "a/b", FileOperations.ConvertToWindowsPath( "root / a/b", withAbsolutePath: false ) );
	}

	// ---- tree/node path ("root\a\b" or "root/a/b") -> local path

	[Theory]
	[InlineData( @"root\Projects\Parts", "Projects|Parts" )]
	[InlineData( "root/Projects/Parts", "Projects|Parts" )]
	[InlineData( @"PWA\Projects", "Projects" )]
	[InlineData( @"Projects\Parts", "Projects|Parts" )]
	[InlineData( "root", "" )]
	public void NodePathToWindowsPath_Relative( string nodePath, string expectedSegments )
	{
		using var pwa = new PwaScope();
		Assert.Equal( Join( expectedSegments ), FileOperations.NodePathToWindowsPath( nodePath, withAbsolutePath: false ) );
	}

	[Fact]
	public void NodePathToWindowsPath_DefaultsToAbsolute()
	{
		using var pwa = new PwaScope();
		Assert.Equal( Path.Combine( pwa.Absolute, "Projects" ), FileOperations.NodePathToWindowsPath( @"root\Projects" ) );
	}

	// ---- local path -> Odoo complete name

	[Theory]
	[InlineData( @"PWA\Projects\Parts", "root / Projects / Parts" )]
	[InlineData( "PWA/Projects/Parts", "root / Projects / Parts" )]
	[InlineData( @"root\Projects", "root / Projects" )]
	[InlineData( @"Projects\Parts", "root / Projects / Parts" )]   // root is prepended when missing
	public void WindowsToOdooPath_Relative( string localPath, string expected )
	{
		using var pwa = new PwaScope();
		Assert.Equal( expected, FileOperations.WindowsToOdooPath( localPath ) );
	}

	[Fact]
	public void WindowsToOdooPath_FromFullPath_StripsEverythingAboveWorkingFolder()
	{
		using var pwa = new PwaScope();
		string full = Path.Combine( pwa.Absolute, "Projects", "Parts" );
		Assert.Equal( "root / Projects / Parts", FileOperations.WindowsToOdooPath( full, fromFullPath: true ) );
	}

	// ---- full file path -> directory relative to the folder above the working folder

	[Fact]
	public void GetRelativePath_ReturnsDirectoryStartingAtWorkingFolderName()
	{
		using var pwa = new PwaScope();
		string file = Path.Combine( pwa.Absolute, "Projects", "Parts", "bracket.txt" );
		Assert.Equal( Path.Combine( "PWA", "Projects", "Parts" ), FileOperations.GetRelativePath( file ) );
	}

	[Fact]
	public void GetRelativePath_OutsideWorkingFolder_IsNull()
	{
		using var pwa = new PwaScope();
		Assert.Null( FileOperations.GetRelativePath( Path.Combine( Path.GetTempPath(), "elsewhere", "x.txt" ) ) );
	}

	[Fact]
	public void InPWAFolder_ChecksPrefix()
	{
		using var pwa = new PwaScope();
		Assert.True( FileOperations.InPWAFolder( Path.Combine( pwa.Absolute, "a.txt" ) ) );
		Assert.False( FileOperations.InPWAFolder( Path.Combine( Path.GetTempPath(), "a.txt" ) ) );
		Assert.False( FileOperations.InPWAFolder( null ) );
	}

	// ---- HackDefaults.DefaultPath: always drops the first segment, whatever it is

	[Theory]
	[InlineData( @"root\Projects\Parts", "Projects|Parts" )]
	[InlineData( "anything/Projects", "Projects" )]
	[InlineData( "root", "" )]
	public void DefaultPath_Relative_DropsFirstSegment( string treePath, string expectedSegments )
	{
		using var pwa = new PwaScope();
		Assert.Equal( Join( expectedSegments ), HackDefaults.DefaultPath( treePath ) );
	}

	[Fact]
	public void DefaultPath_Empty_IsRootOrWorkingFolder()
	{
		using var pwa = new PwaScope();
		Assert.Equal( "root", HackDefaults.DefaultPath( "" ) );
		Assert.Equal( "root", HackDefaults.DefaultPath( null ) );
		Assert.Equal( pwa.Absolute, HackDefaults.DefaultPath( null, withAbsolute: true ) );
		Assert.Equal( Path.Combine( pwa.Absolute, "Projects" ), HackDefaults.DefaultPath( @"root\Projects", withAbsolute: true ) );
	}

	private static string Join( string segments )
		=> segments.Length == 0 ? "" : string.Join( S, segments.Split( '|' ) );
}
