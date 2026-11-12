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
	/// <summary>Writes a line to the operation's status log.</summary>
	/// <param name="status">The kind of line (info, error, warning, progress...), shown as its category.</param>
	/// <param name="message">The text of the line.</param>
	void Log( StatusMessage status, string message );

	/// <summary>Opens the status window.</summary>
	void ShowWindow();

	/// <summary>Starts an operation: renews the cancellation source and marks the window busy.</summary>
	/// <returns>The cancellation source for the operation; it is cancelled when the user cancels from the status window.</returns>
	/// <remarks>The source is only replaced when the previous one was already cancelled, so overlapping operations share it.</remarks>
	CancellationTokenSource BeginOperation();
	/// <summary>Cancels the running operation, if any.</summary>
	void Cancel();

	/// <summary>Resets the progress counters for <paramref name="total"/> items.</summary>
	/// <param name="total">The number of items the operation will process.</param>
	void SetTotal( int total );
	/// <summary>Adds to the processed/skipped counters. Safe to call from several threads.</summary>
	/// <param name="processed">How many more items were processed.</param>
	/// <param name="skipped">How many more items were skipped (already up to date, for example).</param>
	void Advance( int processed, int skipped = 0 );
	/// <summary>Sets the processed counter to an absolute value.</summary>
	/// <param name="processed">The total number of items processed so far.</param>
	void SetProcessed( int processed );
	/// <summary>Fills the progress bar and marks the window idle.</summary>
	void Complete();

	/// <summary>Shows a message to the user at the end of an operation.</summary>
	/// <param name="message">The message text.</param>
	/// <param name="caption">The title of the message box.</param>
	/// <remarks>The task completes when the user dismisses the message.</remarks>
	Task NotifyAsync( string message, string caption = "Status" );
}
