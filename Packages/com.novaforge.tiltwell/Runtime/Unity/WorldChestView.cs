using NovaForge.Core.UI;
using NovaForge.UI;
using NovaForge.Unity.Audio;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NovaForge.Tiltwell.Unity
{
    /// <summary>
    /// Popup (view id "world_chest") shown when a world is cleared. Args: the world's tier (int). The reward is
    /// granted by <see cref="TiltwellApp.OpenChest"/> before the popup opens; this view only celebrates it.
    /// </summary>
    public sealed class WorldChestView : UIView
    {
        [SerializeField] private TMP_Text title;
        [SerializeField] private TMP_Text coinsLabel;
        [SerializeField] private TMP_Text hintsLabel;
        [SerializeField] private Button collectButton;
        [SerializeField] private ConfettiBurst confetti;
        [SerializeField] private UIPopIn chestPop;

        protected override void Awake()
        {
            base.Awake();
            collectButton.onClick.AddListener(Close);
        }

        protected override void OnShowing(object args)
        {
            int tier = args is int t ? t : 1;
            var (coins, hints) = WorldChest.Reward(tier);
            title.text = Core.Localization.Format(TiltwellStrings.WorldDone, tier);
            coinsLabel.text = "+" + coins;
            hintsLabel.text = "+" + hints;
        }

        protected override void OnShown()
        {
            if (chestPop != null) chestPop.Play(0f);
            if (confetti != null) confetti.Burst();
            if (AudioService.Instance != null) AudioService.Instance.PlaySfx(SoundIds.Win);
            Haptics.Play(HapticStrength.Medium);
        }
    }
}
