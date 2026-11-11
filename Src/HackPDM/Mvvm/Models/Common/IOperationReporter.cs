using System.Threading;
using System.Threading.Tasks;

using HackPDM.Shared.GlobalData;

namespace HackPDM.Mvvm.Models.Common;

/// <summary>
/// Where long-running PDM operations send their log lines, progress and final message.
/// Lets workflow code report progress without knowing about the status window or view models.
/// </summary>
public interface IOperationReporter
{
	void Log( StatusMessage status, string message );

	/// <summary>Opens the status window.</summary>
	void ShowWindow();

	/// <summary>Starts an operation: renews the cancellation source and marks the window busy.</summary>
	CancellationTokenSource BeginOperation();
	/// <summary>Cancels the running operation, if any.</summary>
	void Cancel();

	/// <summary>Resets the progress counters for <paramref name="total"/> items.</summary>
	void SetTotal( int total );
	/// <summary>Adds to the processed/skipped counters. Safe to call from several threads.</summary>
	void Advance( int processed, int skipped = 0 );
	/// <summary>Sets the processed counter to an absolute value.</summary>
	void SetProcessed( int processed );
	/// <summary>Fills the progress bar and marks the window idle.</summary>
	void Complete();

	/// <summary>Shows a message to the user at the end of an operation.</summary>
	Task NotifyAsync( string message, string caption = "Status" );
}
