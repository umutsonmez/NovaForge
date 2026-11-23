using System;
using System.Collections.Generic;

namespace NovaForge.Core.Foundation
{
    public interface IEventBus
    {
        IDisposable Subscribe<T>(Action<T> handler);
        void Publish<T>(T evt);
    }

    /// <summary>
    /// Typed synchronous publish/subscribe bus for decoupled module communication.
    /// - Handlers may subscribe/unsubscribe during dispatch (dispatch iterates a snapshot).
    /// - A throwing handler is reported to the error handler and does NOT stop other handlers.
    /// Main-thread only.
    /// </summary>
    public sealed class EventBus : IEventBus
    {
        private readonly Dictionary<Type, List<Delegate>> _handlers = new Dictionary<Type, List<Delegate>>();
        private readonly IErrorHandler _errors;

        public EventBus(IErrorHandler errors)
        {
            _errors = errors ?? throw new ArgumentNullException(nameof(errors));
        }

        public IDisposable Subscribe<T>(Action<T> handler)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            if (!_handlers.TryGetValue(typeof(T), out var list))
            {
                list = new List<Delegate>();
                _handlers[typeof(T)] = list;
            }
            list.Add(handler);
            return new Subscription(() => list.Remove(handler));
        }

        public void Publish<T>(T evt)
        {
            if (!_handlers.TryGetValue(typeof(T), out var list) || list.Count == 0) return;
            var snapshot = list.ToArray();
            for (int i = 0; i < snapshot.Length; i++)
            {
                try
                {
                    ((Action<T>)snapshot[i])(evt);
                }
                catch (Exception ex)
                {
                    _errors.Report(ex, $"EventBus handler for {typeof(T).Name}");
                }
            }
        }

        private sealed class Subscription : IDisposable
        {
            private Action _dispose;
            public Subscription(Action dispose) { _dispose = dispose; }
            public void Dispose()
            {
                _dispose?.Invoke();
                _dispose = null;
            }
        }
    }

    /// <summary>Collects subscriptions so a module can release them all at once.</summary>
    public sealed class SubscriptionBag : IDisposable
    {
        private readonly List<IDisposable> _items = new List<IDisposable>();
        public void Add(IDisposable d) => _items.Add(d);
        public void Dispose()
        {
            for (int i = _items.Count - 1; i >= 0; i--) _items[i].Dispose();
            _items.Clear();
        }
    }
}
