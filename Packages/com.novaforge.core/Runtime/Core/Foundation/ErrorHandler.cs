using System;
using System.Collections.Generic;

namespace NovaForge.Core.Foundation
{
    /// <summary>External crash/error reporting backend (e.g. Crashlytics, Sentry, Unity Cloud Diagnostics) adapter.</summary>
    public interface IErrorReportSink
    {
        void Send(ErrorReport report);
    }

    public sealed class ErrorReport
    {
        public DateTime UtcTime;
        public string Context;
        public string ExceptionType;
        public string Message;
        public string StackTrace;
        public bool IsFatal;
    }

    public interface IErrorHandler
    {
        /// <summary>Reports a handled exception. Never throws.</summary>
        void Report(Exception exception, string context, bool isFatal = false);
        IReadOnlyList<ErrorReport> RecentErrors { get; }
    }

    /// <summary>
    /// Central place for handled errors: logs them, keeps a short history and forwards to optional sinks.
    /// Goal: nothing is swallowed silently, yet a broken subsystem cannot crash the game loop.
    /// </summary>
    public sealed class ErrorHandler : IErrorHandler
    {
        private const string Tag = "Error";
        private readonly ILog _log;
        private readonly IClock _clock;
        private readonly List<IErrorReportSink> _sinks = new List<IErrorReportSink>();
        private readonly List<ErrorReport> _recent = new List<ErrorReport>();
        private readonly int _maxRecent;
        private bool _reporting;

        public ErrorHandler(ILog log, IClock clock, int maxRecent = 50)
        {
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _maxRecent = Math.Max(1, maxRecent);
        }

        public IReadOnlyList<ErrorReport> RecentErrors => _recent;

        public void AddSink(IErrorReportSink sink) => _sinks.Add(sink ?? throw new ArgumentNullException(nameof(sink)));

        public void Report(Exception exception, string context, bool isFatal = false)
        {
            if (exception == null) return;
            // Guard against re-entrancy (a sink that throws and reports again).
            if (_reporting) return;
            _reporting = true;
            try
            {
                var report = new ErrorReport
                {
                    UtcTime = _clock.UtcNow,
                    Context = context ?? string.Empty,
                    ExceptionType = exception.GetType().FullName,
                    Message = exception.Message,
                    StackTrace = exception.StackTrace,
                    IsFatal = isFatal,
                };
                _recent.Add(report);
                if (_recent.Count > _maxRecent) _recent.RemoveAt(0);

                _log.Error(Tag, $"{(isFatal ? "FATAL " : string.Empty)}{context}: {exception.GetType().Name}: {exception.Message}", exception);

                for (int i = 0; i < _sinks.Count; i++)
                {
                    try { _sinks[i].Send(report); }
                    catch (Exception sinkEx) { _log.Warning(Tag, $"Error sink {_sinks[i].GetType().Name} failed: {sinkEx.Message}"); }
                }
            }
            finally
            {
                _reporting = false;
            }
        }
    }
}
