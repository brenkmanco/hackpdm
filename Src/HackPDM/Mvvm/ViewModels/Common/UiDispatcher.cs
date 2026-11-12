using System;
using System.Threading;
using System.Threading.Tasks;

namespace HackPDM.Mvvm.ViewModels.Common;

/// <summary>
/// Runs work on the UI thread without the caller knowing about the UI framework. The one way the
/// app marshals to the UI thread (it replaced UiDispatcher.Post's delegate, SafeHelper,
/// DispatcherExtensions and the scattered DispatcherQueue.TryEnqueue calls).
/// </summary>
public interface IUiDispatcher
{
	/// <summary>Whether the caller is on the UI thread.</summary>
	bool HasThreadAccess { get; }

	/// <summary>
	/// Queues <paramref name="action"/> to run later on the UI thread, even when called from it
	/// (so a caller can finish first); exceptions are logged.
	/// </summary>
	/// <param name="action">The work to run.</param>
	/// <returns><see langword="false"/> when the dispatcher rejected the work item (it will not run).</returns>
	/// <remarks>The <see cref="InlineUiDispatcher"/> runs <paramref name="action"/> immediately instead.</remarks>
	bool Post( Action action );

	/// <summary>Runs <paramref name="action"/> on the UI thread; completes when it has run, with its exception.</summary>
	/// <param name="action">The work to run.</param>
	/// <param name="token">Cancels the work if it has not started yet.</param>
	/// <returns>A task that completes after <paramref name="action"/> ran (inline when already on the UI thread),
	/// faults with its exception, or is cancelled.</returns>
	/// <remarks>When the dispatcher rejects the work item the task faults with <see cref="InvalidOperationException"/>.</remarks>
	Task RunAsync( Action action, CancellationToken token = default );

	/// <summary>Runs an async delegate on the UI thread; completes when it has finished, with its exception.</summary>
	/// <param name="action">The asynchronous work to start on the UI thread.</param>
	/// <returns>A task that completes when the task returned by <paramref name="action"/> completes, or faults with its exception.</returns>
	/// <remarks>When the dispatcher rejects the work item the task faults with <see cref="InvalidOperationException"/>.</remarks>
	Task RunAsync( Func<Task> action );
}

/// <summary>Holds the app's <see cref="IUiDispatcher"/>. The shell sets it at startup; until then work runs inline.</summary>
public static class UiDispatcher
{
	/// <summary>The dispatcher used by the app; an <see cref="InlineUiDispatcher"/> until the shell replaces it.</summary>
	public static IUiDispatcher Current { get; set; } = new InlineUiDispatcher();

	/// <inheritdoc cref="IUiDispatcher.Post"/>
	public static bool Post( Action action ) => Current.Post( action );
}

/// <summary>Runs everything on the calling thread (no UI yet, and tests).</summary>
public sealed class InlineUiDispatcher : IUiDispatcher
{
	public bool HasThreadAccess => true;

	/// <inheritdoc/>
	public bool Post( Action action )
	{
		try
		{
			action();
		}
		catch( Exception e )
		{
			System.Diagnostics.Debug.WriteLine( $"UI work item failed: {e}" );
		}
		return true;
	}

	/// <inheritdoc/>
	public Task RunAsync( Action action, CancellationToken token = default )
	{
		if( token.IsCancellationRequested )
			return Task.FromCanceled( token );
		try
		{
			action();
			return Task.CompletedTask;
		}
		catch( Exception e )
		{
			return Task.FromException( e );
		}
	}

	/// <inheritdoc/>
	public Task RunAsync( Func<Task> action )
	{
		try
		{
			return action();
		}
		catch( Exception e )
		{
			return Task.FromException( e );
		}
	}
}
