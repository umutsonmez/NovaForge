using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NovaForge.Core.Foundation;

namespace NovaForge.Core.UI
{
    /// <summary>A screen or popup. Implemented by the Unity layer (UIView) or by test doubles.</summary>
    public interface IView
    {
        string Id { get; }
        /// <summary>Plays the enter transition. Completes when the view is fully visible and interactive.</summary>
        Task ShowAsync(object args);
        /// <summary>Plays the exit transition. Completes when the view is fully hidden.</summary>
        Task HideAsync();
        /// <summary>Lets the view consume the back action itself (e.g. close an inner panel). Return true if handled.</summary>
        bool HandleBack();
    }

    /// <summary>Resolves (and may lazily create) views by id.</summary>
    public interface IViewProvider
    {
        IView Get(string id);
    }

    public readonly struct ScreenChanged
    {
        public readonly string From;
        public readonly string To;
        public ScreenChanged(string from, string to) { From = from; To = to; }
    }

    public readonly struct PopupChanged
    {
        public readonly string Id;
        public readonly bool Opened;
        public PopupChanged(string id, bool opened) { Id = id; Opened = opened; }
    }

    /// <summary>Back pressed on the root screen with nothing to close: the game should confirm quitting.</summary>
    public readonly struct ExitRequested { }

    /// <summary>
    /// Screen stack + popup stack + queued popups, with Android-style back handling.
    ///
    /// - All operations are serialized: a navigation request issued during a transition runs after it
    ///   (no double-tap glitches, no overlapping transitions).
    /// - Screens: Push keeps the previous screen in history; Replace drops it; ResetTo clears everything.
    /// - Popups: ShowPopup opens immediately on top; EnqueuePopup waits until no popup is open
    ///   (daily reward, achievement toasts) and is ordered by priority, then FIFO.
    /// - Back: top popup (if dismissible) → current screen's own HandleBack → pop screen → ExitRequested.
    /// Engine-independent: views are abstract, so the whole policy is unit tested.
    /// </summary>
    public sealed class NavigationService
    {
        private const string Tag = "Navigation";

        private sealed class PopupEntry
        {
            public string Id;
            public object Args;
            public int Priority;
            public bool Dismissible;
            public long Order;
        }

        private readonly IViewProvider _views;
        private readonly IEventBus _events;
        private readonly ILog _log;
        private readonly List<string> _screens = new List<string>();
        private readonly List<PopupEntry> _popups = new List<PopupEntry>();
        private readonly List<PopupEntry> _queued = new List<PopupEntry>();
        private Task _tail = Task.CompletedTask;
        private int _pending;
        private long _order;

        public NavigationService(IViewProvider views, IEventBus events, ILog log)
        {
            _views = views ?? throw new ArgumentNullException(nameof(views));
            _events = events;
            _log = log ?? throw new ArgumentNullException(nameof(log));
        }

        public string CurrentScreen => _screens.Count > 0 ? _screens[_screens.Count - 1] : null;
        public IReadOnlyList<string> ScreenHistory => _screens;
        public string TopPopup => _popups.Count > 0 ? _popups[_popups.Count - 1].Id : null;
        public int OpenPopupCount => _popups.Count;
        public int QueuedPopupCount => _queued.Count;
        /// <summary>True while a transition runs or is queued. UI can use it to ignore input.</summary>
        public bool IsBusy => _pending > 0;

        public bool IsPopupOpen(string id) => _popups.Exists(p => p.Id == id);

        // ------------------------------------------------------------------ screens

        public Task PushScreenAsync(string id, object args = null) => Run(async () =>
        {
            var from = CurrentScreen;
            var next = _views.Get(id);
            await CloseAllPopupsInternal();
            if (from != null) await _views.Get(from).HideAsync();
            _screens.Add(id);
            await next.ShowAsync(args);
            _events?.Publish(new ScreenChanged(from, id));
            await ShowNextQueuedInternal();
        });

        public Task ReplaceScreenAsync(string id, object args = null) => Run(async () =>
        {
            var from = CurrentScreen;
            var next = _views.Get(id);
            await CloseAllPopupsInternal();
            if (from != null)
            {
                await _views.Get(from).HideAsync();
                _screens.RemoveAt(_screens.Count - 1);
            }
            _screens.Add(id);
            await next.ShowAsync(args);
            _events?.Publish(new ScreenChanged(from, id));
            await ShowNextQueuedInternal();
        });

        /// <summary>Clears history and open popups and shows <paramref name="id"/> as the only screen. Queued popups are kept.</summary>
        public Task ResetToAsync(string id, object args = null) => Run(async () =>
        {
            var from = CurrentScreen;
            var next = _views.Get(id);
            await CloseAllPopupsInternal();
            if (from != null) await _views.Get(from).HideAsync();
            _screens.Clear();
            _screens.Add(id);
            await next.ShowAsync(args);
            _events?.Publish(new ScreenChanged(from, id));
            await ShowNextQueuedInternal();
        });

        /// <returns>false when there is no previous screen.</returns>
        public Task<bool> PopScreenAsync() => Run(PopScreenInternal);

