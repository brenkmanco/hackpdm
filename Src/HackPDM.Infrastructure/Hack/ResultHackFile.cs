using System.IO;
using HackPDM.Shared.GlobalData;

namespace HackPDM.Core.Hack;

public record ResultHackFile(bool IsBroken, bool IsTested, HackTestDepth TestDepth, HackFile? Hack)
{
	public bool IsBroken { get; private set; } = IsBroken;
	public bool IsTested { get; private set; } = IsTested;
	public HackTestDepth TestDepth { get; private set; } = TestDepth;
	public HackResult Result { get; private set; } = HackResult.NotTested;

	public ResultHackFile(HackFile? hack, HackTestDepth test = HackTestDepth.FileExistsTest) : this(default, default, default, hack)
	{
		DoTest(test);
	}

	public void DoTest() => DoTest(TestDepth);
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
		if (!FileOperations.InPWAFolder(Hack.FullPath))
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

