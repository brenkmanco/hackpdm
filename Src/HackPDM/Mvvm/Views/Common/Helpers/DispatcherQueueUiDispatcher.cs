using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

using HackPDM.Mvvm.ViewModels.Common;

using Microsoft.UI.Dispatching;

namespace HackPDM.Mvvm.Views.Common.Helpers;

/// <summary><see cref="IUiDispatcher"/> on the main window's <see cref="DispatcherQueue"/>.</summary>
public sealed class DispatcherQueueUiDispatcher( DispatcherQueue queue ) : IUiDispatcher
{
	public bool HasThreadAccess => queue.HasThreadAccess;

	public bool Post( Action action )
	{
		if( queue.TryEnqueue( () => Run( action ) ) )
			return true;
		Debug.WriteLine( "UI dispatcher rejected the work item; it was not run" );
		return false;
	}

	public Task RunAsync( Action action, CancellationToken token = default )
	{
		if( token.IsCancellationRequested )
			return Task.FromCanceled( token );
		if( queue.HasThreadAccess )
			return new InlineUiDispatcher().RunAsync( action, token );

		var tcs = new TaskCompletionSource( TaskCreationOptions.RunContinuationsAsynchronously );
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
			// a rejected work item would otherwise leave the task pending forever
			tcs.TrySetException( new InvalidOperationException( "UI dispatcher rejected the work item" ) );
		return tcs.Task;
	}

	public Task RunAsync( Func<Task> action )
	{
		if( queue.HasThreadAccess )
			return new InlineUiDispatcher().RunAsync( action );

		var tcs = new TaskCompletionSource( TaskCreationOptions.RunContinuationsAsynchronously );
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
			tcs.TrySetException( new InvalidOperationException( "UI dispatcher rejected the work item" ) );
		return tcs.Task;
	}

	private static void Run( Action action )
	{
		try
		{
			action();
		}
		catch( Exception e )
		{
			Debug.WriteLine( $"UI work item failed: {e}" );
		}
	}
}
