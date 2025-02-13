using static System.Net.Mime.MediaTypeNames;
using HackPDM_Server.CommandLine;

// Hack Server needs to be able to download new versions for entries 
// and save them to some folder. It also might need to update odoo based
// on if files are different.
// update intervals can be determined by program arguments or default


/// <summary>
/// Class with program entry point.
/// </summary>
internal sealed class Program
{
	/// <summary>
	/// Program entry point.
	/// </summary>
	private static void Main(string[] args)
	{
		ProgramOptions.Parse(args);
	}
}