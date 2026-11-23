using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NovaForge.Core.Analytics;
using NovaForge.Core.Economy;
using NovaForge.Core.Foundation;
using NovaForge.Core.Progression;
using NovaForge.Core.Purchasing;

namespace NovaForge.Core.Shop
{
    public static class ShopCategories
    {
        public const string Coins = "coins";
        public const string Gems = "gems";
        public const string RemoveAds = "remove_ads";
        public const string StarterPacks = "starter_packs";
        public const string Cosmetics = "cosmetics";
        public const string SpecialOffers = "special_offers";
        public const string Hints = "hints";
        public const string Premium = "premium";
    }

    /// <summary>
    /// A shop entry. Either priced in soft currency (<see cref="SoftPrice"/>) or backed by a real-money store
    /// product (<see cref="IapProductId"/>; price shown is the store's localized price). Never both.
    /// </summary>
    public sealed class ShopItem
    {
        public string Id { get; }
        public string Category { get; }
        public string TitleKey { get; }
        public IReadOnlyList<ResourceAmount> SoftPrice { get; }
        public string IapProductId { get; }
        /// <summary>What a soft-currency purchase delivers (IAP payouts are defined on the ProductDefinition).</summary>
        public RewardBundle Payout { get; }
        /// <summary>0 = unlimited.</summary>
        public int MaxPurchases { get; }
        public int SortOrder { get; }

        private ShopItem(string id, string category, string titleKey, IReadOnlyList<ResourceAmount> softPrice, string iapProductId, RewardBundle payout, int maxPurchases, int sortOrder)
        {
            Id = Guard.Id(id, nameof(id));
            Category = Guard.Id(category, nameof(category));
            TitleKey = titleKey ?? "shop." + id;
            SoftPrice = softPrice;
            IapProductId = iapProductId;
            Payout = payout;
            MaxPurchases = Math.Max(0, maxPurchases);
            SortOrder = sortOrder;
        }

        public bool IsRealMoney => IapProductId != null;

        public static ShopItem ForSoftCurrency(string id, string category, IReadOnlyList<ResourceAmount> price, RewardBundle payout, int maxPurchases = 0, int sortOrder = 0, string titleKey = null)
        {
            if (price == null || price.Count == 0) throw new ArgumentException("Soft-currency item needs a price.");
            foreach (var p in price) if (p.Amount <= 0) throw new ArgumentException("Prices must be positive.");
            if (payout == null || payout.IsEmpty) throw new ArgumentException("Item delivers nothing.");
            return new ShopItem(id, category, titleKey, price, null, payout, maxPurchases, sortOrder);
        }

        public static ShopItem ForStoreProduct(string id, string category, string iapProductId, int sortOrder = 0, string titleKey = null)
            => new ShopItem(id, category, titleKey, null, Guard.Id(iapProductId, nameof(iapProductId)), null, 0, sortOrder);
    }

    public enum ShopPurchaseStatus { Success, InsufficientFunds, LimitReached, Unknown, AlreadyOwned, Pending, Cancelled, Failed, StoreUnavailable }

    /// <summary>
    /// Game-configurable shop. Soft purchases are a single atomic economy transaction (debit price + credit payout).
    /// Real-money items delegate to <see cref="PurchaseService"/> (validation, ledger, entitlements).
    /// </summary>
    public sealed class ShopService
    {
        private readonly Dictionary<string, ShopItem> _items = new Dictionary<string, ShopItem>();
        private readonly IWallet _wallet;
        private readonly PurchaseService _purchases;
        private readonly IAnalytics _analytics;
        private readonly IStatistics _stats;
        private readonly IClock _clock;
        private long _counter;

