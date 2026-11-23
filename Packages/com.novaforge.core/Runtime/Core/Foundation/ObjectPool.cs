using System;
using System.Collections.Generic;

namespace NovaForge.Core.Foundation
{
    /// <summary>
    /// Allocation-free reuse of objects (bullets, particles, tiles) to avoid GC spikes on low-end Android.
    /// Engine-agnostic: the Unity layer wraps it for GameObjects (activate/deactivate in onGet/onRelease).
    /// </summary>
    public sealed class ObjectPool<T> where T : class
    {
        private readonly Stack<T> _free;
        private readonly HashSet<T> _inUse = new HashSet<T>();
        private readonly Func<T> _create;
        private readonly Action<T> _onGet;
        private readonly Action<T> _onRelease;
        private readonly Action<T> _onDestroy;
        private readonly int _maxSize;

        public ObjectPool(Func<T> create, Action<T> onGet = null, Action<T> onRelease = null, Action<T> onDestroy = null, int prewarm = 0, int maxSize = 256)
        {
            _create = create ?? throw new ArgumentNullException(nameof(create));
            _onGet = onGet;
            _onRelease = onRelease;
            _onDestroy = onDestroy;
            _maxSize = Math.Max(1, maxSize);
            _free = new Stack<T>(Math.Max(prewarm, 4));
            for (int i = 0; i < prewarm && i < _maxSize; i++)
            {
                var item = _create();
                _onRelease?.Invoke(item);
                _free.Push(item);
            }
        }

        public int CountFree => _free.Count;
        public int CountInUse => _inUse.Count;

        public T Get()
        {
            var item = _free.Count > 0 ? _free.Pop() : _create();
            _inUse.Add(item);
            _onGet?.Invoke(item);
            return item;
        }

        public void Release(T item)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));
            if (!_inUse.Remove(item)) throw new InvalidOperationException("Releasing an object that is not in use (double release?).");
            _onRelease?.Invoke(item);
            if (_free.Count < _maxSize) _free.Push(item);
            else _onDestroy?.Invoke(item);
        }

        public void Clear()
        {
            while (_free.Count > 0) _onDestroy?.Invoke(_free.Pop());
        }
    }
}
