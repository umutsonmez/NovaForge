using System;
using System.Collections.Generic;
using NovaForge.Core.Data;
using NovaForge.Core.Foundation;

namespace NovaForge.Core.Economy
{
    public static class StandardEntitlements
    {
        /// <summary>Disables interstitial, banner and app-open ads. Rewarded ads stay available (player-initiated).</summary>
        public const string NoAds = "no_ads";
        public const string Premium = "premium";
    }

    public sealed class EntitlementState : ISaveValidatable
    {
        /// <summary>id -> expiry unix ms (0 = permanent)</summary>
        public Dictionary<string, long> Entitlements = new Dictionary<string, long>();
        public string Validate() => Entitlements == null ? "null" : null;
    }

    public readonly struct EntitlementsChanged { }

    public interface IEntitlements
    {
        bool Has(string entitlementId);
        IEnumerable<string> Active { get; }
    }

    /// <summary>
    /// Durable ownership flags (no-ads, premium, unlocked modes, subscriptions with expiry).
    /// Granted by PurchaseService after validation, or by restore. Grants are idempotent.
    /// </summary>
    public sealed class EntitlementService : IEntitlements
    {
        public const int SaveVersion = 1;
        private readonly SaveSlot<EntitlementState> _slot;
        private readonly IClock _clock;
        private readonly IEventBus _events;
        private EntitlementState _state;

        public EntitlementService(SaveManager saves, IClock clock, IEventBus events)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _events = events;
            _slot = new SaveSlot<EntitlementState>(saves, "entitlements", SaveVersion, () => new EntitlementState());
            _state = _slot.Load().Data;
        }

        public bool Has(string entitlementId)
        {
            if (!_state.Entitlements.TryGetValue(entitlementId ?? string.Empty, out var expiry)) return false;
            return expiry == 0 || TimeUtil.ToUnixMs(_clock.UtcNow) < expiry;
        }

        public IEnumerable<string> Active
        {
            get
            {
                foreach (var kv in _state.Entitlements)
                    if (Has(kv.Key)) yield return kv.Key;
            }
        }

        /// <param name="expiresUtc">null = permanent (non-consumable). Subscriptions pass the validated expiry.</param>
        public void Grant(string entitlementId, DateTime? expiresUtc = null)
        {
            Guard.Id(entitlementId, nameof(entitlementId));
            long expiry = expiresUtc.HasValue ? TimeUtil.ToUnixMs(expiresUtc.Value) : 0;
            if (_state.Entitlements.TryGetValue(entitlementId, out var existing))
            {
                // Never downgrade a permanent grant; extend a subscription only forward.
                if (existing == 0 || (expiry != 0 && expiry <= existing)) return;
            }
            _state.Entitlements[entitlementId] = expiry;
            _slot.Save(_state);
            _events?.Publish(new EntitlementsChanged());
        }

        /// <summary>Used when the store reports a refund/revocation.</summary>
        public void Revoke(string entitlementId)
        {
            if (_state.Entitlements.Remove(entitlementId ?? string.Empty))
            {
                _slot.Save(_state);
                _events?.Publish(new EntitlementsChanged());
            }
        }
    }
}