        /// <param name="purchases">null when the game has no IAP.</param>
        public ShopService(IEnumerable<ShopItem> items, IWallet wallet, PurchaseService purchases, IStatistics stats, IAnalytics analytics, IClock clock)
        {
            _wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));
            _purchases = purchases;
            _stats = stats ?? throw new ArgumentNullException(nameof(stats));
            _analytics = analytics ?? throw new ArgumentNullException(nameof(analytics));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            foreach (var i in items ?? throw new ArgumentNullException(nameof(items)))
            {
                if (_items.ContainsKey(i.Id)) throw new ArgumentException($"Duplicate shop item '{i.Id}'.");
                if (i.IsRealMoney && purchases == null) throw new ArgumentException($"Item '{i.Id}' needs IAP but no PurchaseService was provided.");
                _items[i.Id] = i;
            }
        }

        public void NotifyOpened(string entryPoint) => _analytics.Log(AnalyticsEvents.ShopOpened, "entry_point", entryPoint ?? "unknown");

        public IReadOnlyList<ShopItem> GetItems(string category = null)
        {
            var list = new List<ShopItem>();
            foreach (var i in _items.Values)
            {
                if (category != null && i.Category != category) continue;
                // Hide owned non-consumables (e.g. Remove Ads) and items whose store product is unavailable.
                if (i.IsRealMoney)
                {
                    if (_purchases.IsOwned(i.IapProductId)) continue;
                    var info = _purchases.GetProductInfo(i.IapProductId);
                    if (info == null || !info.Available) continue;
                }
                list.Add(i);
            }
            list.Sort((a, b) => a.SortOrder != b.SortOrder ? a.SortOrder.CompareTo(b.SortOrder) : string.CompareOrdinal(a.Id, b.Id));
            return list;
        }

        /// <summary>Localized display price: store string for IAP, null for soft items (UI renders the currency icon + amount).</summary>
        public string GetDisplayPrice(string itemId)
        {
            if (!_items.TryGetValue(itemId, out var item) || !item.IsRealMoney) return null;
            return _purchases.GetProductInfo(item.IapProductId)?.LocalizedPrice;
        }

        /// <summary>Lifetime soft-currency purchases of an item (persisted in player statistics).</summary>
        public long PurchaseCount(string itemId) => _stats.Get(PurchaseStatKey(itemId));

        private static string PurchaseStatKey(string itemId) => "shop_bought." + itemId;

        public ShopPurchaseStatus BuyWithSoftCurrency(string itemId)
        {
            if (!_items.TryGetValue(itemId ?? string.Empty, out var item) || item.IsRealMoney) return ShopPurchaseStatus.Unknown;
            if (item.MaxPurchases > 0 && PurchaseCount(itemId) >= item.MaxPurchases) return ShopPurchaseStatus.LimitReached;

            var tx = new EconomyTransaction($"shop:{itemId}:{TimeUtil.ToUnixMs(_clock.UtcNow)}:{++_counter}", "shop");
            foreach (var p in item.SoftPrice) tx.Debit(p.ResourceId, p.Amount);
            foreach (var r in item.Payout.Items) tx.Credit(r.ResourceId, r.Amount);
            var status = _wallet.Apply(tx);
            switch (status)
            {
                case TransactionStatus.Success:
                    _stats.Increment(PurchaseStatKey(itemId));
                    _analytics.Log(AnalyticsEvents.ItemPurchased, new Dictionary<string, object> { { "item_id", itemId }, { AnalyticsParams.Currency, item.SoftPrice[0].ResourceId }, { AnalyticsParams.Value, item.SoftPrice[0].Amount } });
                    return ShopPurchaseStatus.Success;
                case TransactionStatus.InsufficientFunds: return ShopPurchaseStatus.InsufficientFunds;
                default: return ShopPurchaseStatus.Failed;
            }
        }

        public async Task<ShopPurchaseStatus> BuyWithRealMoneyAsync(string itemId)
        {
            if (!_items.TryGetValue(itemId ?? string.Empty, out var item) || !item.IsRealMoney) return ShopPurchaseStatus.Unknown;
            var outcome = await _purchases.PurchaseAsync(item.IapProductId);
            switch (outcome.Status)
            {
                case PurchaseOutcomeStatus.Success: return ShopPurchaseStatus.Success;
                case PurchaseOutcomeStatus.Pending: return ShopPurchaseStatus.Pending;
            }
            switch (outcome.FailureReason)
            {
                case PurchaseFailureReason.UserCancelled: return ShopPurchaseStatus.Cancelled;
                case PurchaseFailureReason.AlreadyOwned: return ShopPurchaseStatus.AlreadyOwned;
                case PurchaseFailureReason.NotInitialized:
                case PurchaseFailureReason.StoreUnavailable: return ShopPurchaseStatus.StoreUnavailable;
                default: return ShopPurchaseStatus.Failed;
            }
        }
    }
}
