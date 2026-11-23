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

        protected override void Awake()
        {
            base.Awake();
            playButton.onClick.AddListener(() => Navigation.ResetToAsync(ViewIds.Gameplay, TiltwellApp.Instance.NextPlayableLevelId()).Forget());
            levelsButton.onClick.AddListener(() => Navigation.PushScreenAsync(TiltwellViewIds.Levels).Forget());
            shopButton.onClick.AddListener(() => Navigation.PushScreenAsync(ViewIds.Shop, "main_menu").Forget());
            settingsButton.onClick.AddListener(() => Navigation.ShowPopupAsync(ViewIds.Settings).Forget());
        }

        protected override void OnShowing(object args)
        {
            int index = Core.Progression.Catalog.IndexOf(TiltwellApp.Instance.NextPlayableLevelId());
            playLabel.text = Core.Localization.Format(TiltwellStrings.PlayLevel, index + 1);
        }

        protected override void OnShown()
        {
            DailyRewardView.EnqueueIfClaimable();
            if (AudioService.Instance != null) AudioService.Instance.PlayMusic("menu");
        }
    }
}
