using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NovaForge.Core.Ads;
using NovaForge.Core.Economy;
using NovaForge.Core.Foundation;
using NovaForge.Core.Localization;
using NovaForge.Core.UI;
using NovaForge.UI;
using NovaForge.Unity.Audio;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if TILTWELL_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace NovaForge.Tiltwell.Unity
{
    /// <summary>Main menu (view id "main_menu").</summary>
    public sealed class MainMenuView : UIView
    {
        [SerializeField] private TMP_Text playLabel;
        [SerializeField] private Button playButton;
        [SerializeField] private Button levelsButton;
        [SerializeField] private Button shopButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button dailyButton;
        [Header("Daily puzzle")]
        [SerializeField] private Button puzzleButton;
        [Tooltip("Badge on the puzzle button: '!' when today's puzzle is open, the streak when done.")]
        [SerializeField] private TMP_Text puzzleBadge;

        protected override void Awake()
        {
            base.Awake();
            playButton.onClick.AddListener(() => Navigation.ResetToAsync(ViewIds.Gameplay, TiltwellApp.Instance.NextPlayableLevelId()).Forget());
            levelsButton.onClick.AddListener(() => Navigation.PushScreenAsync(TiltwellViewIds.Levels).Forget());
            shopButton.onClick.AddListener(() => Navigation.PushScreenAsync(ViewIds.Shop, "main_menu").Forget());
            settingsButton.onClick.AddListener(() => Navigation.ShowPopupAsync(ViewIds.Settings).Forget());
            if (dailyButton != null) dailyButton.onClick.AddListener(() => Navigation.ShowPopupAsync(ViewIds.DailyReward).Forget());
            if (puzzleButton != null) puzzleButton.onClick.AddListener(() => OpenPuzzle().Forget());
        }

        private async Task OpenPuzzle()
        {
            var app = TiltwellApp.Instance;
            int today = app.Today;
            if (app.Daily.IsDone(today))
            {
                await UIManager.Instance.ConfirmAsync(new ConfirmRequest
                {
                    TitleKey = TiltwellStrings.DailyPuzzle,
                    MessageKey = TiltwellStrings.DailyComeBack,
                    ConfirmKey = CoreStrings.Ok,
                    CancelKey = null,
                });
                return;
            }
            puzzleButton.interactable = false;
            try
            {
                var level = await app.GetDailyAsync(today);
                if (this == null) return;
                await Navigation.ResetToAsync(ViewIds.Gameplay, new DailyArgs { Level = level, Day = today });
            }
            catch (Exception ex)
            {
                Core.Errors.Report(ex, "Tiltwell.DailyPuzzle");
            }
            finally
            {
                if (this != null) puzzleButton.interactable = true;
            }
        }

        private void RefreshPuzzleBadge()
        {
            if (puzzleBadge == null) return;
            var app = TiltwellApp.Instance;
            int today = app.Today;
            bool done = app.Daily.IsDone(today);
            int streak = app.Daily.CurrentStreak(today);
            var bubble = puzzleBadge.transform.parent.gameObject;
            bubble.SetActive(!done || streak > 0);
            puzzleBadge.text = done ? streak.ToString() : "!";
        }

        protected override void OnShowing(object args)
        {
            int index = Core.Progression.Catalog.IndexOf(TiltwellApp.Instance.NextPlayableLevelId());
            playLabel.text = Core.Localization.Format(TiltwellStrings.PlayLevel, index + 1);
            RefreshPuzzleBadge();
        }

        protected override void OnShown()
        {
            DailyRewardView.EnqueueIfClaimable();
            if (AudioService.Instance != null) AudioService.Instance.PlayMusic(TiltwellSounds.Music);
        }
    }
}
