using System;
using System.Collections.Generic;
using NovaForge.Core.Data;
using NovaForge.Core.Foundation;

namespace NovaForge.Core.Progression
{
    /// <summary>Stat keys used by Core. Games add their own (e.g. "colors_mixed").</summary>
    public static class CoreStats
    {
        public const string LevelsCompleted = "levels_completed";
        public const string LevelsFailed = "levels_failed";
        public const string LevelsStarted = "levels_started";
        public const string StarsEarned = "stars_earned";
        public const string ThreeStarLevels = "three_star_levels";
        public const string RewardedAdsWatched = "rewarded_ads_watched";
        public const string DailyRewardsClaimed = "daily_rewards_claimed";
        public const string SessionsPlayed = "sessions_played";
        public const string CoinsEarned = "coins_earned";
    }

    public readonly struct StatChanged
    {
        public readonly string Key;
        public readonly long Value;
        public StatChanged(string key, long value) { Key = key; Value = value; }
    }

    public sealed class StatisticsState : ISaveValidatable
    {
        public Dictionary<string, long> Counters = new Dictionary<string, long>();
        public string Validate() => Counters == null ? "null" : null;
    }

    public interface IStatistics
    {
        long Get(string key);
        void Increment(string key, long amount = 1);
        /// <summary>Stores max(current, value): for "best combo", "highest level reached"...</summary>
        void SetMax(string key, long value);
    }

    /// <summary>Monotonic player counters. Feeds missions and achievements through <see cref="StatChanged"/> events.</summary>
    public sealed class PlayerStatistics : IStatistics
    {
        public const int SaveVersion = 1;
        private readonly SaveSlot<StatisticsState> _slot;
        private readonly IEventBus _events;
        private readonly StatisticsState _state;

        public PlayerStatistics(SaveManager saves, IEventBus events)
        {
            _events = events;
            _slot = new SaveSlot<StatisticsState>(saves, "statistics", SaveVersion, () => new StatisticsState());
            _state = _slot.Load().Data;
        }

        public long Get(string key) => _state.Counters.TryGetValue(key ?? string.Empty, out var v) ? v : 0;

        public void Increment(string key, long amount = 1)
        {
            Guard.Id(key, nameof(key));
            if (amount <= 0) return;
            long v = Get(key) + amount;
            _state.Counters[key] = v;
            _slot.Save(_state);
            _events?.Publish(new StatChanged(key, v));
        }

        public void SetMax(string key, long value)
        {
            Guard.Id(key, nameof(key));
            if (value <= Get(key)) return;
            _state.Counters[key] = value;
            _slot.Save(_state);
            _events?.Publish(new StatChanged(key, value));
        }
    }
}
