#if !WINDOWS
using System;
using HackPDM.Core.Configuration;
using HackPDM.Core.Pdm;
using Uno.UI.Hosting;

namespace HackPDM;

public class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        try
        {
            var host = UnoPlatformHostBuilder.Create()
                .App(() => new HackApp())
                .UseX11()
                .UseLinuxFrameBuffer()
                .UseMacOS()
                .UseWin32()
                .Build();

            host.Run();
        }
        catch (Exception ex)
        {
            try
            {
                HackLogger.WriteCrashDump("Program.Main", ex, ClientUpdate.CurrentText);
                HackLogger.Critical("App", $"Unhandled exception in desktop host: {ex.Message}", ex);
            }
            catch { }

            try { AppComposition.LocalFiles.Dispose(); } catch { }
            Environment.Exit(1);
        }
    }
}
#endif
