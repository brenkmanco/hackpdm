using System.IO;
using HackPDM.Shared.GlobalData;

namespace HackPDM.Core.Hack;

/// <summary>A local file together with the result of checking that it can be used (exists, inside the working folder, readable).</summary>
/// <param name="IsBroken">Whether the file failed a check.</param>
/// <param name="IsTested">Whether a check has run. Note: <see cref="HackResult.NotTested"/> is 0, so <see cref="DoTest(HackTestDepth)"/> always leaves this false.</param>
/// <param name="TestDepth">Which checks were requested.</param>
/// <param name="Hack">The file checked.</param>
public record ResultHackFile(bool IsBroken, bool IsTested, HackTestDepth TestDepth, HackFile? Hack)
{
	public bool IsBroken { get; private set; } = IsBroken;
	public bool IsTested { get; private set; } = IsTested;
	public HackTestDepth TestDepth { get; private set; } = TestDepth;
	public HackResult Result { get; private set; } = HackResult.NotTested;

	/// <summary>Wraps a file and checks it right away.</summary>
	/// <param name="hack">The file to check.</param>
	/// <param name="test">Which checks to run; by default whether the file exists, which also checks that it is inside the working folder.</param>
	/// <exception cref="NullReferenceException"><paramref name="hack"/> is null and <paramref name="test"/> includes the existence check.</exception>
	public ResultHackFile(HackFile? hack, HackTestDepth test = HackTestDepth.FileExistsTest) : this(default, default, default, hack)
	{
		DoTest(test);
	}

	/// <summary>Marks the file broken because its references could not be read, so its dependency tree is unknown.</summary>
	/// <remarks>Sets <see cref="Result"/> to <see cref="HackResult.CorruptDepFile"/>.</remarks>
	public void MarkUnreadable()
	{
		Result = HackResult.CorruptDepFile;
		IsBroken = true;
		IsTested = true;
	}

	/// <summary>Runs the checks of the current <see cref="TestDepth"/> again.</summary>
	public void DoTest() => DoTest(TestDepth);
	/// <summary>Runs the requested checks and updates <see cref="Result"/> and <see cref="IsBroken"/>.</summary>
	/// <param name="test">
	/// The checks: <see cref="HackTestDepth.FileExistsTest"/> (and <see cref="HackTestDepth.FileIsCorruptTest"/>, which currently does
	/// the same) checks that the file is inside the working folder and exists; <see cref="HackTestDepth.InPWATest"/> checks only the folder.
	/// Becomes the new <see cref="TestDepth"/>.
	/// </param>
	/// <remarks>
	/// A file outside the working folder gets <see cref="HackResult.OutOfPWA"/> and a missing one <see cref="HackResult.MissingFile"/>;
	/// either makes it broken. Only <see cref="HackResult.Clean"/> (or no result) counts as not broken.
	/// </remarks>
	public void DoTest(HackTestDepth test)
	{
		if (test != TestDepth) TestDepth = test;

		if ( test.HasFlag(HackTestDepth.FileIsCorruptTest ) ) this.Result = TestFileIsCorrupt();
		if ( this.Result.HasFlag( HackResult.NotTested ) && test.HasFlag( HackTestDepth.FileExistsTest ) ) this.Result = TestFileExists();
		if ( this.Result.HasFlag( HackResult.NotTested ) && test.HasFlag( HackTestDepth.InPWATest ) ) this.Result = TestPWA();

		this.IsBroken = this.Result switch
		{ 
			HackResult.Clean => false,
			HackResult.NotTested => false,
			_ => true
		};
		this.IsTested = !this.Result.HasFlag( HackResult.NotTested );
	}

	private HackResult TestPWA()
	{
		var resultPWA = Result;
		if (string.IsNullOrEmpty(Hack?.FullPath))
		{
			resultPWA &= ~HackResult.Clean;
			resultPWA |= HackResult.MissingFile;
			return resultPWA;
		}
		if (!PwaPaths.IsInside(Hack.FullPath))
		{
			resultPWA &= ~HackResult.Clean;
			resultPWA |= HackResult.OutOfPWA;
		}
		return Result = resultPWA;
	}
	private HackResult TestFileExists()
	{
		var resultPWA = Result = TestPWA();
		if (Hack!.Exists is null)
		{
			Hack.Info ??= new FileInfo(Hack.FullPath ?? "");
		}

		if (Hack!.Exists is true)
		{
			resultPWA &= ~HackResult.NotTested;
			if (!resultPWA.HasFlag(HackResult.OutOfPWA))
				resultPWA |= HackResult.Clean;
		}
		else
		{
			resultPWA &= ~HackResult.Clean;
			resultPWA |= HackResult.MissingFile;
		}
		return Result = resultPWA;
	}
	private HackResult TestFileIsCorrupt() => TestFileExists();
}

