using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using HackPDM.Shared.GlobalData;

namespace HackPDM.Tests.Mocks;

/// <summary>
/// A decoupled, thread-safe mock logger that records <see cref="StatusMessage"/> notifications
/// emitted by PDM infrastructure operations (delete, checkout, commit, download).
/// </summary>
public class MockStatusLogger
{
    private readonly ConcurrentQueue<(StatusMessage Status, string Message)> _records = new();

    public IReadOnlyList<(StatusMessage Status, string Message)> Records => _records.ToList();

    public int Count => _records.Count;

    public void Log(StatusMessage status, string message)
    {
        _records.Enqueue((status, message));
    }

    public bool HasStatus(StatusMessage status)
    {
        return _records.Any(r => r.Status == status);
    }

    public bool HasMessageContaining(string text, StringComparison comparison = StringComparison.OrdinalIgnoreCase)
    {
        return _records.Any(r => r.Message.Contains(text, comparison));
    }

    public IEnumerable<string> GetMessagesForStatus(StatusMessage status)
    {
        return _records.Where(r => r.Status == status).Select(r => r.Message);
    }

    public void Clear()
    {
        _records.Clear();
    }

    /// <summary>
    /// Implicit conversion to the logging callback delegate used by PDM operations.
    /// </summary>
    public static implicit operator Action<StatusMessage, string>(MockStatusLogger logger)
    {
        return logger.Log;
    }
}
