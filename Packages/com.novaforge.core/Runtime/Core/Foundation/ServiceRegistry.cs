using System;
using System.Collections.Generic;

namespace NovaForge.Core.Foundation
{
    /// <summary>Read-only view used by consumers to obtain services.</summary>
    public interface IServiceResolver
    {
        T Resolve<T>() where T : class;
        bool TryResolve<T>(out T service) where T : class;
        bool IsRegistered<T>() where T : class;
    }

    /// <summary>
    /// Minimal, explicit dependency-injection container.
    /// Registration is factory-based (no reflection auto-wiring) so it is IL2CPP / code-stripping safe
    /// and every dependency edge is visible in the composition root.
    /// Singletons are created lazily on first resolve; circular dependencies are detected and reported.
    /// Not thread-safe: compose and resolve on the main thread.
    /// </summary>
    public sealed class ServiceRegistry : IServiceResolver, IDisposable
    {
        private readonly Dictionary<Type, Func<IServiceResolver, object>> _factories = new Dictionary<Type, Func<IServiceResolver, object>>();
        private readonly Dictionary<Type, object> _instances = new Dictionary<Type, object>();
        private readonly HashSet<Type> _resolving = new HashSet<Type>();
        private readonly List<IDisposable> _ownedDisposables = new List<IDisposable>();
        private bool _disposed;

        /// <summary>Registers an already-created instance. The registry does NOT dispose external instances.</summary>
        public ServiceRegistry RegisterInstance<T>(T instance) where T : class
        {
            if (instance == null) throw new ArgumentNullException(nameof(instance));
            EnsureNotRegistered(typeof(T));
            _instances[typeof(T)] = instance;
            return this;
        }

        /// <summary>Registers a lazily-created singleton. Instances created by the registry are disposed with it.</summary>
        public ServiceRegistry RegisterSingleton<T>(Func<IServiceResolver, T> factory) where T : class
        {
            if (factory == null) throw new ArgumentNullException(nameof(factory));
            EnsureNotRegistered(typeof(T));
            _factories[typeof(T)] = r => factory(r);
            return this;
        }

        /// <summary>Exposes an existing registration under an additional interface (e.g. IWallet -> EconomyService).</summary>
        public ServiceRegistry RegisterAlias<TAlias, TService>() where TService : class, TAlias where TAlias : class
        {
            EnsureNotRegistered(typeof(TAlias));
            _factories[typeof(TAlias)] = r => r.Resolve<TService>();
            return this;
        }

        public bool IsRegistered<T>() where T : class
        {
            return _instances.ContainsKey(typeof(T)) || _factories.ContainsKey(typeof(T));
        }

        public T Resolve<T>() where T : class
        {
            if (TryResolve(out T service)) return service;
            throw new InvalidOperationException($"Service '{typeof(T).FullName}' is not registered.");
        }

        public bool TryResolve<T>(out T service) where T : class
        {
            ThrowIfDisposed();
            var type = typeof(T);
            if (_instances.TryGetValue(type, out var existing))
            {
                service = (T)existing;
                return true;
            }

            if (!_factories.TryGetValue(type, out var factory))
            {
                service = null;
                return false;
            }

            if (!_resolving.Add(type))
                throw new InvalidOperationException($"Circular dependency detected while resolving '{type.FullName}'.");

            try
            {
                var created = factory(this) ?? throw new InvalidOperationException($"Factory for '{type.FullName}' returned null.");
                _instances[type] = created;
                // Aliases resolve to another registration that owns the instance; don't double-track.
                if (created is IDisposable disposable && !_ownedDisposables.Contains(disposable))
                    _ownedDisposables.Add(disposable);
                service = (T)created;
                return true;
            }
            finally
            {
                _resolving.Remove(type);
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            // Reverse creation order: dependents are disposed before their dependencies.
            for (int i = _ownedDisposables.Count - 1; i >= 0; i--)
                _ownedDisposables[i].Dispose();
            _ownedDisposables.Clear();
            _instances.Clear();
            _factories.Clear();
        }

        private void EnsureNotRegistered(Type type)
        {
            ThrowIfDisposed();
            if (_instances.ContainsKey(type) || _factories.ContainsKey(type))
                throw new InvalidOperationException($"Service '{type.FullName}' is already registered.");
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(ServiceRegistry));
        }
    }
}
