using System;

namespace NovaForge.Core.Foundation
{
    /// <summary>Top-level application flow states shared by every game.</summary>
    public enum GameFlowState
    {
        Boot,
        MainMenu,
        Loading,
        Playing,
        Paused,
        LevelEnd,
    }

    public readonly struct AppPaused { }
    public readonly struct AppResumed
    {
        public readonly TimeSpan TimeAway;
        public AppResumed(TimeSpan timeAway) { TimeAway = timeAway; }
    }
    public readonly struct AppQuitting { }

    /// <summary>
    /// Translates platform lifecycle callbacks (Unity OnApplicationPause / OnApplicationQuit) into bus events.
    /// On mobile, pause is the last reliable moment to persist: the OS may kill a backgrounded app without quit.
    /// </summary>
    public sealed class GameLifecycle
    {
        private readonly IEventBus _events;
        private readonly IClock _clock;
        private DateTime? _pausedAt;

        public bool IsPaused => _pausedAt.HasValue;

        public GameLifecycle(IEventBus events, IClock clock)
        {
            _events = events ?? throw new ArgumentNullException(nameof(events));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        }

        public void HandlePause(bool paused)
        {
            if (paused)
            {
                if (_pausedAt.HasValue) return;
                _pausedAt = _clock.UtcNow;
                _events.Publish(new AppPaused());
            }
            else
            {
                if (!_pausedAt.HasValue) return;
                var away = _clock.UtcNow - _pausedAt.Value;
                _pausedAt = null;
                _events.Publish(new AppResumed(away < TimeSpan.Zero ? TimeSpan.Zero : away));
            }
        }

        public void HandleQuit() => _events.Publish(new AppQuitting());

        /// <summary>Standard transition table for <see cref="GameFlowState"/>.</summary>
        public static StateMachine<GameFlowState> CreateFlow(ILog log, IEventBus events)
        {
            return new StateMachine<GameFlowState>(log, events)
                .AllowTransition(GameFlowState.Boot, GameFlowState.MainMenu, GameFlowState.Loading)
                .AllowTransition(GameFlowState.MainMenu, GameFlowState.Loading)
                .AllowTransition(GameFlowState.Loading, GameFlowState.Playing, GameFlowState.MainMenu)
                .AllowTransition(GameFlowState.Playing, GameFlowState.Paused, GameFlowState.LevelEnd, GameFlowState.Loading, GameFlowState.MainMenu)
                .AllowTransition(GameFlowState.Paused, GameFlowState.Playing, GameFlowState.Loading, GameFlowState.MainMenu)
                .AllowTransition(GameFlowState.LevelEnd, GameFlowState.Loading, GameFlowState.MainMenu, GameFlowState.Playing);
        }
    }
}
