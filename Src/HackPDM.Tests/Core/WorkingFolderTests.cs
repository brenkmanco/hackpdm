using HackPDM.Core;
using HackPDM.Core.Hack;
using HackPDM.Infrastructure.Odoo;
using HackPDM.Shared.GlobalData;
using HackPDM.Tests.Characterization;

namespace HackPDM.Tests.Core;

/// <summary>Working-folder paths and the commit dependency tree when the working folder changes.</summary>
[Collection( SharedStateCollection.Name )]
public class WorkingFolderTests
{
	[Fact]
	public void PwaPathRelative_FollowsTheWorkingFolder()
	{
		using var scope = new PwaScope();
		string other = Path.Combine( Path.GetTempPath(), $"hackpdm-other-{Guid.NewGuid():N}", "Second" );
		string first = HackDefaults.Instance.PwaPathAbsolute;
		try
		{
			// the name was worked out once and kept, so it went on naming the first folder
			HackDefaults.Instance.PwaPathAbsolute = other;
			Assert.Equal( "Second", HackDefaults.Instance.PwaPathRelative );
			// the working folder's name stands for root in server paths
			Assert.Equal( Path.Combine( other, "sub" ), PwaPaths.ToAbsolute( @"Second\sub" ) );
		}
		finally
		{
			HackDefaults.Instance.PwaPathAbsolute = first;
		}
	}

	[Fact]
	public void IsInside_IgnoresFoldersThatOnlyStartWithTheWorkingFolderName()
	{
		using var scope = new PwaScope();
		Assert.False( PwaPaths.IsInside( Path.Combine( scope.Absolute + "2", "a.txt" ) ) );
		Assert.True( PwaPaths.IsInside( Path.Combine( scope.Absolute, "a.txt" ) ) );
	}

	[Fact]
	public async Task UnreadableSolidWorksFile_BreaksTheTree_InsteadOfLookingLikeItHasNoReferences()
	{
		using var scope = new PwaScope( create: true );
		string path = Path.Combine( scope.Absolute, "damaged.SLDASM" );
		File.WriteAllText( path, "not a SolidWorks document" );

		var (success, files, broken) = await new HackFile( path ).GetHackFileWithDependencies();

		Assert.False( success );
		Assert.Empty( files );
		Assert.Contains( broken, b => b.Value?.Result == HackResult.CorruptDepFile );
	}
}
