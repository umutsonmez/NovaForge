using System;
using System.Collections.Generic;
using System.Text;
using NovaForge.Core;
using NovaForge.Core.Ads;
using NovaForge.Core.Economy;
using NovaForge.Core.Foundation;
using NovaForge.Core.Localization;
using NovaForge.Core.Meta;
using NovaForge.Core.Progression;
using NovaForge.Core.Shop;
using NovaForge.Core.UI;
using NovaForge.Unity;
using NovaForge.Unity.Audio;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NovaForge.UI
{
    /// <summary>
    /// Shop screen (view id "shop"). Lists ShopService items (optionally one category) by cloning a template row.
    /// Soft-currency and real-money purchases both go through ShopService (validation, ledger, limits).
    /// Restore button: required by Apple for non-consumables; shown on iOS (and in the editor for testing).
    /// </summary>
    public sealed class ShopView : UIView
    {
        [SerializeField] private ShopItemView itemTemplate;
        [SerializeField] private RectTransform itemContainer;
        [Tooltip("Empty = all categories.")]
        [SerializeField] private string category;
        [SerializeField] private Button restoreButton;
        [SerializeField] private Button closeButton;

        private readonly List<ShopItemView> _rows = new List<ShopItemView>();
        private bool _busy;

        protected override void Awake()
        {
            base.Awake();
            if (itemTemplate != null) itemTemplate.gameObject.SetActive(false);
            if (restoreButton != null) restoreButton.onClick.AddListener(OnRestore);
            if (closeButton != null) closeButton.onClick.AddListener(Close);
        }

        protected override void OnShowing(object args)
        {
            Core.Shop.NotifyOpened(args as string);
            if (restoreButton != null)
                restoreButton.gameObject.SetActive(Core.Purchases != null && (Application.platform == RuntimePlatform.IPhonePlayer || Application.isEditor));
            Rebuild();
        }

        private void Rebuild()
        {
            foreach (var row in _rows) if (row != null) Destroy(row.gameObject);
            _rows.Clear();
            if (itemTemplate == null) return;
            var parent = itemContainer != null ? itemContainer : (RectTransform)itemTemplate.transform.parent;
            foreach (var item in Core.Shop.GetItems(string.IsNullOrEmpty(category) ? null : category))
            {
                var row = Instantiate(itemTemplate, parent, false);
                row.gameObject.SetActive(true);
                Bind(row, item);
                _rows.Add(row);
            }
        }

        private void Bind(ShopItemView row, ShopItem item)
        {
            var loc = Core.Localization;
            if (row.titleLabel != null) row.titleLabel.text = loc.Has(item.TitleKey) ? loc.Get(item.TitleKey) : item.Id;
            if (row.contentLabel != null) row.contentLabel.text = item.Payout != null ? RewardText.Format(item.Payout, loc) : "";
            bool limitReached = item.MaxPurchases > 0 && Core.Shop.PurchaseCount(item.Id) >= item.MaxPurchases;
            if (row.ownedMark != null) row.ownedMark.SetActive(limitReached);
            if (row.buyButton != null)
            {
                row.buyButton.gameObject.SetActive(!limitReached);
                row.buyButton.onClick.RemoveAllListeners();
                row.buyButton.onClick.AddListener(() => OnBuy(item));
            }
            if (row.priceLabel != null)
                row.priceLabel.text = limitReached ? loc.Get(CoreStrings.ShopOwned) : Core.Shop.GetDisplayPrice(item.Id);
        }

        private async void OnBuy(ShopItem item)
        {
            if (_busy) return;
            _busy = true;
            try
            {
                var status = item.IsRealMoney
                    ? await Core.Shop.BuyWithRealMoneyAsync(item.Id)
                    : Core.Shop.BuyWithSoftCurrency(item.Id);
                if (this == null) return;
                switch (status)
                {
                    case ShopPurchaseStatus.Success:
                        if (AudioService.Instance != null) AudioService.Instance.PlaySfx(SoundIds.RewardCollect);
                        Haptics.Play(HapticStrength.Medium);
                        break;
                    case ShopPurchaseStatus.Cancelled:
                        break; // the player backed out: say nothing
                    case ShopPurchaseStatus.InsufficientFunds:
                        await Info(CoreStrings.NotEnough);
                        break;
                    case ShopPurchaseStatus.Pending:
                        await Info(CoreStrings.PurchasePending);
                        break;
                    case ShopPurchaseStatus.AlreadyOwned:
                    case ShopPurchaseStatus.LimitReached:
                        break;
                    default:
                        await Info(CoreStrings.PurchaseFailed);
                        break;
                }
                if (this != null && IsVisible) Rebuild();
            }
            finally { _busy = false; }
        }

        private async void OnRestore()
        {
            if (_busy || Core.Purchases == null) return;
            _busy = true;
            try
            {
                bool ok = await Core.Purchases.RestoreAsync();
                if (!ok && this != null) await Info(CoreStrings.PurchaseFailed);
                if (this != null && IsVisible) Rebuild();
            }
            finally { _busy = false; }
        }

        private System.Threading.Tasks.Task<bool> Info(string messageKey) =>
            UIManager.Instance.ConfirmAsync(new ConfirmRequest { MessageKey = messageKey, CancelKey = null });
    }
}
