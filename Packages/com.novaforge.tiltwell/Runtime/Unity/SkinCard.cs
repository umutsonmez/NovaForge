using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NovaForge.Tiltwell.Unity
{
    /// <summary>One card in the skin shop: preview, name and a buy / equip button. Filled by <see cref="SkinsView"/>.</summary>
    public sealed class SkinCard : MonoBehaviour
    {
        [SerializeField] private RectTransform previewArea;
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private Button button;
        [SerializeField] private TMP_Text buttonLabel;
        [SerializeField] private GameObject coinIcon;
        [SerializeField] private Image frame;

        public Button Button => button;
        public RectTransform PreviewArea => previewArea;

        public void Set(string displayName, string buttonText, bool showCoin, bool interactable, bool selected, Color normal, Color highlight)
        {
            nameLabel.text = displayName;
            buttonLabel.text = buttonText;
            if (coinIcon != null) coinIcon.SetActive(showCoin);
            button.interactable = interactable;
            if (frame != null) frame.color = selected ? highlight : normal;
        }
    }
}
