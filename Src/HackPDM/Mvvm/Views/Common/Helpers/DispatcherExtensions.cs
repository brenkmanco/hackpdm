using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;

namespace HackPDM.Mvvm.Views.Common.Helpers;

/// <summary>
/// Extensions for safe UI thread dispatching with DispatcherQueue.
/// </summary>
public static class DispatcherExtensions
{
    public static async Task ExecuteUIAsync(this DispatcherQueue dispatcher, Func<Task> function)
    {
        if (dispatcher.HasThreadAccess)
        {
            await function();
        }
        else
        {
            var tcs = new TaskCompletionSource();
            dispatcher.TryEnqueue(async () =>
            {
                try
                {
                    await function();
                    tcs.SetResult();
                }
                catch (Exception e)
                {
                    Debug.Fail(e.Message, e.StackTrace);
                    tcs.SetException(e);
                }
            });
            await tcs.Task;
        }
    }

    public static async Task ExecuteUIAsync(this DispatcherQueue dispatcher, Action function)
    {
        if (dispatcher.HasThreadAccess)
        {
            function();
        }
        else
        {
            var tcs = new TaskCompletionSource();
            dispatcher.TryEnqueue(() =>
            {
                try
                {
                    function();
                    tcs.SetResult();
                }
                catch (Exception e)
                {
                    Debug.Fail(e.Message, e.StackTrace);
                    tcs.SetException(e);
                }
            });
            await tcs.Task;
        }
    }


}
