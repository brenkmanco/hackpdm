using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
using HackPDM.Mvvm.Views.Hack;

namespace HackPDM.Mvvm.Views.Common.Controls
{
	internal static class SafeHelper
	{
		internal static void SafeInvokeGen<T>(T data, Action<T> action)
		{
			HackFileManagerView.HackDispatcherQueue.TryEnqueue(() => action.Invoke(data));
		}
		internal static void SafeInvoker(Action action)
			=> SafeInvokerInternal(action, DispatcherQueue.GetForCurrentThread());
		internal static Task<T> SafeInvoker<T>(Func<T> func)
		{
			var tcs = new TaskCompletionSource<T>();
			SafeInvokerInternal(() =>
			{
				try
				{
					T result = func();
					tcs.SetResult(result);
				}
				catch (Exception ex)
				{
					tcs.SetException(ex);
				}
			}, DispatcherQueue.GetForCurrentThread());
			return tcs.Task;
		}
		private static void SafeInvokerInternal(Action action, DispatcherQueue dispatcher)
		{
			_ = dispatcher is not null and { HasThreadAccess: true}
				? TryDoAction(action)
				: dispatcher?.TryEnqueue(()=>TryDoAction(action));
		}
		private static bool TryDoAction(Action action)
		{
			try
			{
				action();
				return true;
			}
			catch (Exception ex)
			{
				Debug.WriteLine(ex.Message, ex.StackTrace);
				return false;
			}
		}
		//internal async static Task<TResult> SafeInvokerAsync<TResult>(Func<TResult> func)
		//{
		//	var tcs = new TaskCompletionSource<TResult>();
		//	HackApp.RootFrame?.DispatcherQueue.TryEnqueue(() =>
		//	{
		//		try
		//		{
		//			tcs.SetResult(func());
		//		}
		//		catch (Exception ex)
		//		{
		//			tcs.SetException(ex);
		//		}
		//	});
		//	return await tcs.Task;
		//}
		/// <summary>
		/// Runs an async delegate on the UI thread and completes when it has finished,
		/// propagating its exception. Without this overload async lambdas bind to
		/// <see cref="SafeInvokerAsync(Action)"/> as async void: the returned task completes at the
		/// lambda's first await and any later exception is lost.
		/// </summary>
		internal static Task SafeInvokerAsync(Func<Task> func)
		{
			var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
			if (!HackApp.DispatcherQueue.TryEnqueue(async () =>
			{
				try
				{
					await func();
					tcs.SetResult(true);
				}
				catch (Exception ex)
				{
					tcs.SetException(ex);
				}
			}))
			{
				tcs.SetException(new InvalidOperationException("UI dispatcher queue rejected the work item"));
			}
			return tcs.Task;
		}
		internal static Task SafeInvokerAsync(Action action)
		{
			var tcs = new TaskCompletionSource<bool>();
			HackApp.DispatcherQueue.TryEnqueue(() =>
			{
				try
				{
					action();
					tcs.SetResult(true);
				}
				catch (Exception ex)
				{
					tcs.SetException(ex);
				}
			});
			return tcs.Task;
		}
		internal static Task SafeInvokerAsync(Action action, CancellationToken token)
		{
			var tcs = new TaskCompletionSource<bool>();
			HackApp.DispatcherQueue.TryEnqueue(() =>
			{
				try
				{
					action();
					tcs.SetResult(true);
				}
				catch (Exception ex)
				{
					tcs.SetException(ex);
				}
			});
			return tcs.Task;
		}
	}
}