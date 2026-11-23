using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using NovaForge.Core.Analytics;
using NovaForge.Core.Data;
using NovaForge.Core.Economy;
using NovaForge.Core.Foundation;

namespace NovaForge.Core.Meta
{
    public enum DailyClaimStatus
    {
        Claimed,
        AlreadyClaimedToday,
        Disabled,
        /// <summary>Device clock moved backwards beyond tolerance; claiming is blocked until time catches up.</summary>
        ClockTampering,
        GrantFailed,
    }

    public sealed class DailyRewardState : ISaveValidatable
    {
        public int LastClaimDay = int.MinValue;
        /// <summary>Index of the NEXT reward to give in the schedule.</summary>
        public int StreakIndex;
        public long LastSeenUnixMs;
        public string Validate() => StreakIndex < 0 ? "negative streak" : null;
    }

    public sealed class DailyRewardOptions
    {
        public TimeSpan DayResetOffset = TimeSpan.Zero;
        /// <summary>If true, missing a day restarts the schedule from day 1.</summary>
        public bool ResetStreakOnMissedDay = true;
        /// <summary>Allowed backwards clock drift (timezone changes, NTP corrections).</summary>
        public TimeSpan ClockRollbackTolerance = TimeSpan.FromHours(26);
    }

    /// <summary>
    /// Daily login reward calendar. Schedule comes from config (remote-tunable), e.g. [{"coin":50},{"coin":75,"hint":1}].
    /// Uses device UTC time, which a player can manipulate: rollback detection limits abuse, and the
    /// economy's daily earn caps bound the damage. Use a server time source if the rewards are valuable.
    /// </summary>
    public sealed class DailyRewardService
    {
        public const int SaveVersion = 1;
        private const string Tag = "DailyReward";
        private readonly Func<IReadOnlyList<RewardBundle>> _schedule;
        private readonly Func<bool> _enabled;
        private readonly IRewardService _rewards;
        private readonly IAnalytics _analytics;
        private readonly IClock _clock;
        private readonly ILog _log;
        private readonly DailyRewardOptions _options;
        private readonly SaveSlot<DailyRewardState> _slot;
        private readonly DailyRewardState _state;

        public DailyRewardService(Func<IReadOnlyList<RewardBundle>> schedule, Func<bool> enabled, IRewardService rewards,
            IAnalytics analytics, SaveManager saves, IClock clock, ILog log, DailyRewardOptions options = null)
        {
            _schedule = schedule ?? throw new ArgumentNullException(nameof(schedule));
            _enabled = enabled ?? (() => true);
            _rewards = rewards ?? throw new ArgumentNullException(nameof(rewards));
            _analytics = analytics ?? throw new ArgumentNullException(nameof(analytics));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _options = options ?? new DailyRewardOptions();
            _slot = new SaveSlot<DailyRewardState>(saves, "daily_reward", SaveVersion, () => new DailyRewardState());
            _state = _slot.Load().Data;
        }

        /// <summary>Parses the JSON schedule format used in config.</summary>
        public static IReadOnlyList<RewardBundle> ParseSchedule(JToken json)
        {
            var list = new List<RewardBundle>();
            if (!(json is JArray arr)) throw new FormatException("Daily reward schedule must be a JSON array.");
            foreach (var day in arr)
            {
                if (!(day is JObject obj) || !obj.HasValues) throw new FormatException("Each schedule day must be a non-empty object.");
                var b = new RewardBundle();
                foreach (var prop in obj.Properties()) b.Add(prop.Name, prop.Value.Value<long>());
                list.Add(b);
            }
            if (list.Count == 0) throw new FormatException("Daily reward schedule is empty.");
            return list;
        }

        public bool CanClaim => _enabled() && !IsClockTampered() && Today() != _state.LastClaimDay;

        /// <summary>Zero-based position in the schedule the next claim will grant.</summary>
        public int CurrentDayIndex
        {
            get
            {
                var schedule = _schedule();
                int idx = _state.StreakIndex;
                if (_options.ResetStreakOnMissedDay && _state.LastClaimDay != int.MinValue && Today() > _state.LastClaimDay + 1) idx = 0;
                return schedule.Count == 0 ? 0 : idx % schedule.Count;
            }
        }

        /// <summary>Current schedule (config-driven, may change with remote config).</summary>
        public IReadOnlyList<RewardBundle> Schedule => _schedule();

        public RewardBundle NextReward
        {
            get { var s = _schedule(); return s.Count == 0 ? null : s[CurrentDayIndex]; }
        }

        public TimeSpan TimeUntilNextClaim
        {
            get
            {
                if (CanClaim) return TimeSpan.Zero;
                var now = _clock.UtcNow;
                var nextDayStart = new DateTime((Today() + 1) * TimeSpan.TicksPerDay, DateTimeKind.Utc) + _options.DayResetOffset;
                return nextDayStart > now ? nextDayStart - now : TimeSpan.Zero;
            }
        }

        public DailyClaimStatus Claim()
        {
            if (!_enabled()) return DailyClaimStatus.Disabled;
            if (IsClockTampered())
            {
                _log.Warning(Tag, "Clock moved backwards; claim blocked.");
                return DailyClaimStatus.ClockTampering;
            }
            int today = Today();
            if (today == _state.LastClaimDay) return DailyClaimStatus.AlreadyClaimedToday;

            int dayIdx = CurrentDayIndex;
            var reward = _schedule()[dayIdx];
            var status = _rewards.Grant("daily:" + today, "daily_reward", reward);
            if (status != TransactionStatus.Success && status != TransactionStatus.Duplicate)
            {
                _log.Error(Tag, $"Daily reward grant failed: {status}");
                return DailyClaimStatus.GrantFailed;
            }

            _state.LastClaimDay = today;
            _state.StreakIndex = dayIdx + 1;
            _state.LastSeenUnixMs = Math.Max(_state.LastSeenUnixMs, TimeUtil.ToUnixMs(_clock.UtcNow));
            _slot.Save(_state);
            _analytics.Log(AnalyticsEvents.DailyRewardClaimed, "day", dayIdx + 1);
            return DailyClaimStatus.Claimed;
        }

        /// <summary>Call on app start / resume to record the latest trusted time.</summary>
        public void Touch()
        {
            long now = TimeUtil.ToUnixMs(_clock.UtcNow);
            if (now > _state.LastSeenUnixMs)
            {
                _state.LastSeenUnixMs = now;
                _slot.Save(_state);
            }
        }

        private bool IsClockTampered()
        {
            if (_state.LastSeenUnixMs == 0) return false;
            var lastSeen = TimeUtil.FromUnixMs(_state.LastSeenUnixMs);
            return _clock.UtcNow < lastSeen - _options.ClockRollbackTolerance;
        }

        private int Today() => TimeUtil.DayIndex(_clock.UtcNow, _options.DayResetOffset);
    }
}
