using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using HackPDM.Forms.Hack;

using Newtonsoft.Json.Linq;

using StatDialog = HackPDM.Forms.Settings.StatusDialog;

namespace HackPDM.ClientUtils;

internal class AsyncHelper
{
    private readonly Func<bool> _predicate;
    private readonly double _pollDelayMs;
    private readonly double _timeoutMs;
    private readonly int _pollDelayMsInt;
    private readonly int _timeoutMsInt;
    private int _totalPollMs = 0;
    private bool _signal = false;
    private bool _timedOut = false;
    private bool _wasCancelled = false;
    private CancellationToken _token;
    private AsyncHelper(Func<bool> eval, int millisecondTimeout = -1, int pollingMilliseconds = 1000, CancellationToken token = default)
    {
        if (eval is null) throw new ArgumentException("predicate must not be null");
        _predicate = eval;
        _timeoutMsInt = millisecondTimeout;
        _pollDelayMsInt = pollingMilliseconds;
        _pollDelayMs = Convert.ToDouble(pollingMilliseconds);
        _timeoutMs = Convert.ToDouble(millisecondTimeout);
    }
    // WaitUntil: wantTrue = true 
    // WaitWhile: wantTrue = false
    private async Task<AsyncHelper> InitializeAsync(bool wantTrue = true)
        => await InitializeAsync(null, wantTrue);
    private async Task<AsyncHelper> InitializeAsync(Action action, bool wantTrue = true)
    {
        bool indefinite = _timeoutMs <= 0;
        var timeout = DateTime.UtcNow;

        if (!indefinite)
        {
            timeout = timeout + TimeSpan.FromMilliseconds(_timeoutMs);
        }
        while (!_signal)
        {
            TimeSpan leftOver = timeout - DateTime.UtcNow;
            int min = indefinite ? _pollDelayMsInt : Convert.ToInt32(Help.MinDownTo(0, leftOver.TotalMilliseconds, _pollDelayMs));
            bool willEval = indefinite || min == _pollDelayMsInt;

            try { await Task.Delay(min, _token); }
            catch (TaskCanceledException e)
            {
                _wasCancelled = true;
                Debug.WriteLine(e, "CANCEL");
                break;
            }

            if (!willEval)
            {
                _timedOut = true;
                break;
            }
            if (!wantTrue ^ _predicate()) _signal = true;
            else
            {
                //_totalPollMs += min;
                if (action is not null) action();
            }
        }
        return this;
    }
    private async IAsyncEnumerable<(AsyncHelper, T)> InitializeAsync<T>(Func<T> func, bool wantTrue = true)
    {
        bool indefinite = _timeoutMs <= 0;
        var timeout = DateTime.UtcNow;

        if (!indefinite)
        {
            timeout = timeout + TimeSpan.FromMilliseconds(_timeoutMs);
        }
        while (!_signal)
        {
            yield return (null, await IterationInternal(timeout, func, indefinite, wantTrue));
        }
        yield return ((this, default));
    }

    private async Task<T> IterationInternal<T>(DateTime timeout, Func<T> func, bool indefinite = false, bool wantTrue = true)
    {
        TimeSpan leftOver = timeout - DateTime.UtcNow;
        int min = indefinite ? _pollDelayMsInt : Convert.ToInt32(Help.MinDownTo(0, leftOver.TotalMilliseconds, _pollDelayMs));
        bool willEval = indefinite || min == _pollDelayMsInt;

        try 
        { 
            await Task.Delay(min, _token); 
        }
        catch (TaskCanceledException e)
        {
            _wasCancelled = true;
            Debug.WriteLine(e, "CANCEL");
            return default;
        }

        if (!willEval)
        {
            _timedOut = true;
            return default;
        }
        if (!wantTrue ^ _predicate())
        {
            _signal = true;
        }
        if (func is not null)
        {
            return func();
        }
        return default;
        //else
        //{
        //    //_totalPollMs += min;
        //    if (action is not null) action();
        //}
    }
    /// <summary>Wait until the predicate condition evaluates to true before continuing.</summary>
    /// <param name="eval">function to evaluate returning true or false</param>
    /// <param name="msTimeout">time before exiting function. (t <= 0) means that it continues indefinitely</param>
    /// <param name="pollingMs">millisecond intervals before checking if the condition is fulfilled</param>
    /// <param name="token">async cancellation token</param>
    public static async Task<bool> WaitUntil(Func<bool> eval, int pollingMs = 1000, int msTimeout = -1, CancellationToken token = default)
        => await WaitInternal(eval, true, msTimeout, pollingMs, token);
    public static async Task<bool> WaitWhile(Func<bool> eval, int pollingMs = 1000, int msTimeout = -1, CancellationToken token = default)
        => await WaitInternal(eval, false, msTimeout, pollingMs, token);
    public static async Task<bool> WaitForMilliseconds(int milliseconds, CancellationToken token = default)
        => await WaitTimeInternal(milliseconds, token);

    public static async Task<bool> DoWhileWaitUntil(Func<bool> eval, Action action, int pollingMs = 1000, int msTimeout = -1, CancellationToken token = default)
        => await DoWhileWaitInternal(eval, action, true, pollingMs, msTimeout, token);
    public static async Task<bool> DoWhileWaitWhile(Func<bool> eval, Action action, int pollingMs = 1000, int msTimeout = -1, CancellationToken token = default)
        => await DoWhileWaitInternal(eval, action, false, pollingMs, msTimeout, token);
    public static async Task<bool> AsyncRunner(Func<Task> function, string statusHeader = "Status", CancellationTokenSource tokenSource = default)
    {
        tokenSource ??= new();
        var task = Task.Run(() => function(), tokenSource.Token);
        bool blnWorkCanceled = await AsyncHelper.WaitUntil(() => HackFileManager.Dialog?.Canceled is true || task.IsCompleted || task.IsCanceled, 500);

        if (blnWorkCanceled)
        {
            if (HackFileManager.Dialog?.Canceled is true || task.IsCanceled)
            {
                tokenSource.Cancel();
                try
                {
                    await task; // Await to observe cancellation
                }
                catch (OperationCanceledException)
                {
                    // handles cancellation feedback
                }
                return false;
            }
        }
        await task;
        return true;
    }

    private static async Task<bool> DoWhileWaitInternal(Func<bool> eval, Action action, bool wantTrue = true, int pollingMsEval = 1000, int msTimeout = -1, CancellationToken token = default)
    {
        var wait = new AsyncHelper(eval, msTimeout, pollingMsEval, token);
        var iterableReturn = wait.InitializeAsync(() => action, wantTrue);
        await foreach (var item in iterableReturn)
        {
            if (item.Item1 is not null)
            {
                return !item.Item1._timedOut && !item.Item1._wasCancelled && item.Item1._signal;
            }
        }
        return false;
    }
    private static async Task<bool> WaitInternal(Func<bool> eval, bool wantTrue = true, int msTimeout = -1, int pollingMs = 1000, CancellationToken token = default)
    {
        var wait = new AsyncHelper(eval, msTimeout, pollingMs, token);
        AsyncHelper helper = await wait.InitializeAsync(wantTrue);
        return !helper._wasCancelled && !helper._timedOut && helper._signal;
    }
    private static async Task<bool> WaitTimeInternal(int milliseconds = 1000, CancellationToken token = default)
    {
        var wait = new AsyncHelper(()=>false, milliseconds, milliseconds + 1000, token);
        AsyncHelper helper = await wait.InitializeAsync(true);
        return !helper._wasCancelled && helper._timedOut;
    }
}