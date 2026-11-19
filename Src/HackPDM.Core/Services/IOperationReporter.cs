using System.Diagnostics;

using HackPDM.Core.Configuration;
using HackPDM.Core.Pdm;

namespace HackPDM.Core.Services;

/// <summary>Receives log lines, progress, and final notifications from long-running PDM workflows.</summary>
public interface IOperationReporter
{
	void Log( StatusMessage status, string message );
	void ShowWindow();
	CancellationTokenSource BeginOperation();
	void Cancel();
	void SetTotal( int total );
	void Advance( int processed, int skipped = 0 );
	void SetProcessed( int processed );
	void AddDownloadedBytes( long bytes );
	void AddUploadedBytes( long bytes );
	void Complete();
	Task NotifyAsync( string message, string caption = "Status" );
}

/// <summary>Opens files, folders, and URLs with the desktop's default application.</summary>
public static class ShellLauncher
{
	public static bool OpenFile( string path ) => File.Exists( path ) && Launch( path );
	public static bool OpenFolder( string path ) => Directory.Exists( path ) && Launch( path );
	public static bool OpenUrl( string url ) => Launch( url );

	private static bool Launch( string target )
	{
		try
		{
			if( OperatingSystem.IsLinux() )
				Process.Start( "xdg-open", target );
			else if( OperatingSystem.IsMacOS() )
				Process.Start( "open", target );
			else
				Process.Start( new ProcessStartInfo( target ) { UseShellExecute = true } );
			return true;
		}
		catch( Exception e )
		{
			HackLogger.Warn( "ShellLauncher", $"Could not open '{target}': {e.Message}", e );
			return false;
		}
	}
}

/// <summary>Async background runner and condition-polling helpers.</summary>
public static class AsyncHelper
{
	public static Task<bool> WaitUntil( Func<bool> eval, int pollingMs = 1000, int msTimeout = -1, CancellationToken token = default )
		=> WaitInternal( eval, wantTrue: true, msTimeout, pollingMs, token );

	public static Task<bool> WaitWhile( Func<bool> eval, int pollingMs = 1000, int msTimeout = -1, CancellationToken token = default )
		=> WaitInternal( eval, wantTrue: false, msTimeout, pollingMs, token );

	public static async Task<bool> AsyncRunner( Func<Task> function, string statusHeader = "Status", CancellationTokenSource? tokenSource = null )
	{
		CancellationToken token = tokenSource?.Token ?? CancellationToken.None;
		try
		{
			await Task.Run( function, token );
			return true;
		}
		catch( OperationCanceledException ) when( token.IsCancellationRequested )
		{
			return false;
		}
	}

	private static async Task<bool> WaitInternal( Func<bool> eval, bool wantTrue, int msTimeout, int pollingMs, CancellationToken token )
	{
		ArgumentNullException.ThrowIfNull( eval );
		int stepMs = Math.Max( 1, pollingMs );
		bool indefinite = msTimeout <= 0;
		long deadline = indefinite ? 0 : Environment.TickCount64 + msTimeout;

		while( true )
		{
			int delay = stepMs;
			if( !indefinite )
			{
				long remaining = deadline - Environment.TickCount64;
				if( remaining <= 0 )
					return false;
				delay = (int)Math.Min( stepMs, remaining );
			}

			try
			{
				await Task.Delay( delay, token );
			}
			catch( OperationCanceledException )
			{
				return false;
			}

			if( eval() == wantTrue )
				return true;
		}
	}
}
