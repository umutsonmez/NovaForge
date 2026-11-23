using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NovaForge.Core.Analytics;
using NovaForge.Core.Data;
using NovaForge.Core.Economy;
using NovaForge.Core.Foundation;

namespace NovaForge.Core.Purchasing
{
    public enum PurchaseOutcomeStatus { Success, Pending, Failed }

    public sealed class PurchaseOutcome
    {
        public PurchaseOutcomeStatus Status;
        public PurchaseFailureReason FailureReason;
        public string ProductId;
        public string TransactionId;
        public string Message;

        public static PurchaseOutcome Ok(string productId, string txId) => new PurchaseOutcome { Status = PurchaseOutcomeStatus.Success, ProductId = productId, TransactionId = txId };
        public static PurchaseOutcome Deferred(string productId) => new PurchaseOutcome { Status = PurchaseOutcomeStatus.Pending, ProductId = productId };
        public static PurchaseOutcome Fail(string productId, PurchaseFailureReason reason, string msg = null) => new PurchaseOutcome { Status = PurchaseOutcomeStatus.Failed, ProductId = productId, FailureReason = reason, Message = msg };
    }

    public readonly struct PurchaseFulfilled
    {
        public readonly string ProductId;
        public readonly string TransactionId;
        public readonly bool IsRestore;
        public PurchaseFulfilled(string productId, string transactionId, bool isRestore) { ProductId = productId; TransactionId = transactionId; IsRestore = isRestore; }
    }

    public sealed class PurchaseLedgerState : ISaveValidatable
    {
        /// <summary>Every fulfilled transaction id, kept permanently (real money: volume is small, safety matters more).</summary>
        public List<string> FulfilledTransactions = new List<string>();
        /// <summary>Transactions awaiting validation retry: txId -> productId.</summary>
        public Dictionary<string, string> AwaitingValidation = new Dictionary<string, string>();
        public string Validate() => FulfilledTransactions == null || AwaitingValidation == null ? "null collection" : null;
    }

    /// <summary>
    /// Provider-independent IAP flow:
    ///   store delivers transaction → validate (server) → fulfill exactly once (persisted ledger keyed by transaction id)
    ///   → finish transaction at the store.
    /// Persisting BEFORE finishing means a crash in between causes a re-delivery that the ledger recognizes,
    /// so the player is neither double-paid nor loses a purchase.
    /// </summary>
    public sealed class PurchaseService : IDisposable
    {
        public const int SaveVersion = 1;
        private const string Tag = "IAP";
        private readonly IPurchaseProvider _provider;
        private readonly IPurchaseValidator _validator;
        private readonly IWallet _wallet;
        private readonly EntitlementService _entitlements;
        private readonly IAnalytics _analytics;
        private readonly ILog _log;
        private readonly IErrorHandler _errors;
        private readonly IEventBus _events;
        private readonly SaveSlot<PurchaseLedgerState> _slot;
        private readonly Dictionary<string, ProductDefinition> _catalog = new Dictionary<string, ProductDefinition>();
        private readonly Dictionary<string, TaskCompletionSource<PurchaseOutcome>> _inflight = new Dictionary<string, TaskCompletionSource<PurchaseOutcome>>();
        private readonly HashSet<string> _fulfilled;
        private readonly HashSet<string> _processing = new HashSet<string>();
        private PurchaseLedgerState _state;

        public bool IsInitialized { get; private set; }

        public PurchaseService(IPurchaseProvider provider, IPurchaseValidator validator, IEnumerable<ProductDefinition> catalog,
            IWallet wallet, EntitlementService entitlements, SaveManager saves, IAnalytics analytics, ILog log,
            IErrorHandler errors, IEventBus events)
        {
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
            _validator = validator ?? throw new ArgumentNullException(nameof(validator));
            _wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));
            _entitlements = entitlements ?? throw new ArgumentNullException(nameof(entitlements));
            _analytics = analytics ?? throw new ArgumentNullException(nameof(analytics));
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _errors = errors ?? throw new ArgumentNullException(nameof(errors));
            _events = events;
            foreach (var p in catalog ?? throw new ArgumentNullException(nameof(catalog)))
            {
                if (_catalog.ContainsKey(p.Id)) throw new ArgumentException($"Duplicate product '{p.Id}'.");
                _catalog[p.Id] = p;
            }
            _slot = new SaveSlot<PurchaseLedgerState>(saves, "purchases", SaveVersion, () => new PurchaseLedgerState());
            _state = _slot.Load().Data;
            _fulfilled = new HashSet<string>(_state.FulfilledTransactions);

