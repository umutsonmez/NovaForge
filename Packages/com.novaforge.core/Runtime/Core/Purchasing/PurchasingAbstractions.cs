using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NovaForge.Core.Economy;
using NovaForge.Core.Foundation;

namespace NovaForge.Core.Purchasing
{
    public enum StoreProductType { Consumable, NonConsumable, Subscription }

    /// <summary>
    /// Game-side definition of a real-money product. Prices are NOT defined here: they come from the
    /// platform store catalog (Google Play Console / App Store Connect) via the provider at runtime.
    /// </summary>
    public sealed class ProductDefinition
    {
        public string Id { get; }
        public StoreProductType Type { get; }
        /// <summary>Platform -> store SKU, e.g. "google" -> "com.studio.game.coins_small". Empty = same as Id.</summary>
        public IReadOnlyDictionary<string, string> StoreIds { get; }
        /// <summary>Resources delivered on purchase (consumables) or once on first purchase (others). May be empty.</summary>
        public RewardBundle Payout { get; }
        /// <summary>Entitlement granted (non-consumable / subscription). Null for pure consumables.</summary>
        public string EntitlementId { get; }

        public ProductDefinition(string id, StoreProductType type, RewardBundle payout = null, string entitlementId = null,
            IReadOnlyDictionary<string, string> storeIds = null)
        {
            Id = Guard.Id(id, nameof(id));
            Type = type;
            Payout = payout ?? new RewardBundle();
            EntitlementId = entitlementId == null ? null : Guard.Id(entitlementId, nameof(entitlementId));
            StoreIds = storeIds ?? new Dictionary<string, string>();
            if (type == StoreProductType.Consumable && EntitlementId != null)
                throw new ArgumentException($"Consumable '{id}' cannot grant an entitlement.");
            if (type != StoreProductType.Consumable && EntitlementId == null && Payout.IsEmpty)
                throw new ArgumentException($"Product '{id}' delivers nothing.");
            if (type == StoreProductType.Consumable && Payout.IsEmpty)
                throw new ArgumentException($"Consumable '{id}' has no payout.");
        }
    }

    /// <summary>Localized product info as reported by the store. Display <see cref="LocalizedPrice"/> verbatim.</summary>
    public sealed class StoreProductInfo
    {
        public string ProductId;
        public string LocalizedPrice;
        public decimal Price;
        public string IsoCurrencyCode;
        public string LocalizedTitle;
        public string LocalizedDescription;
        public bool Available;
    }

    /// <summary>A transaction delivered by the store that still needs validation + fulfillment + finish.</summary>
    public sealed class StorePurchase
    {
        public string ProductId;
        public string TransactionId;
        /// <summary>Opaque platform receipt / purchase token. Sent to the validation server untouched.</summary>
        public string Receipt;
        public string Platform;
        public bool IsRestore;
    }

    public enum PurchaseFailureReason
    {
        UserCancelled,
        PaymentDeclined,
        ProductUnavailable,
        StoreUnavailable,
        ExistingPurchasePending,
        DuplicateTransaction,
        ValidationFailed,
        NotInitialized,
        AlreadyOwned,
        Unknown,
    }

    /// <summary>
    /// Adapter for a store SDK (Unity In-App Purchasing → Google Play Billing / Apple StoreKit).
    /// Contract:
    ///  - Every purchase (new, restored, or unfinished from a previous run) is delivered through <see cref="PurchaseReceived"/>.
    ///  - The adapter must NOT confirm/finish/consume a transaction on its own; it waits for <see cref="FinishTransaction"/>.
    ///    Unfinished transactions are re-delivered by the store, which is what makes crash recovery work.
    ///  - Deferred payments (Ask to Buy, pending cash payments) are reported via <see cref="PurchaseDeferred"/>.
    /// </summary>
    public interface IPurchaseProvider
    {
        string Name { get; }
        string Platform { get; }
        Task<bool> InitializeAsync(IReadOnlyList<ProductDefinition> catalog);
        IReadOnlyList<StoreProductInfo> Products { get; }
        void Purchase(string productId);
        Task<bool> RestoreAsync();
        void FinishTransaction(StorePurchase purchase);

        event Action<StorePurchase> PurchaseReceived;
        event Action<string> PurchaseDeferred;
        event Action<string, PurchaseFailureReason, string> PurchaseFailed;
    }

    public enum ValidationStatus
    {
        Valid,
        /// <summary>Definitively invalid (forged, refunded, wrong app). Do not fulfill.</summary>
        Invalid,
        /// <summary>Validation backend unreachable. Keep the transaction unfinished and retry later.</summary>
        RetryLater,
    }

    public sealed class ValidationResult
    {
        public ValidationStatus Status;
        public string Reason;
        /// <summary>Product id confirmed by the validator; must match the requested product.</summary>
        public string ValidatedProductId;
        /// <summary>Subscription expiry from the store server; null for non-subscriptions.</summary>
        public DateTime? ExpiresUtc;

        public static ValidationResult Valid(string productId, DateTime? expires = null) => new ValidationResult { Status = ValidationStatus.Valid, ValidatedProductId = productId, ExpiresUtc = expires };
        public static ValidationResult Invalid(string reason) => new ValidationResult { Status = ValidationStatus.Invalid, Reason = reason };
        public static ValidationResult Retry(string reason) => new ValidationResult { Status = ValidationStatus.RetryLater, Reason = reason };
    }

    /// <summary>
    /// Validates a store transaction before anything is delivered.
    /// Production: <see cref="ServerPurchaseValidator"/> (your backend verifies with Google Play Developer API /
    /// App Store Server API). Client-only validation can be forged on rooted/jailbroken devices.
    /// </summary>
    public interface IPurchaseValidator
    {
        bool IsSecureForProduction { get; }
        Task<ValidationResult> ValidateAsync(StorePurchase purchase);
    }
}
