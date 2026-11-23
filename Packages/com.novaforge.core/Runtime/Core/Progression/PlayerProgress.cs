using System;
using System.Collections.Generic;
using NovaForge.Core.Data;
using NovaForge.Core.Foundation;

namespace NovaForge.Core.Progression
{
    /// <summary>Ordered list of level ids of a game. Implemented by the game (ScriptableObject catalog, JSON, generated...).</summary>
    public interface ILevelCatalog
    {
        int Count { get; }
        string GetLevelId(int index);
        /// <returns>-1 when unknown.</returns>
        int IndexOf(string levelId);
    }

    public sealed class ListLevelCatalog : ILevelCatalog
    {
        private readonly List<string> _ids;
        private readonly Dictionary<string, int> _index = new Dictionary<string, int>();

        public ListLevelCatalog(IEnumerable<string> ids)
        {
            _ids = new List<string>(ids ?? throw new ArgumentNullException(nameof(ids)));
            for (int i = 0; i < _ids.Count; i++)
            {
                Guard.Id(_ids[i], nameof(ids));
                if (_index.ContainsKey(_ids[i])) throw new ArgumentException($"Duplicate level id '{_ids[i]}'.");
                _index[_ids[i]] = i;
            }
        }

        public int Count => _ids.Count;
        public string GetLevelId(int index) => _ids[index];
        public int IndexOf(string levelId) => levelId != null && _index.TryGetValue(levelId, out var i) ? i : -1;
    }

    public sealed class LevelRecord
    {
        public bool Completed;
        public int BestStars;
        public long BestScore;
        public int Attempts;
        public int Fails;
    }

    public sealed class ProgressState : ISaveValidatable
    {
        public Dictionary<string, LevelRecord> Levels = new Dictionary<string, LevelRecord>();
        public int HighestUnlockedIndex;

        public string Validate()
        {
            if (Levels == null) return "null levels";
            if (HighestUnlockedIndex < 0) return "negative unlock index";
            foreach (var kv in Levels)
                if (kv.Value == null || kv.Value.BestStars < 0 || kv.Value.BestStars > 3) return $"bad record {kv.Key}";
            return null;
        }
    }

    public sealed class LevelCompletionResult
    {
        public string LevelId;
        public bool FirstCompletion;
        public int Stars;
        public int PreviousBestStars;
        public bool NewBestScore;
        /// <summary>-1 if nothing new was unlocked.</summary>
        public int NewlyUnlockedIndex = -1;
        public int StarsGained => Math.Max(0, Stars - PreviousBestStars);
    }

    public readonly struct LevelUnlocked
    {
        public readonly int Index;
        public readonly string LevelId;
        public LevelUnlocked(int index, string levelId) { Index = index; LevelId = levelId; }
    }

    /// <summary>Persistent per-level progress: unlocks, stars (0-3), best score, attempts.</summary>
    public sealed class ProgressionService
    {
        public const int SaveVersion = 1;
        public const int MaxStars = 3;
        private readonly ILevelCatalog _catalog;
        private readonly SaveSlot<ProgressState> _slot;
        private readonly IEventBus _events;
        private readonly ProgressState _state;

        public ProgressionService(ILevelCatalog catalog, SaveManager saves, IEventBus events)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _events = events;
            _slot = new SaveSlot<ProgressState>(saves, "progress", SaveVersion, () => new ProgressState());
            _state = _slot.Load().Data;
            if (_catalog.Count > 0 && _state.HighestUnlockedIndex >= _catalog.Count) _state.HighestUnlockedIndex = _catalog.Count - 1;
        }

        public ILevelCatalog Catalog => _catalog;
        public int HighestUnlockedIndex => _state.HighestUnlockedIndex;

        public int TotalStars
        {
            get { int s = 0; foreach (var r in _state.Levels.Values) s += r.BestStars; return s; }
        }

        public int CompletedCount
        {
            get { int c = 0; foreach (var r in _state.Levels.Values) if (r.Completed) c++; return c; }
        }

        /// <summary>First level that is unlocked but not completed; last level if all done.</summary>
        public int NextLevelIndex
        {
            get
            {
                for (int i = 0; i <= _state.HighestUnlockedIndex && i < _catalog.Count; i++)
                    if (!Get(_catalog.GetLevelId(i)).Completed) return i;
                return Math.Min(_state.HighestUnlockedIndex, Math.Max(0, _catalog.Count - 1));
            }
        }

        public bool IsUnlocked(int index) => index >= 0 && index < _catalog.Count && index <= _state.HighestUnlockedIndex;
        public bool IsUnlocked(string levelId) => IsUnlocked(_catalog.IndexOf(levelId));

        /// <summary>Returns a copy; mutate through Record* methods only.</summary>
        public LevelRecord Get(string levelId)
        {
            if (!_state.Levels.TryGetValue(levelId ?? string.Empty, out var r)) return new LevelRecord();
            return new LevelRecord { Completed = r.Completed, BestStars = r.BestStars, BestScore = r.BestScore, Attempts = r.Attempts, Fails = r.Fails };
        }

        public void RecordAttempt(string levelId)
        {
            Mutable(RequireKnown(levelId)).Attempts++;
            _slot.Save(_state);
        }

        public void RecordFail(string levelId)
        {
            Mutable(RequireKnown(levelId)).Fails++;
            _slot.Save(_state);
        }

        public LevelCompletionResult RecordCompletion(string levelId, int stars, long score)
        {
            int index = _catalog.IndexOf(RequireKnown(levelId));
            if (!IsUnlocked(index)) throw new InvalidOperationException($"Level '{levelId}' is locked.");
            stars = Math.Max(0, Math.Min(MaxStars, stars));
            var r = Mutable(levelId);
            var result = new LevelCompletionResult
            {
                LevelId = levelId,
                FirstCompletion = !r.Completed,
                Stars = stars,
                PreviousBestStars = r.BestStars,
                NewBestScore = score > r.BestScore,
            };
            r.Completed = true;
            r.BestStars = Math.Max(r.BestStars, stars);
            r.BestScore = Math.Max(r.BestScore, score);

            if (index == _state.HighestUnlockedIndex && index + 1 < _catalog.Count)
            {
                _state.HighestUnlockedIndex = index + 1;
                result.NewlyUnlockedIndex = index + 1;
            }
            _slot.Save(_state);
            if (result.NewlyUnlockedIndex >= 0)
                _events?.Publish(new LevelUnlocked(result.NewlyUnlockedIndex, _catalog.GetLevelId(result.NewlyUnlockedIndex)));
            return result;
        }

        private LevelRecord Mutable(string levelId)
        {
            if (!_state.Levels.TryGetValue(levelId, out var r))
            {
                r = new LevelRecord();
                _state.Levels[levelId] = r;
            }
            return r;
        }

        private string RequireKnown(string levelId)
        {
            if (_catalog.IndexOf(levelId) < 0) throw new ArgumentException($"Unknown level '{levelId}'.");
            return levelId;
        }
    }
}
