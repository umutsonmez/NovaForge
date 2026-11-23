using System;
using System.Collections.Generic;
using NovaForge.Core.Analytics;
using NovaForge.Core.Data;
using NovaForge.Core.Economy;
using NovaForge.Core.Foundation;
using NovaForge.Core.Progression;

namespace NovaForge.Core.Meta
{
    public enum MissionPeriod { Daily, Weekly, Permanent }

    public sealed class MissionDefinition
    {
        public string Id { get; }
        public string StatKey { get; }
        public long Target { get; }
        public RewardBundle Reward { get; }
        public MissionPeriod Period { get; }
        public string TitleKey { get; }

        public MissionDefinition(string id, string statKey, long target, RewardBundle reward, MissionPeriod period, string titleKey = null)
        {
            Id = Guard.Id(id, nameof(id));
            StatKey = Guard.Id(statKey, nameof(statKey));
            if (target <= 0) throw new ArgumentOutOfRangeException(nameof(target));
            Target = target;
            Reward = reward ?? throw new ArgumentNullException(nameof(reward));
            Period = period;
            TitleKey = titleKey ?? "mission." + id;
        }
    }

    public sealed class MissionsState : ISaveValidatable
    {
        public int DailyPeriod = int.MinValue;
        public int WeeklyPeriod = int.MinValue;
        /// <summary>Stat value at the start of the mission's current period.</summary>
        public Dictionary<string, long> Baselines = new Dictionary<string, long>();
        public HashSet<string> Claimed = new HashSet<string>();
        public string Validate() => Baselines == null || Claimed == null ? "null" : null;
    }

    public enum MissionClaimStatus { Claimed, NotComplete, AlreadyClaimed, Unknown, GrantFailed }

    public readonly struct MissionProgress
    {
        public readonly MissionDefinition Mission;
        public readonly long Current;
        public readonly bool Claimed;
        public MissionProgress(MissionDefinition m, long current, bool claimed) { Mission = m; Current = current; Claimed = claimed; }
        public bool IsComplete => Current >= Mission.Target;
    }

    /// <summary>
    /// Stat-driven missions: progress = stat now - stat at period start. Daily/weekly periods reset automatically.
    /// Rewards must be claimed by the player (explicit action, idempotent grant per period).
    /// </summary>
    public sealed class MissionService
    {
        public const int SaveVersion = 1;
        private readonly Dictionary<string, MissionDefinition> _defs = new Dictionary<string, MissionDefinition>();
        private readonly IStatistics _stats;
        private readonly IRewardService _rewards;
        private readonly IAnalytics _analytics;
        private readonly IClock _clock;
        private readonly TimeSpan _dayOffset;
        private readonly SaveSlot<MissionsState> _slot;
        private readonly MissionsState _state;

        public MissionService(IEnumerable<MissionDefinition> missions, IStatistics stats, IRewardService rewards, IAnalytics analytics,
            SaveManager saves, IClock clock, TimeSpan? dayResetOffset = null)
        {
            foreach (var m in missions ?? throw new ArgumentNullException(nameof(missions)))
            {
                if (_defs.ContainsKey(m.Id)) throw new ArgumentException($"Duplicate mission '{m.Id}'.");
                _defs[m.Id] = m;
            }
            _stats = stats ?? throw new ArgumentNullException(nameof(stats));
            _rewards = rewards ?? throw new ArgumentNullException(nameof(rewards));
            _analytics = analytics ?? throw new ArgumentNullException(nameof(analytics));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _dayOffset = dayResetOffset ?? TimeSpan.Zero;
            _slot = new SaveSlot<MissionsState>(saves, "missions", SaveVersion, () => new MissionsState());
            _state = _slot.Load().Data;
            RollPeriods();
        }

        public IReadOnlyList<MissionProgress> GetAll()
        {
            RollPeriods();
            var list = new List<MissionProgress>();
            foreach (var m in _defs.Values) list.Add(Progress(m));
            return list;
        }

        public MissionClaimStatus Claim(string missionId)
        {
            RollPeriods();
            if (!_defs.TryGetValue(missionId ?? string.Empty, out var m)) return MissionClaimStatus.Unknown;
            var p = Progress(m);
            if (p.Claimed) return MissionClaimStatus.AlreadyClaimed;
            if (!p.IsComplete) return MissionClaimStatus.NotComplete;
            var status = _rewards.Grant($"mission:{m.Id}:{PeriodIndex(m.Period)}", "mission", m.Reward);
            if (status != TransactionStatus.Success && status != TransactionStatus.Duplicate) return MissionClaimStatus.GrantFailed;
            _state.Claimed.Add(m.Id);
            _slot.Save(_state);
            _analytics.Log(AnalyticsEvents.MissionCompleted, "mission_id", m.Id);
            return MissionClaimStatus.Claimed;
        }

        private MissionProgress Progress(MissionDefinition m)
        {
            _state.Baselines.TryGetValue(m.Id, out var baseline);
            long current = Math.Max(0, _stats.Get(m.StatKey) - baseline);
            return new MissionProgress(m, Math.Min(current, m.Target), _state.Claimed.Contains(m.Id));
        }