        private async Task<bool> PopScreenInternal()
        {
            if (_screens.Count < 2) return false;
            var from = CurrentScreen;
            await CloseAllPopupsInternal();
            await _views.Get(from).HideAsync();
            _screens.RemoveAt(_screens.Count - 1);
            var to = CurrentScreen;
            await _views.Get(to).ShowAsync(null);
            _events?.Publish(new ScreenChanged(from, to));
            await ShowNextQueuedInternal();
            return true;
        }

        // ------------------------------------------------------------------ popups

        /// <summary>Opens a popup on top of everything immediately (after any running transition).</summary>
        public Task ShowPopupAsync(string id, object args = null, bool dismissible = true) => Run(async () =>
        {
            if (IsPopupOpen(id))
            {
                _log.Warning(Tag, $"Popup '{id}' already open; ignored.");
                return;
            }
            await OpenPopupInternal(new PopupEntry { Id = id, Args = args, Dismissible = dismissible, Order = ++_order });
        });

        /// <summary>
        /// Shows the popup when no other popup is open. Higher priority first; equal priority in request order.
        /// Use for non-urgent popups (daily reward, achievement, offers) so they never stack on each other.
        /// </summary>
        public Task EnqueuePopupAsync(string id, object args = null, int priority = 0, bool dismissible = true) => Run(async () =>
        {
            _queued.Add(new PopupEntry { Id = id, Args = args, Priority = priority, Dismissible = dismissible, Order = ++_order });
            await ShowNextQueuedInternal();
        });

        /// <summary>Closes the given popup (or the top one when id is null).</summary>
        public Task<bool> ClosePopupAsync(string id = null) => Run(async () =>
        {
            var entry = id == null ? (_popups.Count > 0 ? _popups[_popups.Count - 1] : null) : _popups.Find(p => p.Id == id);
            if (entry == null) return false;
            await ClosePopupInternal(entry);
            await ShowNextQueuedInternal();
            return true;
        });

        // ------------------------------------------------------------------ back

        /// <summary>
        /// Android back button / Escape. Returns true when something consumed it.
        /// Ignored (returns true) while a transition is running, so rapid presses can't skip screens.
        /// </summary>
        public Task<bool> HandleBackAsync()
        {
            if (IsBusy) return Task.FromResult(true);
            return Run(async () =>
            {
                if (_popups.Count > 0)
                {
                    var top = _popups[_popups.Count - 1];
                    if (_views.Get(top.Id).HandleBack()) return true;
                    if (!top.Dismissible) return true; // modal: back does nothing
                    await ClosePopupInternal(top);
                    await ShowNextQueuedInternal();
                    return true;
                }

                var current = CurrentScreen;
                if (current != null && _views.Get(current).HandleBack()) return true;
                if (await PopScreenInternal()) return true;
                _events?.Publish(new ExitRequested());
                return false;
            });
        }

        // ------------------------------------------------------------------ internals

        private async Task OpenPopupInternal(PopupEntry entry)
        {
            var view = _views.Get(entry.Id);
            _popups.Add(entry);
            await view.ShowAsync(entry.Args);
            _events?.Publish(new PopupChanged(entry.Id, true));
        }

        private async Task ClosePopupInternal(PopupEntry entry)
        {
            _popups.Remove(entry);
            await _views.Get(entry.Id).HideAsync();
            _events?.Publish(new PopupChanged(entry.Id, false));
        }

        private async Task CloseAllPopupsInternal()
        {
            while (_popups.Count > 0) await ClosePopupInternal(_popups[_popups.Count - 1]);
        }

        private async Task ShowNextQueuedInternal()
        {
            if (_popups.Count > 0 || _queued.Count == 0) return;
            PopupEntry best = null;
            foreach (var q in _queued)
                if (best == null || q.Priority > best.Priority || (q.Priority == best.Priority && q.Order < best.Order)) best = q;
            _queued.Remove(best);
            await OpenPopupInternal(best);
        }

        private Task Run(Func<Task> op) => Run(async () => { await op(); return true; });

        /// <summary>Chains operations so they never overlap. Exceptions surface to the caller; the chain keeps going.</summary>
        private Task<T> Run<T>(Func<Task<T>> op)
        {
            _pending++;
            var previous = _tail;
            var task = RunAfter(previous, op);
            _tail = task;
            return task;
        }

        private async Task<T> RunAfter<T>(Task previous, Func<Task<T>> op)
        {
            try
            {
                try { await previous; }
                catch (Exception) { /* already surfaced to that operation's caller */ }
                return await op();
            }
            finally
            {
                _pending--;
            }
        }
    }

    /// <summary>Standard view ids used by Core-provided screens. Games add their own ids freely.</summary>
    public static class ViewIds
    {
        public const string MainMenu = "main_menu";
        public const string Gameplay = "gameplay";
        public const string Shop = "shop";
        public const string Settings = "settings";
        public const string Pause = "pause";
        public const string Win = "win";
        public const string Lose = "lose";
        public const string LevelResult = "level_result";
        public const string Reward = "reward";
        public const string Confirm = "confirm";
        public const string DailyReward = "daily_reward";
    }
}
