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
            if (!IsVisible || Core == null || Time.unscaledTime < _nextCountdownRefresh) return;
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
}
