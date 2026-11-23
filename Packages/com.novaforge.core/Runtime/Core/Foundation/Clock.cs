using System;

namespace NovaForge.Core.Foundation
{
    /// <summary>Time source abstraction. All time-dependent logic (daily rewards, ad caps, missions) uses this for testability.</summary>
    public interface IClock
    {
        DateTime UtcNow { get; }
    }

    public sealed class SystemClock : IClock
    {
        public DateTime UtcNow => DateTime.UtcNow;
    }

    /// <summary>Deterministic clock for tests and tooling.</summary>
    public sealed class ManualClock : IClock
    {
        public DateTime UtcNow { get; set; }

        public ManualClock(DateTime utcNow)
        {
            UtcNow = DateTime.SpecifyKind(utcNow, DateTimeKind.Utc);
        }

        public void Advance(TimeSpan delta) => UtcNow = UtcNow.Add(delta);
    }

    public static class TimeUtil
    {
        public static long ToUnixMs(DateTime utc) => new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)).ToUnixTimeMilliseconds();

        public static DateTime FromUnixMs(long ms) => DateTimeOffset.FromUnixTimeMilliseconds(ms).UtcDateTime;

        /// <summary>
        /// Day index used for "once per day" features. dayResetOffset shifts the boundary
        /// (e.g. +3h means the day rolls over at 03:00 UTC).
        /// </summary>
        public static int DayIndex(DateTime utc, TimeSpan dayResetOffset)
        {
            var shifted = utc - dayResetOffset;
            return (int)(shifted.Ticks / TimeSpan.TicksPerDay);
        }
    }
}
