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
        [Tooltip("Star objects in order. Earned stars keep their color; unearned ones are dimmed (or hidden if they have no Graphic).")]
        [SerializeField] private GameObject[] stars = Array.Empty<GameObject>();
        [SerializeField] private TMP_Text rewardLabel;
        [SerializeField] private Button nextButton;
        [SerializeField] private Button retryButton;
        [SerializeField] private Button homeButton;
        [SerializeField] private Button doubleRewardButton;
        [Tooltip("Optional: burst on win.")]
        [SerializeField] private ConfettiBurst confetti;
        [Tooltip("Seconds between earned stars popping in (stars with a UIPopIn).")]
        [SerializeField, Min(0f)] private float starStagger = 0.22f;

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

        [SerializeField, Range(0f, 1f)] private float unearnedStarAlpha = 0.18f;
        private Color[] _starColors;

        private void SetStar(int i, bool won, bool earned)
        {
            var star = stars[i];
            if (star == null) return;
            if (_starColors == null)
            {
                _starColors = new Color[stars.Length];
                for (int k = 0; k < stars.Length; k++)
                    _starColors[k] = stars[k] != null && stars[k].TryGetComponent<Graphic>(out var gk) ? gk.color : Color.white;
            }
            if (!star.TryGetComponent<Graphic>(out var g)) { star.SetActive(earned); return; }
            star.SetActive(won); // a lost level shows no star row at all
            var c = _starColors[i];
            g.color = earned ? c : new Color(c.r, c.g, c.b, c.a * unearnedStarAlpha);
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
            for (int i = 0; i < stars.Length; i++)
            {
                SetStar(i, won, i < starCount);
                if (stars[i] != null && i < starCount && stars[i].TryGetComponent<UIPopIn>(out var pop)) pop.Play(0.25f + i * starStagger);
            }
            if (won && confetti != null) confetti.Burst();
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
}
