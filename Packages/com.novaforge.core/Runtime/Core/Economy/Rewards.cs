using System;
using System.Collections.Generic;
using NovaForge.Core.Foundation;

namespace NovaForge.Core.Economy
{
    /// <summary>A set of resources granted together (level reward, daily reward, IAP payout...).</summary>
    public sealed class RewardBundle
    {
        private readonly List<ResourceAmount> _items = new List<ResourceAmount>();
        public IReadOnlyList<ResourceAmount> Items => _items;
        public bool IsEmpty => _items.Count == 0;

        public RewardBundle Add(string resourceId, long amount)
        {
            if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));
            _items.Add(new ResourceAmount(Guard.Id(resourceId, nameof(resourceId)), amount));
            return this;
        }

        public static RewardBundle Of(string resourceId, long amount) => new RewardBundle().Add(resourceId, amount);

        public RewardBundle Scaled(double multiplier)
        {
            if (multiplier <= 0) throw new ArgumentOutOfRangeException(nameof(multiplier));
            var b = new RewardBundle();
            foreach (var i in _items) b.Add(i.ResourceId, Math.Max(1, (long)Math.Round(i.Amount * multiplier)));
            return b;
        }

        public override string ToString() => string.Join(", ", _items);
    }

    public readonly struct RewardGranted
    {
        public readonly string GrantId;
        public readonly string Source;
        public readonly RewardBundle Bundle;
        public RewardGranted(string grantId, string source, RewardBundle bundle) { GrantId = grantId; Source = source; Bundle = bundle; }
    }

    public interface IRewardService
    {
        /// <summary>
        /// Grants a bundle exactly once per grantId. Repeated calls with the same id return Duplicate and change nothing.
        /// Grant ids should be deterministic for the event they reward, e.g. "level_complete:12:first".
        /// </summary>
        TransactionStatus Grant(string grantId, string source, RewardBundle bundle);
    }

    public sealed class RewardService : IRewardService
    {
        private readonly IWallet _wallet;
        private readonly IEventBus _events;

        public RewardService(IWallet wallet, IEventBus events)
        {
            _wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));
            _events = events;
        }

        public TransactionStatus Grant(string grantId, string source, RewardBundle bundle)
        {
            if (bundle == null || bundle.IsEmpty) return TransactionStatus.InvalidAmount;
            var tx = new EconomyTransaction("reward:" + grantId, source);
            foreach (var i in bundle.Items) tx.Credit(i.ResourceId, i.Amount);
            var status = _wallet.Apply(tx);
            if (status == TransactionStatus.Success) _events?.Publish(new RewardGranted(grantId, source, bundle));
            return status;
        }
    }
}
