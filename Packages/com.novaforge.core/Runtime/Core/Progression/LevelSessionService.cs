using System;
using System.Collections.Generic;
using NovaForge.Core.Ads;
using NovaForge.Core.Analytics;
using NovaForge.Core.Economy;
using NovaForge.Core.Foundation;

namespace NovaForge.Core.Progression
{
    public enum LevelSessionState { Idle, Playing, Paused, Won, Lost }

    public readonly struct LevelSessionEnded
    {
        public readonly string LevelId;
        public readonly bool Won;
        public readonly LevelCompletionResult Completion;
        public readonly RewardBundle RewardGranted;
        public LevelSessionEnded(string levelId, bool won, LevelCompletionResult completion, RewardBundle reward)
        { LevelId = levelId; Won = won; Completion = completion; RewardGranted = reward; }
    }

    /// <summary>Computes the reward for a completion. Games provide their own (e.g. coins per star, first-clear bonus).</summary>
    public interface ILevelRewardPolicy
    {
        /// <returns>null or empty for no reward.</returns>
        RewardBundle GetReward(LevelCompletionResult completion);
    }

    /// <summary>
    /// Orchestrates one level attempt and wires the cross-cutting systems together:
    /// progression, statistics (→ missions/achievements), rewards (idempotent), analytics, ad pacing.
    /// Genre-specific gameplay only calls Start / Win / Lose / Restart.
    /// </summary>
    public sealed class LevelSessionService
    {
        private readonly ProgressionService _progression;
        private readonly IStatistics _stats;
        private readonly IRewardService _rewards;
        private readonly ILevelRewardPolicy _rewardPolicy;
        private readonly IAnalytics _analytics;
        private readonly AdsService _ads;
        private readonly IClock _clock;
        private readonly IEventBus _events;
        private DateTime _startedUtc;
        private int _attemptCounter;

        public LevelSessionState State { get; private set; } = LevelSessionState.Idle;
        public string CurrentLevelId { get; private set; }
        public int ConsecutiveFails { get; private set; }

        /// <param name="ads">Optional: null when a game ships without ads.</param>
        public LevelSessionService(ProgressionService progression, IStatistics stats, IRewardService rewards, ILevelRewardPolicy rewardPolicy,
            IAnalytics analytics, AdsService ads, IClock clock, IEventBus events)
        {
            _progression = progression ?? throw new ArgumentNullException(nameof(progression));
            _stats = stats ?? throw new ArgumentNullException(nameof(stats));
            _rewards = rewards ?? throw new ArgumentNullException(nameof(rewards));
            _rewardPolicy = rewardPolicy ?? throw new ArgumentNullException(nameof(rewardPolicy));
            _analytics = analytics ?? throw new ArgumentNullException(nameof(analytics));
            _ads = ads;
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _events = events;
        }

        public void Start(string levelId)
        {
            if (State == LevelSessionState.Playing || State == LevelSessionState.Paused)
                throw new InvalidOperationException($"Level '{CurrentLevelId}' is still in progress.");
            if (!_progression.IsUnlocked(levelId)) throw new InvalidOperationException($"Level '{levelId}' is locked.");
            if (CurrentLevelId != levelId) ConsecutiveFails = 0;
            CurrentLevelId = levelId;
            State = LevelSessionState.Playing;
            _startedUtc = _clock.UtcNow;
            _attemptCounter++;
            _progression.RecordAttempt(levelId);
            _stats.Increment(CoreStats.LevelsStarted);
            _analytics.Log(AnalyticsEvents.LevelStarted, AnalyticsParams.LevelId, levelId);
        }

        public void Pause() { if (State == LevelSessionState.Playing) State = LevelSessionState.Paused; }
        public void Resume() { if (State == LevelSessionState.Paused) State = LevelSessionState.Playing; }

