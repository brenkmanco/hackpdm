using HackPDM.Tests.Integration;

namespace HackPDM.Tests.Characterization;

/// <summary>Which server directories a local file belongs in (no server needed).</summary>
[Collection( SharedStateCollection.Name )]
public class CommitDirectorySegmentsTests
{
	[Fact]
	public void FileInSubfolders_GivesEachFolder()
	{
		using var pwa = new PwaScope( create: true );
		var hack = new HackFile( Path.Combine( pwa.Absolute, "Projects", "Parts", "bracket.txt" ) );
		Assert.Equal( [ "Projects", "Parts" ], CommitOperations.DirectorySegments( hack ).Cast<string>() );
	}

	[Fact]
	public void FileAtTheTop_GivesNoFolders()
	{
		using var pwa = new PwaScope( create: true );
		var hack = new HackFile( Path.Combine( pwa.Absolute, "readme.txt" ) );
		Assert.Empty( CommitOperations.DirectorySegments( hack ) );
	}

	[Fact]
	public void FileOutsideTheWorkingFolder_GivesNoFolders()
	{
		using var pwa = new PwaScope( create: true );
		var hack = new HackFile( Path.Combine( Path.GetTempPath(), "elsewhere", "x.txt" ) );
		Assert.Empty( CommitOperations.DirectorySegments( hack ) );
	}
}
