using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NovaForge.Core.Analytics;
using NovaForge.Core.Data;
using NovaForge.Core.Economy;
using NovaForge.Core.Foundation;
using NovaForge.Core.Privacy;

namespace NovaForge.Core.Tests
{
    /// <summary>Shared in-memory composition for tests.</summary>
    internal sealed class TestEnv
    {
        public readonly ManualClock Clock = new ManualClock(new DateTime(2026, 3, 10, 12, 0, 0, DateTimeKind.Utc));
        public readonly RingBufferLogSink LogSink = new RingBufferLogSink(500);
        public readonly LoggingService Log;
        public readonly ErrorHandler Errors;
        public readonly EventBus Events;
        public InMemorySaveStorage Storage = new InMemorySaveStorage();
        public SaveManager Saves;
        public readonly RecordingAnalyticsProvider AnalyticsProvider = new RecordingAnalyticsProvider();

        public TestEnv()
        {
            Log = new LoggingService(Clock, LogLevel.Debug);
            Log.AddSink(LogSink);
            Errors = new ErrorHandler(Log, Clock);
            Events = new EventBus(Errors);
            Saves = new SaveManager(Storage, Log, Errors, Clock);
        }

        /// <summary>Simulates an app restart: same storage, fresh service instances.</summary>
        public SaveManager Restart()
        {
            Saves = new SaveManager(Storage, Log, Errors, Clock);
            return Saves;
        }

        public static EconomyDefinition DefaultEconomy() => new EconomyDefinition()
            .AddCurrency("coin", initial: 100, max: 1_000_000)
            .AddCurrency("gem", initial: 0, max: 10_000)
            .AddItem("hint", initial: 0, max: 99);

        public EconomyService Economy(EconomyDefinition def = null) => new EconomyService(def ?? DefaultEconomy(), Saves, Clock, Log, Events);

        public ConsentService Consent(bool analyticsGranted = true)
        {
            var c = new ConsentService(Saves, Clock, Events);
            if (analyticsGranted) c.SetAnalytics(ConsentStatus.Granted);
            return c;
        }

        public AnalyticsService Analytics(IConsent consent)
        {
            var a = new AnalyticsService(consent, Log, Errors, Events);
            a.AddProvider(AnalyticsProvider);
            return a;
        }
    }

    /// <summary>
    /// Runs async code synchronously for Unity Test Framework 1.x (no async test support).
    /// Task.Run avoids deadlocks with UnitySynchronizationContext in the Editor.
    /// </summary>
    internal static class AsyncTest
    {
        public static T Run<T>(Func<Task<T>> f) => Task.Run(f).GetAwaiter().GetResult();
        public static void Run(Func<Task> f) => Task.Run(f).GetAwaiter().GetResult();
    }

    internal sealed class RecordingAnalyticsProvider : IAnalyticsProvider
    {
        public readonly List<(string name, IReadOnlyDictionary<string, object> p)> Events = new List<(string, IReadOnlyDictionary<string, object>)>();
        public bool CollectionEnabled;
        public string Name => "recording";
        public void Initialize() { }
        public void SetCollectionEnabled(bool enabled) => CollectionEnabled = enabled;
        public void LogEvent(string name, IReadOnlyDictionary<string, object> parameters) => Events.Add((name, parameters));
        public void SetUserProperty(string name, string value) { }
        public int Count(string name) { int n = 0; foreach (var e in Events) if (e.name == name) n++; return n; }
    }

    /// <summary>Storage whose writes can be made to fail, to test rollback paths.</summary>
    internal sealed class FlakyStorage : ISaveStorage
    {
        private readonly InMemorySaveStorage _inner = new InMemorySaveStorage();
        public bool FailWrites;
        public bool Exists(string key) => _inner.Exists(key);
        public string Read(string key) => _inner.Read(key);
        public void Write(string key, string content)
        {
            if (FailWrites) throw new System.IO.IOException("disk full (simulated)");
            _inner.Write(key, content);
        }
        public void Delete(string key) => _inner.Delete(key);
        public IEnumerable<string> Keys() => _inner.Keys();
    }
}