            _provider.PurchaseReceived += OnPurchaseReceived;
            _provider.PurchaseDeferred += OnPurchaseDeferred;
            _provider.PurchaseFailed += OnPurchaseFailed;
        }

        public IEnumerable<ProductDefinition> Catalog => _catalog.Values;
        public int AwaitingValidationCount => _state.AwaitingValidation.Count;

        public async Task<bool> InitializeAsync()
        {
            if (IsInitialized) return true;
            try
            {
                IsInitialized = await _provider.InitializeAsync(new List<ProductDefinition>(_catalog.Values));
            }
            catch (Exception ex)
            {
                _errors.Report(ex, $"IAP provider '{_provider.Name}' init");
                IsInitialized = false;
            }
            if (!IsInitialized) _log.Warning(Tag, "Store unavailable. Purchases disabled; gameplay unaffected.");
            return IsInitialized;
        }

        public StoreProductInfo GetProductInfo(string productId)
        {
            foreach (var p in _provider.Products ?? Array.Empty<StoreProductInfo>())
                if (p.ProductId == productId) return p;
            return null;
        }

        public bool IsOwned(string productId) =>
            _catalog.TryGetValue(productId, out var def) && def.EntitlementId != null && _entitlements.Has(def.EntitlementId);

        public Task<PurchaseOutcome> PurchaseAsync(string productId)
        {
            if (!_catalog.TryGetValue(productId ?? string.Empty, out var def))
                return Task.FromResult(PurchaseOutcome.Fail(productId, PurchaseFailureReason.ProductUnavailable, "not in catalog"));
            if (!IsInitialized)
                return Task.FromResult(PurchaseOutcome.Fail(productId, PurchaseFailureReason.NotInitialized));
            if (def.Type != StoreProductType.Consumable && IsOwned(productId))
                return Task.FromResult(PurchaseOutcome.Fail(productId, PurchaseFailureReason.AlreadyOwned));
            if (_inflight.ContainsKey(productId))
                return Task.FromResult(PurchaseOutcome.Fail(productId, PurchaseFailureReason.ExistingPurchasePending));

            var tcs = new TaskCompletionSource<PurchaseOutcome>();
            _inflight[productId] = tcs;
            _analytics.Log(AnalyticsEvents.PurchaseStarted, AnalyticsParams.ProductId, productId);
            try
            {
                _provider.Purchase(productId);
            }
            catch (Exception ex)
            {
                _errors.Report(ex, $"IAP '{_provider.Name}' Purchase {productId}");
                Complete(productId, PurchaseOutcome.Fail(productId, PurchaseFailureReason.Unknown, ex.Message));
            }
            return tcs.Task;
        }

        /// <summary>Required by Apple for non-consumables/subscriptions (a visible "Restore Purchases" button).</summary>
        public async Task<bool> RestoreAsync()
        {
            if (!IsInitialized) return false;
            try { return await _provider.RestoreAsync(); }
            catch (Exception ex) { _errors.Report(ex, $"IAP '{_provider.Name}' Restore"); return false; }
        }

        public void Dispose()
        {
            _provider.PurchaseReceived -= OnPurchaseReceived;
            _provider.PurchaseDeferred -= OnPurchaseDeferred;
            _provider.PurchaseFailed -= OnPurchaseFailed;
        }

        // ---------------------------------------------------------------------------------

        private async void OnPurchaseReceived(StorePurchase purchase)
        {
            // async void is intentional (event handler); every path is wrapped so nothing escapes.
            try
            {
                await ProcessAsync(purchase);
            }
            catch (Exception ex)
            {
                _errors.Report(ex, "IAP ProcessPurchase");
                if (purchase != null) Complete(purchase.ProductId, PurchaseOutcome.Fail(purchase.ProductId, PurchaseFailureReason.Unknown, ex.Message));
            }
        }

        /// <summary>Exposed for tests; production code path is the provider event.</summary>
        internal async Task ProcessAsync(StorePurchase purchase)
        {
            if (purchase == null || string.IsNullOrEmpty(purchase.TransactionId) || string.IsNullOrEmpty(purchase.ProductId))
            {
                _log.Error(Tag, "Malformed purchase delivered by provider; ignored.");
                return;
            }

            if (!_catalog.TryGetValue(purchase.ProductId, out var def))
            {
                // Unknown product (removed from catalog?). Leave unfinished so a future version can deliver it.
                _log.Error(Tag, $"Purchase for unknown product '{purchase.ProductId}' ({purchase.TransactionId}) left unfinished.");
                return;
            }

            if (_fulfilled.Contains(purchase.TransactionId))
            {
                // Already delivered (crash before finish, or a restore). Ensure entitlement and finish.
                if (def.EntitlementId != null) _entitlements.Grant(def.EntitlementId);
                SafeFinish(purchase);
                Complete(purchase.ProductId, PurchaseOutcome.Ok(purchase.ProductId, purchase.TransactionId));
                return;
            }

            if (!_processing.Add(purchase.TransactionId)) return; // concurrent duplicate delivery
            try
            {
                ValidationResult validation;
                try
                {
                    validation = await _validator.ValidateAsync(purchase) ?? ValidationResult.Retry("validator returned null");
                }
                catch (Exception ex)
                {
                    _errors.Report(ex, "IAP validator");
                    validation = ValidationResult.Retry(ex.Message);
                }

                if (validation.Status == ValidationStatus.Valid && validation.ValidatedProductId != purchase.ProductId)
                    validation = ValidationResult.Invalid($"validated product '{validation.ValidatedProductId}' != '{purchase.ProductId}'");

                switch (validation.Status)
                {
                    case ValidationStatus.RetryLater:
                        _state.AwaitingValidation[purchase.TransactionId] = purchase.ProductId;
                        TrySaveLedger();
                        _log.Warning(Tag, $"Validation unavailable for {purchase.TransactionId}: {validation.Reason}. Will retry; transaction left unfinished.");
                        Complete(purchase.ProductId, PurchaseOutcome.Deferred(purchase.ProductId));
                        return;

                    case ValidationStatus.Invalid:
                        _errors.Report(new InvalidOperationException($"Invalid purchase {purchase.ProductId}/{purchase.TransactionId}: {validation.Reason}"), "IAP validation");
                        _state.AwaitingValidation.Remove(purchase.TransactionId);
                        TrySaveLedger();
                        SafeFinish(purchase); // remove from store queue; nothing delivered
                        _analytics.Log(AnalyticsEvents.PurchaseFailed, new Dictionary<string, object> { { AnalyticsParams.ProductId, purchase.ProductId }, { AnalyticsParams.Reason, "validation" } });
                        Complete(purchase.ProductId, PurchaseOutcome.Fail(purchase.ProductId, PurchaseFailureReason.ValidationFailed, validation.Reason));
                        return;
                }

                // Valid → fulfill.
                if (!Fulfill(def, purchase, validation))
                {
                    // Persistence failed: do NOT finish; the store will re-deliver.
                    Complete(purchase.ProductId, PurchaseOutcome.Fail(purchase.ProductId, PurchaseFailureReason.Unknown, "fulfillment not persisted"));
                    return;
                }

                SafeFinish(purchase);
                var info = GetProductInfo(purchase.ProductId);
                var p = new Dictionary<string, object> { { AnalyticsParams.ProductId, purchase.ProductId } };
                if (info != null && !purchase.IsRestore)
                {
                    p[AnalyticsParams.Value] = (double)info.Price;
                    p[AnalyticsParams.Currency] = info.IsoCurrencyCode;
                }
                if (!purchase.IsRestore) _analytics.Log(AnalyticsEvents.PurchaseCompleted, p);
                _events?.Publish(new PurchaseFulfilled(purchase.ProductId, purchase.TransactionId, purchase.IsRestore));
                Complete(purchase.ProductId, PurchaseOutcome.Ok(purchase.ProductId, purchase.TransactionId));
            }
            finally
            {
                _processing.Remove(purchase.TransactionId);
            }
        }

        private bool Fulfill(ProductDefinition def, StorePurchase purchase, ValidationResult validation)
        {
            if (!def.Payout.IsEmpty)
            {
                var tx = new EconomyTransaction("iap:" + purchase.TransactionId, "iap");
                foreach (var item in def.Payout.Items) tx.Credit(item.ResourceId, item.Amount);
                var status = _wallet.Apply(tx);
                if (status != TransactionStatus.Success && status != TransactionStatus.Duplicate)
                {
                    _log.Error(Tag, $"Payout for {purchase.TransactionId} failed: {status}");
                    return false;
                }
            }

            if (def.EntitlementId != null)
            {
                try
                {
                    _entitlements.Grant(def.EntitlementId, def.Type == StoreProductType.Subscription ? validation.ExpiresUtc : null);
                }
                catch (Exception ex)
                {
                    _errors.Report(ex, "IAP entitlement grant");
                    return false;
                }
            }

            _fulfilled.Add(purchase.TransactionId);
            _state.FulfilledTransactions.Add(purchase.TransactionId);
            _state.AwaitingValidation.Remove(purchase.TransactionId);
            return TrySaveLedger();
        }

        private void OnPurchaseDeferred(string productId)
        {
            _log.Info(Tag, $"Purchase of '{productId}' deferred (awaiting approval / payment).");
            Complete(productId, PurchaseOutcome.Deferred(productId));
        }

        private void OnPurchaseFailed(string productId, PurchaseFailureReason reason, string message)
        {
            if (reason != PurchaseFailureReason.UserCancelled)
                _log.Warning(Tag, $"Purchase of '{productId}' failed: {reason} {message}");
            _analytics.Log(AnalyticsEvents.PurchaseFailed, new Dictionary<string, object> { { AnalyticsParams.ProductId, productId }, { AnalyticsParams.Reason, reason.ToString() } });
            Complete(productId, PurchaseOutcome.Fail(productId, reason, message));
        }

        private void Complete(string productId, PurchaseOutcome outcome)
        {
            if (productId != null && _inflight.TryGetValue(productId, out var tcs))
            {
                _inflight.Remove(productId);
                tcs.TrySetResult(outcome);
            }
        }

        private void SafeFinish(StorePurchase purchase)
        {
            try { _provider.FinishTransaction(purchase); }
            catch (Exception ex) { _errors.Report(ex, $"IAP FinishTransaction {purchase.TransactionId}"); }
        }

        private bool TrySaveLedger()
        {
            try { _slot.Save(_state); return true; }
            catch (Exception ex) { _errors.Report(ex, "IAP ledger save"); return false; }
        }
    }
}
