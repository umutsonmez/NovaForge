using System;
using System.Collections.Generic;

namespace NovaForge.Core.Foundation
{
    public interface IState
    {
        void Enter();
        void Exit();
    }

    /// <summary>Optional per-frame behaviour for a state.</summary>
    public interface ITickableState
    {
        void Tick(float deltaTime);
    }

    public readonly struct StateChanged<TStateId>
    {
        public readonly TStateId From;
        public readonly TStateId To;
        public StateChanged(TStateId from, TStateId to) { From = from; To = to; }
    }

    /// <summary>
    /// Finite state machine with an explicit transition whitelist.
    /// Illegal transitions are rejected (returns false + warning) instead of silently corrupting flow.
    /// Transitions requested from inside Enter/Exit are queued and run after the current one completes.
    /// </summary>
    public sealed class StateMachine<TStateId> where TStateId : struct, Enum
    {
        private readonly Dictionary<TStateId, IState> _states = new Dictionary<TStateId, IState>();
        private readonly Dictionary<TStateId, HashSet<TStateId>> _allowed = new Dictionary<TStateId, HashSet<TStateId>>();
        private readonly Queue<TStateId> _pending = new Queue<TStateId>();
        private readonly ILog _log;
        private readonly IEventBus _events;
        private bool _transitioning;

        public bool HasState { get; private set; }
        public TStateId Current { get; private set; }

        public StateMachine(ILog log, IEventBus events = null)
        {
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _events = events;
        }

        public StateMachine<TStateId> AddState(TStateId id, IState state)
        {
            _states[id] = state ?? throw new ArgumentNullException(nameof(state));
            return this;
        }

        public StateMachine<TStateId> AllowTransition(TStateId from, params TStateId[] to)
        {
            if (!_allowed.TryGetValue(from, out var set))
            {
                set = new HashSet<TStateId>();
                _allowed[from] = set;
            }
            foreach (var t in to) set.Add(t);
            return this;
        }

        public bool CanTransition(TStateId to)
        {
            if (!_states.ContainsKey(to)) return false;
            if (!HasState) return true;
            return _allowed.TryGetValue(Current, out var set) && set.Contains(to);
        }

        /// <summary>Requests a transition. Returns false if rejected.</summary>
        public bool TransitionTo(TStateId to)
        {
            if (_transitioning)
            {
                _pending.Enqueue(to);
                return true;
            }

            if (!CanTransition(to))
            {
                _log.Warning("StateMachine", $"Rejected transition {(HasState ? Current.ToString() : "<none>")} -> {to}");
                return false;
            }

            _transitioning = true;
            try
            {
                var from = Current;
                bool hadState = HasState;
                if (hadState) _states[from].Exit();
                Current = to;
                HasState = true;
                _states[to].Enter();
                _log.Debug("StateMachine", $"{(hadState ? from.ToString() : "<none>")} -> {to}");
                _events?.Publish(new StateChanged<TStateId>(from, to));
            }
            finally
            {
                _transitioning = false;
            }

            while (_pending.Count > 0)
            {
                if (!TransitionTo(_pending.Dequeue())) _pending.Clear();
            }
            return true;
        }

        public void Tick(float deltaTime)
        {
            if (HasState && _states[Current] is ITickableState tickable) tickable.Tick(deltaTime);
        }
    }

    /// <summary>Convenience state built from delegates.</summary>
    public sealed class DelegateState : IState
    {
        private readonly Action _enter;
        private readonly Action _exit;
        public DelegateState(Action enter = null, Action exit = null) { _enter = enter; _exit = exit; }
        public void Enter() => _enter?.Invoke();
        public void Exit() => _exit?.Invoke();
    }
}
