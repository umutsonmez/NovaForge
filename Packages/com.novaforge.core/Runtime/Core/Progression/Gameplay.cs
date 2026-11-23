using System;
using System.Collections.Generic;
using NovaForge.Core.Config;

namespace NovaForge.Core.Progression
{
    /// <summary>Per-attempt score with star thresholds. Genre-neutral: the game decides what earns points.</summary>
    public sealed class ScoreTracker
    {
        private readonly long[] _starThresholds;
        public long Score { get; private set; }

        /// <param name="starThresholds">Ascending scores for 1, 2, 3 stars.</param>
        public ScoreTracker(params long[] starThresholds)
        {
            _starThresholds = starThresholds ?? Array.Empty<long>();
            for (int i = 1; i < _starThresholds.Length; i++)
                if (_starThresholds[i] < _starThresholds[i - 1]) throw new ArgumentException("Star thresholds must be ascending.");
        }

        public void Add(long points)
        {
            if (points < 0) throw new ArgumentOutOfRangeException(nameof(points));
            Score += points;
        }

        public void Reset() => Score = 0;

        public int Stars
        {
            get { int s = 0; foreach (var t in _starThresholds) if (Score >= t) s++; return Math.Min(ProgressionService.MaxStars, s); }
        }
    }

    /// <summary>One measurable goal inside a level ("deliver 3 purple drops", "reach 1000 points").</summary>
    public sealed class Objective
    {
        public string Id { get; }
        public long Target { get; }
        public long Current { get; private set; }
        public bool IsComplete => Current >= Target;

        public Objective(string id, long target)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            if (target <= 0) throw new ArgumentOutOfRangeException(nameof(target));
            Target = target;
        }

        internal bool Add(long amount)
        {
            if (amount <= 0 || IsComplete) return false;
            Current = Math.Min(Target, Current + amount);
            return true;
        }
    }

    public sealed class ObjectiveTracker
    {
        private readonly Dictionary<string, Objective> _objectives = new Dictionary<string, Objective>();
        public event Action<Objective> ObjectiveProgressed;
        public event Action AllCompleted;

        public IEnumerable<Objective> Objectives => _objectives.Values;

        public ObjectiveTracker Add(Objective o)
        {
            _objectives.Add(o.Id, o);
            return this;
        }

        public bool AllComplete
        {
            get { foreach (var o in _objectives.Values) if (!o.IsComplete) return false; return _objectives.Count > 0; }
        }

        public void Report(string objectiveId, long amount = 1)
        {
            if (!_objectives.TryGetValue(objectiveId, out var o) || !o.Add(amount)) return;
            ObjectiveProgressed?.Invoke(o);
            if (AllComplete) AllCompleted?.Invoke();
        }
    }

    /// <summary>
    /// Light dynamic difficulty: after N consecutive fails on a level, the multiplier eases toward the configured minimum.
    /// Games interpret the multiplier (e.g. more moves = base / multiplier, slower enemies = speed * multiplier).
    /// </summary>
    public sealed class DifficultyService
    {
        private readonly IConfig _config;
        public DifficultyService(IConfig config) { _config = config ?? throw new ArgumentNullException(nameof(config)); }

        /// <summary>1.0 = designed difficulty; lower = easier.</summary>
        public float GetMultiplier(int consecutiveFails)
        {
            float max = _config.GetFloat(CoreConfig.DifficultyMaxMultiplier);
            if (!_config.GetBool(CoreConfig.DifficultyAdaptiveEnabled)) return max;
            float min = _config.GetFloat(CoreConfig.DifficultyMinMultiplier);
            int every = Math.Max(1, _config.GetInt(CoreConfig.DifficultyEaseAfterFails));
            int steps = Math.Max(0, consecutiveFails) / every;
            return Math.Max(min, max - 0.1f * steps);
        }
    }
}
