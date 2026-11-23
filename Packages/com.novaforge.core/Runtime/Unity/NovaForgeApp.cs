// STATUS: compiles in Unity 6000.3.29f1 (verified 2026-11-23). Runtime behaviour not yet covered by PlayMode tests.
using System;
using NovaForge.Core;
using UnityEngine;

namespace NovaForge.Unity
{
    /// <summary>
    /// Unity entry point. Lives in the boot scene, survives scene loads, owns the NovaForgeCore instance and
    /// forwards platform lifecycle callbacks. Each game subclasses it and fills <see cref="GameSetup"/>.
    /// </summary>
    [DefaultExecutionOrder(-10000)]
    public abstract class NovaForgeApp : MonoBehaviour
    {
        public static NovaForgeCore Core { get; private set; }
        public static event Action<NovaForgeCore> Ready;
        private static NovaForgeApp _owner;

        [SerializeField] private int targetFrameRate = 60;

        /// <summary>Supports "Enter Play Mode Options" with domain reload disabled.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Core = null;
            Ready = null;
            _owner = null;
        }

        /// <summary>Game-specific composition: economy, levels, products, providers...</summary>
        protected abstract GameSetup CreateSetup();

        /// <summary>
        /// Called right after Core is built, before InitializeAsync. Wire things that must exist before SDKs start,
        /// e.g. AdMobConsentBridge.Connect(provider, core.Consent).
        /// </summary>
        protected virtual void OnCoreBuilt(NovaForgeCore core) { }

        /// <summary>Called after Core.InitializeAsync finished (successfully or not). Load the main menu here.</summary>
        protected abstract void OnCoreReady(NovaForgeCore core);

        protected virtual void Awake()
        {
            if (Core != null)
            {
                // Boot scene re-entered: keep the existing instance.
                Destroy(gameObject);
                return;
            }
            _owner = this;
            DontDestroyOnLoad(gameObject);
            Application.targetFrameRate = targetFrameRate;
            Screen.sleepTimeout = SleepTimeout.SystemSetting;

            var setup = CreateSetup();
            setup.IsReleaseBuild = !Debug.isDebugBuild;
            setup.LogSinks.Add(new UnityLogSink());
            if (setup.Storage == null) setup.Storage = UnityStorage.ForGame(setup.GameId);
            Core = NovaForgeCore.Build(setup);
            OnCoreBuilt(Core);
        }

        protected virtual async void Start()
        {
            if (!ReferenceEquals(this, _owner)) return;
            try
            {
                await Core.InitializeAsync();
            }
            catch (Exception ex)
            {
                Core.Errors.Report(ex, "NovaForgeApp.Start");
            }
            OnCoreReady(Core);
            Ready?.Invoke(Core);
        }

        private void OnApplicationPause(bool paused)
        {
            if (ReferenceEquals(this, _owner)) Core?.Lifecycle.HandlePause(paused);
        }

        private void OnApplicationQuit()
        {
            if (ReferenceEquals(this, _owner)) Core?.Lifecycle.HandleQuit();
        }

        protected virtual void OnDestroy()
        {
            // Only the instance that built Core may dispose it (duplicates from re-entering the boot scene must not).
            if (!ReferenceEquals(this, _owner)) return;
            _owner = null;
            Core?.Dispose();
            Core = null;
        }
    }
}
