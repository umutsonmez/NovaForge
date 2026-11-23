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
    /// <summary>Formats reward bundles: "50 Coins, 1 Hint". Resource names use the key "resource.&lt;id&gt;" when present.</summary>
    public static class RewardText
    {
        public static string Format(RewardBundle bundle, LocalizationService loc)
        {
            if (bundle == null || bundle.IsEmpty) return "";
            var sb = new StringBuilder();
            foreach (var item in bundle.Items)
            {
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(CurrencyDisplay.FormatCompact(item.Amount)).Append(' ').Append(ResourceName(item.ResourceId, loc));
            }
            return sb.ToString();
        }

        public static string ResourceName(string resourceId, LocalizationService loc)
        {
            var key = "resource." + resourceId;
            return loc != null && loc.Has(key) ? loc.Get(key) : resourceId;
        }
    }

    /// <summary>Plays the click sound and a light haptic, and gives a small press scale. Add next to any Button.</summary>
    [RequireComponent(typeof(Button))]
    public sealed class UIButtonFeedback : MonoBehaviour, UnityEngine.EventSystems.IPointerDownHandler, UnityEngine.EventSystems.IPointerUpHandler
    {
        [SerializeField] private string soundId = SoundIds.ButtonClick;
        [SerializeField] private bool haptic = true;
        [SerializeField, Range(0.8f, 1f)] private float pressedScale = 0.95f;
        private Vector3 _baseScale;

        private void Awake()
        {
            _baseScale = transform.localScale;
            GetComponent<Button>().onClick.AddListener(() =>
            {
                if (AudioService.Instance != null && !string.IsNullOrEmpty(soundId)) AudioService.Instance.PlaySfx(soundId);
                if (haptic) Haptics.Play(HapticStrength.Light);
            });
        }

        public void OnPointerDown(UnityEngine.EventSystems.PointerEventData e)
        {
            if (GetComponent<Button>().interactable) transform.localScale = _baseScale * pressedScale;
        }

        public void OnPointerUp(UnityEngine.EventSystems.PointerEventData e) => transform.localScale = _baseScale;
        private void OnDisable() { if (_baseScale != Vector3.zero) transform.localScale = _baseScale; }
    }

    // ====================================================================== level result

    public enum LevelResultChoice { Next, Retry, Home }

    /// <summary>Published when the player picks an action on the level result view. The game navigates.</summary>
    public readonly struct LevelResultChosen
    {
        public readonly string LevelId;
        public readonly bool Won;
        public readonly LevelResultChoice Choice;
        public LevelResultChosen(string levelId, bool won, LevelResultChoice choice) { LevelId = levelId; Won = won; Choice = choice; }
    }

    /// <summary>
    /// Win / lose popup. Show with the <see cref="LevelSessionEnded"/> returned by LevelSessionService.Win/Lose.
    /// Offers "x2 reward" via a rewarded ad only when a reward was granted and an ad is ready (player-initiated, per policy).
    /// </summary>
    public sealed class LevelResultView : UIView
    {
        [SerializeField] private TMP_Text title;
        [Tooltip("Star objects in order; the first N are activated for N stars.")]
        [SerializeField] private GameObject[] stars = Array.Empty<GameObject>();
        [SerializeField] private TMP_Text rewardLabel;
        [SerializeField] private Button nextButton;
        [SerializeField] private Button retryButton;
        [SerializeField] private Button homeButton;
        [SerializeField] private Button doubleRewardButton;

        private LevelSessionEnded _result;
        private bool _hasResult;
        private bool _doubled;

        protected override void Awake()
        {
            base.Awake();
            if (nextButton != null) nextButton.onClick.AddListener(() => Choose(LevelResultChoice.Next));
            if (retryButton != null) retryButton.onClick.AddListener(() => Choose(LevelResultChoice.Retry));
            if (homeButton != null) homeButton.onClick.AddListener(() => Choose(LevelResultChoice.Home));
            if (doubleRewardButton != null) doubleRewardButton.onClick.AddListener(OnDoubleReward);
        }

        protected override void OnShowing(object args)
        {
            _hasResult = args is LevelSessionEnded;
            _result = _hasResult ? (LevelSessionEnded)args : default;
            _doubled = false;
            bool won = _hasResult && _result.Won;
            var loc = Core.Localization;

            if (title != null) title.text = loc.Get(won ? CoreStrings.WinTitle : CoreStrings.LoseTitle);
            int starCount = won && _result.Completion != null ? _result.Completion.Stars : 0;
            for (int i = 0; i < stars.Length; i++) if (stars[i] != null) stars[i].SetActive(i < starCount);
            RenderReward();
            if (nextButton != null) nextButton.gameObject.SetActive(won);
            if (retryButton != null) retryButton.gameObject.SetActive(!won);
            if (doubleRewardButton != null)
                doubleRewardButton.gameObject.SetActive(won && HasReward && Core.Ads != null && Core.Ads.IsRewardedAvailable(AdPlacements.DoubleReward));

            if (AudioService.Instance != null) AudioService.Instance.PlaySfx(won ? SoundIds.Win : SoundIds.Lose);
            Haptics.Play(won ? HapticStrength.Medium : HapticStrength.Heavy);
        }

        private bool HasReward => _hasResult && _result.RewardGranted != null && !_result.RewardGranted.IsEmpty;

        private void RenderReward()
        {
            if (rewardLabel == null) return;
            rewardLabel.gameObject.SetActive(HasReward);
            if (!HasReward) return;
            var bundle = _doubled ? _result.RewardGranted.Scaled(2) : _result.RewardGranted;
            rewardLabel.text = "+" + RewardText.Format(bundle, Core.Localization);
        }

        private async void OnDoubleReward()
        {
            doubleRewardButton.interactable = false;
            var outcome = await Core.Ads.ShowRewardedForBundleAsync(AdPlacements.DoubleReward, _result.RewardGranted, Core.Rewards);
            if (this == null) return;
            if (outcome == RewardedAdOutcome.RewardGranted)
            {
                _doubled = true;
                RenderReward();
                doubleRewardButton.gameObject.SetActive(false);
                if (AudioService.Instance != null) AudioService.Instance.PlaySfx(SoundIds.RewardCollect);
            }
            else doubleRewardButton.interactable = true;
        }

        public override bool HandleBack()
        {
            Choose(LevelResultChoice.Home);
            return true;
        }

        private void Choose(LevelResultChoice choice)
        {
            if (!IsVisible) return; // ignore taps during the exit transition
            Core.Events.Publish(new LevelResultChosen(_result.LevelId, _hasResult && _result.Won, choice));
            Close();
        }
    }

    // ====================================================================== daily reward

    /// <summary>One day cell of the daily reward calendar.</summary>
    public sealed class DailyRewardDayCell : MonoBehaviour
    {
        public TMP_Text dayLabel;
        public TMP_Text rewardLabel;
        [Tooltip("Shown for already claimed days.")]
        public GameObject claimedMark;
        [Tooltip("Shown for the day that the next claim grants.")]
        public GameObject currentHighlight;
    }

    /// <summary>Daily login reward calendar (view id "daily_reward"). Use <see cref="EnqueueIfClaimable"/> at session start.</summary>
    public sealed class DailyRewardView : UIView
    {
        [SerializeField] private DailyRewardDayCell[] days = Array.Empty<DailyRewardDayCell>();
        [SerializeField] private Button claimButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private TMP_Text countdownLabel;

        private float _nextCountdownRefresh;

        /// <summary>Queues the popup when today's reward is available. Priority is high so it shows before other offers.</summary>
        public static void EnqueueIfClaimable(int priority = 100)
        {
            var core = NovaForgeApp.Core;
            if (core == null || UIManager.Instance == null || !core.DailyRewards.CanClaim) return;
            UIManager.Instance.Navigation.EnqueuePopupAsync(ViewIds.DailyReward, null, priority).Forget();
        }

        protected override void Awake()
        {
            base.Awake();
            if (claimButton != null) claimButton.onClick.AddListener(OnClaim);
            if (closeButton != null) closeButton.onClick.AddListener(Close);
        }

        protected override void OnShowing(object args) => Render();

        private void Render()
        {
            var daily = Core.DailyRewards;
            var loc = Core.Localization;
            var schedule = daily.Schedule;
            int current = daily.CurrentDayIndex;
            bool canClaim = daily.CanClaim;
            // When today's reward is already taken, CurrentDayIndex points to tomorrow: days before it are claimed.
            for (int i = 0; i < days.Length; i++)
            {
                var cell = days[i];
                if (cell == null) continue;
                bool exists = i < schedule.Count;
                cell.gameObject.SetActive(exists);
                if (!exists) continue;
                if (cell.dayLabel != null) cell.dayLabel.text = loc.Format(CoreStrings.DailyDay, i + 1);
                if (cell.rewardLabel != null) cell.rewardLabel.text = RewardText.Format(schedule[i], loc);
                if (cell.claimedMark != null) cell.claimedMark.SetActive(i < current);
                if (cell.currentHighlight != null) cell.currentHighlight.SetActive(i == current && canClaim);
            }
            if (claimButton != null) claimButton.gameObject.SetActive(canClaim);
            if (closeButton != null) closeButton.gameObject.SetActive(!canClaim);
            RenderCountdown();
        }

        private void RenderCountdown()
        {
            if (countdownLabel == null) return;
            bool waiting = !Core.DailyRewards.CanClaim;
            countdownLabel.gameObject.SetActive(waiting);
            if (!waiting) return;
            var t = Core.DailyRewards.TimeUntilNextClaim;
            countdownLabel.text = Core.Localization.Format(CoreStrings.DailyNextIn, $"{(int)t.TotalHours:00}:{t.Minutes:00}:{t.Seconds:00}");
        }

        private void Update()
        {
            if (!IsVisible || Time.unscaledTime < _nextCountdownRefresh) return;
            _nextCountdownRefresh = Time.unscaledTime + 1f;
            if (Core.DailyRewards.CanClaim && claimButton != null && !claimButton.gameObject.activeSelf) Render(); // day rolled over
            else RenderCountdown();
        }

        private void OnClaim()
        {
            var status = Core.DailyRewards.Claim();
            if (status == DailyClaimStatus.Claimed)
            {
                if (AudioService.Instance != null) AudioService.Instance.PlaySfx(SoundIds.RewardCollect);
                Haptics.Play(HapticStrength.Medium);
            }
            else Core.Log.Warning("DailyReward", $"Claim failed: {status}");
            Render();
        }
    }

    // ====================================================================== shop

    /// <summary>One shop row/card. Bound by ShopView; layout is up to the game.</summary>
    public sealed class ShopItemView : MonoBehaviour
    {
        public TMP_Text titleLabel;
        public TMP_Text contentLabel;
        public TMP_Text priceLabel;
        public Button buyButton;
        [Tooltip("Shown instead of the buy button once the purchase limit is reached.")]
        public GameObject ownedMark;
    }

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
