using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using HackPDM.Core.General;

namespace HackPDM.Core;

/// <summary>Polling helpers for waiting on a condition or a background task.</summary>
public class AsyncHelper
{
    private readonly Func<bool> _predicate;
    private readonly double _pollDelayMs;
    private readonly double _timeoutMs;
    private readonly int _pollDelayMsInt;
    private readonly int _timeoutMsInt;
    private bool _signal = false;
    private bool _timedOut = false;
    private bool _wasCancelled = false;
    private readonly CancellationToken _token;
    private AsyncHelper(Func<bool> eval, int millisecondTimeout = -1, int pollingMilliseconds = 1000, CancellationToken token = default)
    {
        if (eval is null) throw new ArgumentException("predicate must not be null");
        _predicate = eval;
        _timeoutMsInt = millisecondTimeout;
        _pollDelayMsInt = pollingMilliseconds;
        _pollDelayMs = Convert.ToDouble(pollingMilliseconds);
        _timeoutMs = Convert.ToDouble(millisecondTimeout);
        _token = token;
    }
    // WaitUntil: wantTrue = true 
    // WaitWhile: wantTrue = false
    private async Task<AsyncHelper> InitializeAsync(bool wantTrue = true)
        => await InitializeAsync(null, wantTrue);
    private async Task<AsyncHelper> InitializeAsync(Action? action, bool wantTrue = true)
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
            int min = indefinite ? _pollDelayMsInt : Convert.ToInt32(MathUtility.MinDownTo(0, leftOver.TotalMilliseconds, _pollDelayMs));
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
            else action?.Invoke();
        }
        return this;
    }
    /// <summary>Polls a condition until it becomes true, the timeout runs out, or the wait is cancelled.</summary>
    /// <param name="eval">The condition to check; it is first checked after one polling interval, not immediately.</param>
    /// <param name="pollingMs">Milliseconds between checks.</param>
    /// <param name="msTimeout">Milliseconds before giving up; 0 or less waits indefinitely.</param>
    /// <param name="token">Cancels the wait; a cancelled wait returns <see langword="false"/>.</param>
    /// <returns><see langword="true"/> when the condition became true; <see langword="false"/> on timeout or cancellation.</returns>
    /// <exception cref="System.ArgumentException"><paramref name="eval"/> is <see langword="null"/>.</exception>
    public static async Task<bool> WaitUntil(Func<bool> eval, int pollingMs = 1000, int msTimeout = -1, CancellationToken token = default)
        => await WaitInternal(eval, true, msTimeout, pollingMs, token);
    /// <summary>Runs an asynchronous function on the thread pool and waits for it, polling every 500 ms.</summary>
    /// <param name="function">The work to run.</param>
    /// <param name="statusHeader">Currently unused.</param>
    /// <param name="tokenSource">Its token is passed to <see cref="System.Threading.Tasks.Task.Run(System.Func{System.Threading.Tasks.Task}, System.Threading.CancellationToken)"/>; a new source is created when <see langword="null"/>.</param>
    /// <returns><see langword="false"/> when the task ended cancelled; otherwise <see langword="true"/> once it has completed.</returns>
    /// <remarks>Exceptions thrown by <paramref name="function"/> are rethrown when the task is awaited.</remarks>
    public static async Task<bool> AsyncRunner(Func<Task> function, string statusHeader = "Status", CancellationTokenSource? tokenSource = null)
    {
        tokenSource ??= new();
        var task = Task.Run(() => function(), tokenSource.Token);
        bool blnWorkCanceled = await AsyncHelper.WaitUntil(() => task.IsCompleted || task.IsCanceled, 500);

        if (blnWorkCanceled)
        {
            if (task.IsCanceled)
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
    private static async Task<bool> WaitInternal(Func<bool> eval, bool wantTrue = true, int msTimeout = -1, int pollingMs = 1000, CancellationToken token = default)
    {
        var wait = new AsyncHelper(eval, msTimeout, pollingMs, token);
        AsyncHelper helper = await wait.InitializeAsync(wantTrue);
        return !helper._wasCancelled && !helper._timedOut && helper._signal;
    }
}