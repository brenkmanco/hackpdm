using HackPDM.Infrastructure.Hack;
using HackPDM.Core.Hack;
using HackPDM.Infrastructure.Odoo;
using HackPDM.Infrastructure.SldWrks;

namespace HackPDM.Tests.Core;

/// <summary>Stored SolidWorks references (Windows paths from the saving machine) → local files.</summary>
public sealed class SwReferenceResolverTests : IDisposable
{
	private readonly string _vault = Path.Combine( Path.GetTempPath(), $"hackpdm-refs-{Guid.NewGuid():N}", "pwa" );

	public void Dispose()
	{
		try { Directory.Delete( Path.GetDirectoryName( _vault )!, true ); } catch { }
	}

	private string Touch( params string[] segments )
	{
		string path = Path.Combine( [ _vault, .. segments ] );
		Directory.CreateDirectory( Path.GetDirectoryName( path )! );
		File.WriteAllText( path, "" );
		return path;
	}

	[Fact]
	public void PathThroughTheVaultFolder_MapsUnderTheLocalVault_IgnoringCase()
	{
		string shaft = Touch( "Projects", "Parts", "shaft.sldprt" );
		var resolver = new SwReferenceResolver( _vault );

		Assert.Equal( shaft, resolver.Resolve( @"D:\Work\PWA\PROJECTS\parts\Shaft.SLDPRT" ) );
	}

	[Fact]
	public void PathFromAnotherMachine_IsFoundNextToTheReferencingFile()
	{
		string assembly = Touch( "Robot", "drive.SLDASM" );
		string wheel = Touch( "Robot", "wheel.SLDPRT" );
		var resolver = new SwReferenceResolver( _vault );

		Assert.Equal( wheel, resolver.Resolve( @"C:\Users\someone\Desktop\Robot v2\wheel.SLDPRT", assembly ) );
	}

	[Fact]
	public void SearchingTheVault_PrefersTheCopyInTheMatchingFolder()
	{
		Touch( "Old", "Robot", "bracket.SLDPRT" );
		string wanted = Touch( "Library", "Gearbox", "bracket.SLDPRT" );
		string assembly = Touch( "Top", "robot.SLDASM" );
		var resolver = new SwReferenceResolver( _vault );

		Assert.Equal( wanted, resolver.Resolve( @"C:\cad\Gearbox\bracket.SLDPRT", assembly ) );
	}

	[Fact]
	public void MissingReference_IsNull()
	{
		Touch( "Top", "robot.SLDASM" );
		Assert.Null( new SwReferenceResolver( _vault ).Resolve( @"C:\cad\nowhere.SLDPRT", Path.Combine( _vault, "Top", "robot.SLDASM" ) ) );
		Assert.Null( new SwReferenceResolver( null ).Resolve( "" ) );
	}
}

/// <summary>The dependency tree built for a commit, from the SolidWorks 2026 KitBot samples (references saved on another machine).</summary>
[Collection( HackPDM.Tests.Characterization.SharedStateCollection.Name )]
public class KitBotDependencyTreeTests
{
	[KitBotFact]
	public async Task TopAssembly_ResolvesItsWholeTree_FromForeignPaths()
	{
		string folder = Path.Combine( SwSamples.Root!, SwSamples.KitBotFolder );
		// a settings provider too: without one the working folder silently stays unset
		using var pwa = new HackPDM.Tests.Characterization.PwaScope( folder );
		var top = new HackFile( Path.Combine( folder, "0_Kitbot2026withBumpers.SLDASM" ) );

		var (success, files, broken) = await top.GetHackFileWithDependencies();

		Assert.True( success, $"broken references: {string.Join( ", ", broken.Select( b => $"{b.Value?.Result} {b.Value?.Hack?.FullPath ?? "(no file)"}" ) )}" );
		Assert.All( files, f => Assert.True( File.Exists( f.FullPath ), f.FullPath ) );
		// every file the stored component tree lists, plus the assembly itself
		var store = HackPDM.Infrastructure.SldWrks.Format.SwStreamStore.Open( top.FullPath! );
		int treeFiles = System.Text.RegularExpressions.Regex.Matches( store.GetText( "swXmlContents/COMPINSTANCETREE" )!, "<swFile " ).Count;
		Assert.Equal( treeFiles, files.Select( f => f.FullPath ).Distinct().Count() );
	}
}
