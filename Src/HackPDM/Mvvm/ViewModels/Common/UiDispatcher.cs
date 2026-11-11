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
	/// (so a caller can finish first); exceptions are logged. False when nothing accepted it.
	/// </summary>
	bool Post( Action action );

	/// <summary>Runs <paramref name="action"/> on the UI thread; completes when it has run, with its exception.</summary>
	Task RunAsync( Action action, CancellationToken token = default );

	/// <summary>Runs an async delegate on the UI thread; completes when it has finished, with its exception.</summary>
	Task RunAsync( Func<Task> action );
}

/// <summary>Holds the app's <see cref="IUiDispatcher"/>. The shell sets it at startup; until then work runs inline.</summary>
public static class UiDispatcher
{
	public static IUiDispatcher Current { get; set; } = new InlineUiDispatcher();

	/// <inheritdoc cref="IUiDispatcher.Post"/>
	public static bool Post( Action action ) => Current.Post( action );
}

/// <summary>Runs everything on the calling thread (no UI yet, and tests).</summary>
public sealed class InlineUiDispatcher : IUiDispatcher
{
	public bool HasThreadAccess => true;

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
