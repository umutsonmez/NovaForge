using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NovaForge.Core.Purchasing
{
    /// <summary>
    /// TEST-ONLY store. No money moves; it simulates store callbacks so purchase flows can be tested.
    /// Real purchases require the Unity IAP adapter and store sandbox accounts.
    /// </summary>
    public sealed class MockPurchaseProvider : IPurchaseProvider
    {
        private readonly List<StoreProductInfo> _products = new List<StoreProductInfo>();
        private readonly List<StorePurchase> _owned = new List<StorePurchase>();
        private int _txCounter;

        public string Name => "mock";
        public string Platform => "mock";
        public bool InitializeResult = true;
        /// <summary>What happens on the next Purchase call.</summary>
        public Func<string, MockPurchaseBehaviour> Behaviour = _ => MockPurchaseBehaviour.Succeed;
        public readonly List<StorePurchase> Finished = new List<StorePurchase>();
        public readonly List<StorePurchase> Unfinished = new List<StorePurchase>();

        public IReadOnlyList<StoreProductInfo> Products => _products;

        public event Action<StorePurchase> PurchaseReceived;
        public event Action<string> PurchaseDeferred;
        public event Action<string, PurchaseFailureReason, string> PurchaseFailed;

        public Task<bool> InitializeAsync(IReadOnlyList<ProductDefinition> catalog)
        {
            _products.Clear();
            foreach (var p in catalog)
                _products.Add(new StoreProductInfo { ProductId = p.Id, LocalizedPrice = "$0.99", Price = 0.99m, IsoCurrencyCode = "USD", LocalizedTitle = p.Id, Available = true });
            // Redeliver unfinished transactions like real stores do on startup.
            foreach (var u in Unfinished.ToArray()) PurchaseReceived?.Invoke(u);
            return Task.FromResult(InitializeResult);
        }

        public void Purchase(string productId)
        {
            switch (Behaviour(productId))
            {
                case MockPurchaseBehaviour.Succeed:
                    var p = new StorePurchase { ProductId = productId, TransactionId = "mock-tx-" + (++_txCounter), Receipt = "mock-receipt", Platform = Platform };
                    Unfinished.Add(p);
                    PurchaseReceived?.Invoke(p);
                    break;
                case MockPurchaseBehaviour.Cancel:
                    PurchaseFailed?.Invoke(productId, PurchaseFailureReason.UserCancelled, "cancelled");
                    break;
                case MockPurchaseBehaviour.Decline:
                    PurchaseFailed?.Invoke(productId, PurchaseFailureReason.PaymentDeclined, "declined");
                    break;
                case MockPurchaseBehaviour.Defer:
                    PurchaseDeferred?.Invoke(productId);
                    break;
            }
        }

        /// <summary>Test helper: delivers an arbitrary transaction (e.g. re-delivery, approved deferred payment).</summary>
        public void Deliver(StorePurchase purchase)
        {
            if (!Unfinished.Contains(purchase)) Unfinished.Add(purchase);
            PurchaseReceived?.Invoke(purchase);
        }

        public Task<bool> RestoreAsync()
        {
            foreach (var o in _owned)
                PurchaseReceived?.Invoke(new StorePurchase { ProductId = o.ProductId, TransactionId = o.TransactionId, Receipt = o.Receipt, Platform = Platform, IsRestore = true });
            return Task.FromResult(true);
        }

        public void FinishTransaction(StorePurchase purchase)
        {
            Unfinished.RemoveAll(u => u.TransactionId == purchase.TransactionId);
            Finished.Add(purchase);
            if (!purchase.IsRestore && !_owned.Exists(o => o.TransactionId == purchase.TransactionId)) _owned.Add(purchase);
        }
    }

    public enum MockPurchaseBehaviour { Succeed, Cancel, Decline, Defer }
}