        public LevelSessionEnded Win(int stars, long score)
        {
            RequireActive();
            var completion = _progression.RecordCompletion(CurrentLevelId, stars, score);
            State = LevelSessionState.Won;
            ConsecutiveFails = 0;

            _stats.Increment(CoreStats.LevelsCompleted);
            if (completion.StarsGained > 0) _stats.Increment(CoreStats.StarsEarned, completion.StarsGained);
            if (completion.Stars == ProgressionService.MaxStars && completion.PreviousBestStars < ProgressionService.MaxStars)
                _stats.Increment(CoreStats.ThreeStarLevels);

            var reward = _rewardPolicy.GetReward(completion);
            RewardBundle granted = null;
            if (reward != null && !reward.IsEmpty)
            {
                // Deterministic for first clears (can never be granted twice); per-attempt for repeat rewards.
                var grantId = completion.FirstCompletion
                    ? $"level:{CurrentLevelId}:first"
                    : $"level:{CurrentLevelId}:{TimeUtil.ToUnixMs(_clock.UtcNow)}:{_attemptCounter}";
                if (_rewards.Grant(grantId, "level_reward", reward) == TransactionStatus.Success) granted = reward;
            }

            _ads?.NotifyLevelCompleted();
            _analytics.Log(AnalyticsEvents.LevelCompleted, new Dictionary<string, object>
            {
                { AnalyticsParams.LevelId, CurrentLevelId },
                { "stars", completion.Stars },
                { "score", score },
                { "first_clear", completion.FirstCompletion },
                { AnalyticsParams.DurationSec, (long)(_clock.UtcNow - _startedUtc).TotalSeconds },
            });
            var ended = new LevelSessionEnded(CurrentLevelId, true, completion, granted);
            _events?.Publish(ended);
            return ended;
        }

        public LevelSessionEnded Lose(string reason)
        {
            RequireActive();
            State = LevelSessionState.Lost;
            ConsecutiveFails++;
            _progression.RecordFail(CurrentLevelId);
            _stats.Increment(CoreStats.LevelsFailed);
            _analytics.Log(AnalyticsEvents.LevelFailed, new Dictionary<string, object>
            {
                { AnalyticsParams.LevelId, CurrentLevelId },
                { AnalyticsParams.Reason, reason ?? "unknown" },
                { "consecutive_fails", ConsecutiveFails },
                { AnalyticsParams.DurationSec, (long)(_clock.UtcNow - _startedUtc).TotalSeconds },
            });
            var ended = new LevelSessionEnded(CurrentLevelId, false, null, null);
            _events?.Publish(ended);
            return ended;
        }

        /// <summary>Abandons the current attempt (counts as a restart, not a fail) and starts again.</summary>
        public void Restart()
        {
            if (CurrentLevelId == null) throw new InvalidOperationException("No level to restart.");
            _analytics.Log(AnalyticsEvents.LevelRestarted, AnalyticsParams.LevelId, CurrentLevelId);
            State = LevelSessionState.Idle;
            Start(CurrentLevelId);
        }

        public void Quit()
        {
            State = LevelSessionState.Idle;
        }

        private void RequireActive()
        {
            if (State != LevelSessionState.Playing && State != LevelSessionState.Paused)
                throw new InvalidOperationException($"No active level (state {State}).");
        }
    }

    /// <summary>Simple default: coins per star on every clear, plus a first-clear bonus.</summary>
    public sealed class StarBasedRewardPolicy : ILevelRewardPolicy
    {
        private readonly string _resourceId;
        private readonly long _perStar;
        private readonly long _firstClearBonus;
        private readonly long _replayPerStar;

        public StarBasedRewardPolicy(string resourceId, long perStar, long firstClearBonus, long replayPerStar = 0)
        {
            _resourceId = resourceId; _perStar = perStar; _firstClearBonus = firstClearBonus; _replayPerStar = replayPerStar;
        }

        public RewardBundle GetReward(LevelCompletionResult c)
        {
            long amount = c.FirstCompletion ? _firstClearBonus + _perStar * c.Stars : _perStar * c.StarsGained + _replayPerStar * c.Stars;
            return amount > 0 ? RewardBundle.Of(_resourceId, amount) : null;
        }
    }
}
