using System;
using System.Collections.Generic;

namespace NovaForge.Core.Foundation
{
    public enum LogLevel
    {
        Debug = 0,
        Info = 1,
        Warning = 2,
        Error = 3,
    }

    public readonly struct LogEntry
    {
        public readonly DateTime UtcTime;
        public readonly LogLevel Level;
        public readonly string Tag;
        public readonly string Message;
        public readonly Exception Exception;

        public LogEntry(DateTime utcTime, LogLevel level, string tag, string message, Exception exception)
        {
            UtcTime = utcTime;
            Level = level;
            Tag = tag;
            Message = message;
            Exception = exception;
        }

        public override string ToString()
        {
            var text = $"{UtcTime:O} [{Level}] [{Tag}] {Message}";
            return Exception == null ? text : text + Environment.NewLine + Exception;
        }
    }

    /// <summary>Destination for log entries (Unity console, file, crash reporter breadcrumbs...).</summary>
    public interface ILogSink
    {
        void Write(in LogEntry entry);
    }

    /// <summary>Logging facade consumed by all modules.</summary>
    public interface ILog
    {
        void Write(LogLevel level, string tag, string message, Exception exception = null);
        bool IsEnabled(LogLevel level);
    }

    public static class LogExtensions
    {
        public static void Debug(this ILog log, string tag, string message) => log.Write(LogLevel.Debug, tag, message);
        public static void Info(this ILog log, string tag, string message) => log.Write(LogLevel.Info, tag, message);
        public static void Warning(this ILog log, string tag, string message, Exception ex = null) => log.Write(LogLevel.Warning, tag, message, ex);
        public static void Error(this ILog log, string tag, string message, Exception ex = null) => log.Write(LogLevel.Error, tag, message, ex);
    }

    public sealed class LoggingService : ILog
    {
        private readonly List<ILogSink> _sinks = new List<ILogSink>();
        private readonly IClock _clock;

        public LogLevel MinimumLevel { get; set; }

        public LoggingService(IClock clock, LogLevel minimumLevel = LogLevel.Info)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            MinimumLevel = minimumLevel;
        }

        public void AddSink(ILogSink sink)
        {
            if (sink == null) throw new ArgumentNullException(nameof(sink));
            _sinks.Add(sink);
        }

        public bool IsEnabled(LogLevel level) => level >= MinimumLevel;

        public void Write(LogLevel level, string tag, string message, Exception exception = null)
        {
            if (!IsEnabled(level)) return;
            var entry = new LogEntry(_clock.UtcNow, level, tag ?? "-", message ?? string.Empty, exception);
            for (int i = 0; i < _sinks.Count; i++)
            {
                try
                {
                    _sinks[i].Write(entry);
                }
                catch (Exception)
                {
                    // A failing sink must never break gameplay or recurse into logging; other sinks still receive the entry.
                }
            }
        }
    }

    /// <summary>Keeps the last N entries in memory; attached to error/crash reports as breadcrumbs.</summary>
    public sealed class RingBufferLogSink : ILogSink
    {
        private readonly LogEntry[] _buffer;
        private int _next;
        private int _count;

        public RingBufferLogSink(int capacity = 200)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            _buffer = new LogEntry[capacity];
        }

        public void Write(in LogEntry entry)
        {
            _buffer[_next] = entry;
            _next = (_next + 1) % _buffer.Length;
            if (_count < _buffer.Length) _count++;
        }

        /// <summary>Oldest first.</summary>
        public IReadOnlyList<LogEntry> Snapshot()
        {
            var result = new List<LogEntry>(_count);
            int start = (_next - _count + _buffer.Length) % _buffer.Length;
            for (int i = 0; i < _count; i++)
                result.Add(_buffer[(start + i) % _buffer.Length]);
            return result;
        }
    }

    /// <summary>Discards everything. Useful default for tests.</summary>
    public sealed class NullLog : ILog
    {
        public static readonly NullLog Instance = new NullLog();
        public void Write(LogLevel level, string tag, string message, Exception exception = null) { }
        public bool IsEnabled(LogLevel level) => false;
    }
}
