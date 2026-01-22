using System;
using System.Diagnostics;
using System.Threading.Tasks;
using HackPDM.UI.Forms.Hack;
using Microsoft.UI.Dispatching;

namespace HackPDM.Core.Helper.Xaml
{
	internal static class SafeHelper
	{
		internal static void SafeInvokeGen<T>(T data, Action<T> action)
		{
			HackFileManager.HackDispatcherQueue.TryEnqueue(() => action.Invoke(data));
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
	}
}