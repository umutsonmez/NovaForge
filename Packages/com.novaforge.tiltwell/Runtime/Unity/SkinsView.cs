using System.Collections.Generic;
using NovaForge.Core.Economy;
using NovaForge.Core.UI;
using NovaForge.UI;
using NovaForge.Unity.Audio;
using UnityEngine;
using UnityEngine.UI;

namespace NovaForge.Tiltwell.Unity
{
    /// <summary>Popup (view id "skins"): buy droplet skins with coins and pick one.</summary>
    public sealed class SkinsView : UIView
    {
        [SerializeField] private SkinCard cardTemplate;
        [SerializeField] private Button closeButton;
        [SerializeField] private Sprite circle;
        [SerializeField] private Sprite rounded;
        [SerializeField] private float roundedRadiusPx = 48f;
        [SerializeField] private Sprite star;
        [SerializeField] private Color cardNormal = new Color32(0xF1, 0xF4, 0xFB, 0xFF);
        [SerializeField] private Color cardSelected = new Color32(0xFF, 0xF1, 0xC9, 0xFF);

        private readonly List<SkinCard> _cards = new List<SkinCard>();

        protected override void Awake()
        {
            base.Awake();
            closeButton.onClick.AddListener(Close);
            cardTemplate.gameObject.SetActive(false);
        }

        protected override void OnShowing(object args)
        {
            if (_cards.Count == 0)
            {
                foreach (var skin in DropSkins.All)
                {
                    var card = Instantiate(cardTemplate, cardTemplate.transform.parent, false);
                    card.gameObject.SetActive(true);
                    card.name = "Skin_" + skin;
                    var s = skin;
                    card.Button.onClick.AddListener(() => OnPressed(s));
                    var root = card.PreviewArea;
                    var colors = new[] { Paint.Red, Paint.Yellow, Paint.Blue };
                    for (int i = 0; i < colors.Length; i++)
                    {
                        var dropRoot = new GameObject("Drop" + i, typeof(RectTransform)).transform as RectTransform;
                        dropRoot.SetParent(root, false);
                        dropRoot.sizeDelta = Vector2.one * 96f;
                        dropRoot.anchoredPosition = new Vector2((i - 1) * 104f, i == 1 ? 14f : -6f);
                        DropVisual.Create(dropRoot, 96f, TiltwellVisuals.Of(colors[i]), skin, new DropSprites(circle, rounded, roundedRadiusPx, star));
                    }
                    _cards.Add(card);
                }
            }
            Refresh();
        }

        private void Refresh()
        {
            var state = TiltwellApp.Instance.Skins;
            long coins = Core.Economy.GetBalance("coin");
            for (int i = 0; i < _cards.Count; i++)
            {
                var skin = DropSkins.All[i];
                string name = Core.Localization.Get(TiltwellStrings.SkinName(skin));
                bool owned = state.Owns(skin), equipped = state.Equipped == skin;
                string label = equipped ? Core.Localization.Get(TiltwellStrings.Equipped)
                    : owned ? Core.Localization.Get(TiltwellStrings.Equip)
                    : DropSkins.Price(skin).ToString();
                _cards[i].Set(name, label, !owned, !equipped && (owned || coins >= DropSkins.Price(skin)), equipped, cardNormal, cardSelected);
            }
        }

        private void OnPressed(DropSkin skin)
        {
            var app = TiltwellApp.Instance;
            if (!app.Skins.Owns(skin))
            {
                var tx = new EconomyTransaction("skin:" + DropSkins.Id(skin), "skin_buy").Debit("coin", DropSkins.Price(skin));
                if (Core.Economy.Apply(tx) != TransactionStatus.Success) { Refresh(); return; }
                if (AudioService.Instance != null) AudioService.Instance.PlaySfx(SoundIds.RewardCollect);
                Haptics.Play(HapticStrength.Medium);
            }
            app.SetSkin(skin);
            Refresh();
        }
    }
}
