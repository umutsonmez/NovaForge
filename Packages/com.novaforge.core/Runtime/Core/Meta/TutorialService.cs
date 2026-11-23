using System;
using System.Collections.Generic;
using NovaForge.Core.Analytics;
using NovaForge.Core.Data;
using NovaForge.Core.Foundation;

namespace NovaForge.Core.Meta
{
    public sealed class TutorialDefinition
    {
        public string Id { get; }
        /// <summary>Ordered step ids. The game's UI maps each step to highlights / hand pointers / localized text.</summary>
        public IReadOnlyList<string> Steps { get; }
        public bool Skippable { get; }

        public TutorialDefinition(string id, IReadOnlyList<string> steps, bool skippable = true)
        {
            Id = Guard.Id(id, nameof(id));
            if (steps == null || steps.Count == 0) throw new ArgumentException("Tutorial needs at least one step.", nameof(steps));
            Steps = steps;
            Skippable = skippable;
        }
    }

    public sealed class TutorialState : ISaveValidatable
    {
        public HashSet<string> Completed = new HashSet<string>();
        public string Validate() => Completed == null ? "null" : null;
    }

    public readonly struct TutorialStepChanged
    {
        public readonly string TutorialId;
        /// <summary>null when the tutorial ended.</summary>
        public readonly string StepId;
        public readonly int StepIndex;
        public TutorialStepChanged(string tutorialId, string stepId, int stepIndex) { TutorialId = tutorialId; StepId = stepId; StepIndex = stepIndex; }
    }

    /// <summary>
    /// Tracks tutorial sequences. Only completion is persisted: an interrupted tutorial restarts from step 0
    /// next time (simpler and less error-prone than resuming mid-tutorial with a different game state).
    /// </summary>
    public sealed class TutorialService
    {
        public const int SaveVersion = 1;
        private readonly Dictionary<string, TutorialDefinition> _defs = new Dictionary<string, TutorialDefinition>();
        private readonly IAnalytics _analytics;
        private readonly IEventBus _events;
        private readonly SaveSlot<TutorialState> _slot;
        private readonly TutorialState _state;

        public string ActiveTutorialId { get; private set; }
        public int ActiveStepIndex { get; private set; } = -1;
        public string ActiveStepId => ActiveTutorialId == null ? null : _defs[ActiveTutorialId].Steps[ActiveStepIndex];

        public TutorialService(IEnumerable<TutorialDefinition> tutorials, IAnalytics analytics, SaveManager saves, IEventBus events)
        {
            foreach (var t in tutorials ?? throw new ArgumentNullException(nameof(tutorials))) _defs[t.Id] = t;
            _analytics = analytics ?? throw new ArgumentNullException(nameof(analytics));
            _events = events;
            _slot = new SaveSlot<TutorialState>(saves, "tutorials", SaveVersion, () => new TutorialState());
            _state = _slot.Load().Data;
        }

        public bool IsCompleted(string id) => _state.Completed.Contains(id);

        /// <returns>false if already completed, unknown, or another tutorial is running.</returns>
        public bool TryStart(string id)
        {
            if (!_defs.ContainsKey(id ?? string.Empty) || IsCompleted(id) || ActiveTutorialId != null) return false;
            ActiveTutorialId = id;
            ActiveStepIndex = 0;
            _analytics.Log(AnalyticsEvents.TutorialStarted, "tutorial_id", id);
            Publish();
            return true;
        }

        /// <summary>Advances only if <paramref name="expectedStepId"/> is the current step (guards against stale UI callbacks).</summary>
        public bool Advance(string expectedStepId)
        {
            if (ActiveTutorialId == null || ActiveStepId != expectedStepId) return false;
            var def = _defs[ActiveTutorialId];
            if (ActiveStepIndex + 1 >= def.Steps.Count) Finish(false);
            else { ActiveStepIndex++; Publish(); }
            return true;
        }

        public bool Skip()
        {
            if (ActiveTutorialId == null || !_defs[ActiveTutorialId].Skippable) return false;
            Finish(true);
            return true;
        }

        private void Finish(bool skipped)
        {
            var id = ActiveTutorialId;
            _state.Completed.Add(id);
            _slot.Save(_state);
            _analytics.Log(AnalyticsEvents.TutorialCompleted, new Dictionary<string, object> { { "tutorial_id", id }, { "skipped", skipped } });
            ActiveTutorialId = null;
            ActiveStepIndex = -1;
            _events?.Publish(new TutorialStepChanged(id, null, -1));
        }

        private void Publish() => _events?.Publish(new TutorialStepChanged(ActiveTutorialId, ActiveStepId, ActiveStepIndex));
    }
}
