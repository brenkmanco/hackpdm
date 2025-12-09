using System;
using System.Diagnostics;
using System.Threading.Tasks;

using HackPDM.Forms.Hack;

using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;

namespace HackPDM.Src.Helper.Xaml
{
	internal static class SafeHelper
	{
		internal static void SafeInvokeGen<T>(T data, Action<T> action)
		{
			HackFileManager.HackDispatcherQueue.TryEnqueue(() => action.Invoke(data));
		}

		internal static void SafeInvoker(Action action)
			=> SafeInvokerInternal(action, DispatcherQueue.GetForCurrentThread());
		
		
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
				Debug.Fail(ex.Message, ex.StackTrace);
				return false;
			}
		}
		internal static Task SafeInvokerAsync(Action action)
		{
			var tcs = new TaskCompletionSource<bool>();
			HackFileManager.
					HackDispatcherQueue.TryEnqueue(() =>
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