        private void RollPeriods()
        {
            bool changed = false;
            int day = PeriodIndex(MissionPeriod.Daily);
            int week = PeriodIndex(MissionPeriod.Weekly);
            if (_state.DailyPeriod != day) { ResetPeriod(MissionPeriod.Daily); _state.DailyPeriod = day; changed = true; }
            if (_state.WeeklyPeriod != week) { ResetPeriod(MissionPeriod.Weekly); _state.WeeklyPeriod = week; changed = true; }
            foreach (var m in _defs.Values)
                if (m.Period == MissionPeriod.Permanent && !_state.Baselines.ContainsKey(m.Id)) { _state.Baselines[m.Id] = 0; changed = true; }
            if (changed) _slot.Save(_state);
        }

        private void ResetPeriod(MissionPeriod period)
        {
            foreach (var m in _defs.Values)
            {
                if (m.Period != period) continue;
                _state.Baselines[m.Id] = _stats.Get(m.StatKey);
                _state.Claimed.Remove(m.Id);
            }
        }

        private int PeriodIndex(MissionPeriod p)
        {
            int day = TimeUtil.DayIndex(_clock.UtcNow, _dayOffset);
            switch (p)
            {
                case MissionPeriod.Daily: return day;
                // Day 0 (1970-01-01) was a Thursday; +3 makes weeks start on Monday.
                case MissionPeriod.Weekly: return (day + 3) / 7;
                default: return 0;
            }
        }
    }

    public sealed class AchievementDefinition
    {
        public string Id { get; }
        public string StatKey { get; }
        public long Threshold { get; }
        public RewardBundle Reward { get; }
        /// <summary>Optional platform ids (Google Play Games / Game Center) for the platform adapter.</summary>
        public IReadOnlyDictionary<string, string> PlatformIds { get; }

        public AchievementDefinition(string id, string statKey, long threshold, RewardBundle reward = null, IReadOnlyDictionary<string, string> platformIds = null)
        {
            Id = Guard.Id(id, nameof(id));
            StatKey = Guard.Id(statKey, nameof(statKey));
            if (threshold <= 0) throw new ArgumentOutOfRangeException(nameof(threshold));
            Threshold = threshold;
            Reward = reward;
            PlatformIds = platformIds ?? new Dictionary<string, string>();
        }
    }

    public readonly struct AchievementUnlocked
    {
        public readonly AchievementDefinition Achievement;
        public AchievementUnlocked(AchievementDefinition a) { Achievement = a; }
    }

    public sealed class AchievementsState : ISaveValidatable
    {
        public HashSet<string> Unlocked = new HashSet<string>();
        public string Validate() => Unlocked == null ? "null" : null;
    }

    /// <summary>Permanent stat thresholds. Unlock automatically; optional reward granted once.</summary>
    public sealed class AchievementService : IDisposable
    {
        public const int SaveVersion = 1;
        private readonly List<AchievementDefinition> _defs;
        private readonly IStatistics _stats;
        private readonly IRewardService _rewards;
        private readonly IAnalytics _analytics;
        private readonly IEventBus _events;
        private readonly SaveSlot<AchievementsState> _slot;
        private readonly AchievementsState _state;
        private readonly IDisposable _sub;

        public AchievementService(IEnumerable<AchievementDefinition> achievements, IStatistics stats, IRewardService rewards,
            IAnalytics analytics, SaveManager saves, IEventBus events)
        {
            _defs = new List<AchievementDefinition>(achievements ?? throw new ArgumentNullException(nameof(achievements)));
            _stats = stats ?? throw new ArgumentNullException(nameof(stats));
            _rewards = rewards ?? throw new ArgumentNullException(nameof(rewards));
            _analytics = analytics ?? throw new ArgumentNullException(nameof(analytics));
            _events = events ?? throw new ArgumentNullException(nameof(events));
            _slot = new SaveSlot<AchievementsState>(saves, "achievements", SaveVersion, () => new AchievementsState());
            _state = _slot.Load().Data;
            _sub = _events.Subscribe<StatChanged>(e => Evaluate(e.Key));
            Evaluate(null);
        }

        public bool IsUnlocked(string id) => _state.Unlocked.Contains(id);
        public IReadOnlyList<AchievementDefinition> All => _defs;

        public float GetProgress(string id)
        {
            var d = _defs.Find(x => x.Id == id);
            if (d == null) return 0;
            return IsUnlocked(id) ? 1f : Math.Min(1f, (float)_stats.Get(d.StatKey) / d.Threshold);
        }

        public void Dispose() => _sub.Dispose();

        private void Evaluate(string statKey)
        {
            foreach (var d in _defs)
            {
                if (statKey != null && d.StatKey != statKey) continue;
                if (_state.Unlocked.Contains(d.Id) || _stats.Get(d.StatKey) < d.Threshold) continue;
                _state.Unlocked.Add(d.Id);
                _slot.Save(_state);
                if (d.Reward != null && !d.Reward.IsEmpty) _rewards.Grant("achievement:" + d.Id, "achievement", d.Reward);
                _analytics.Log(AnalyticsEvents.AchievementUnlocked, "achievement_id", d.Id);
                _events.Publish(new AchievementUnlocked(d));
            }
        }
    }
}
