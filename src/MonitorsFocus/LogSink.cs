using System;
using System.Collections.Concurrent;

namespace MonitorsFocus;

internal static class LogSink
{
    private static readonly ConcurrentQueue<string> _buffer = new();
    public static event Action<string>? LogAppended;

    public static void Info(string message)
    {
        var line = $"[{DateTime.Now:HH:mm:ss}] {message}";
        _buffer.Enqueue(line);
        Trim();
        LogAppended?.Invoke(line);
    }

    public static string[] Snapshot()
    {
        return _buffer.ToArray();
    }

    private static void Trim(int max = 500)
    {
        while (_buffer.Count > max && _buffer.TryDequeue(out _))
        {
        }
    }
}
