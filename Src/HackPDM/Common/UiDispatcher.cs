using System;
using System.Threading;
using System.Threading.Tasks;

using HackPDM.Core.Configuration;

using Microsoft.UI.Dispatching;

namespace HackPDM.Common;

public interface IUiDispatcher
{
	bool HasThreadAccess { get; }
	bool Post( Action action );
	Task RunAsync( Action action, CancellationToken token = default );
	Task RunAsync( Func<Task> action );
}

public static class UiDispatcher
{
	public static IUiDispatcher Current { get; set; } = new InlineUiDispatcher();
	public static bool Post( Action action ) => Current.Post( action );
}

public sealed class InlineUiDispatcher : IUiDispatcher
{
	public bool HasThreadAccess => true;

	public bool Post( Action action )
	{
		try { action(); }
		catch( Exception e ) { HackLogger.Error( "UiDispatcher", $"UI work item failed: {e.Message}", e ); }
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
		try { return action(); }
		catch( Exception e ) { return Task.FromException( e ); }
	}
}

public sealed class DispatcherQueueUiDispatcher( DispatcherQueue queue ) : IUiDispatcher
{
	public bool HasThreadAccess => queue.HasThreadAccess;

	public bool Post( Action action )
	{
		if( queue.TryEnqueue( () => Run( action ) ) )
			return true;
		HackLogger.Warn( "UiDispatcher", "UI dispatcher rejected the work item; it was not run" );
		return false;
	}

	public Task RunAsync( Action action, CancellationToken token = default )
	{
		if( token.IsCancellationRequested )
			return Task.FromCanceled( token );
		if( queue.HasThreadAccess )
			return new InlineUiDispatcher().RunAsync( action, token );

		TaskCompletionSource tcs = new( TaskCreationOptions.RunContinuationsAsynchronously );
		if( !queue.TryEnqueue( () =>
		{
			if( token.IsCancellationRequested )
			{
				tcs.TrySetCanceled( token );
				return;
			}
			try
			{
				action();
				tcs.TrySetResult();
			}
			catch( Exception e )
			{
				tcs.TrySetException( e );
			}
		} ) )
		{
			tcs.TrySetException( new InvalidOperationException( "UI dispatcher rejected the work item" ) );
		}
		return tcs.Task;
	}

	public Task RunAsync( Func<Task> action )
	{
		if( queue.HasThreadAccess )
			return new InlineUiDispatcher().RunAsync( action );

		TaskCompletionSource tcs = new( TaskCreationOptions.RunContinuationsAsynchronously );
		if( !queue.TryEnqueue( async () =>
		{
			try
			{
				await action();
				tcs.TrySetResult();
			}
			catch( Exception e )
			{
				tcs.TrySetException( e );
			}
		} ) )
		{
			tcs.TrySetException( new InvalidOperationException( "UI dispatcher rejected the work item" ) );
		}
		return tcs.Task;
	}

	private static void Run( Action action )
	{
		try { action(); }
		catch( Exception e ) { HackLogger.Error( "UiDispatcher", $"UI work item failed: {e.Message}", e ); }
	}
}
