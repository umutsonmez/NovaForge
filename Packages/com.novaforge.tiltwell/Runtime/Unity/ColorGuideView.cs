using NovaForge.Core.UI;
using NovaForge.UI;
using UnityEngine;
using UnityEngine.UI;

namespace NovaForge.Tiltwell.Unity
{
    /// <summary>
    /// Popup (view id "color_guide") that teaches the color recipes. Args: <see cref="Paint"/> to highlight
    /// (boxed), or null. Brown is only listed once the player has reached it (arg = Brown).
    /// </summary>
    public sealed class ColorGuideView : UIView
    {
        [SerializeField] private RecipeRow[] rows;
        [SerializeField] private Image[] rowBackgrounds;
        [SerializeField] private Button okButton;
        [Tooltip("Panel resized to fit the visible rows.")]
        [SerializeField] private RectTransform panel;
        [SerializeField] private float baseHeight = 520f;
        [SerializeField] private float rowHeight = 172f;
        [SerializeField] private Color normalRow = new Color32(0xF1, 0xF4, 0xFB, 0xFF);
        [SerializeField] private Color highlightRow = new Color32(0xFF, 0xF1, 0xC9, 0xFF);

        protected override void Awake()
        {
            base.Awake();
            okButton.onClick.AddListener(Close);
        }

        protected override void OnShowing(object args)
        {
            var highlight = args is Paint p ? p : Paint.None;
            bool showBrown = highlight == Paint.Brown || Core == null || Core.Tutorials.IsCompleted(TiltwellTutorials.Brown);
            for (int i = 0; i < rows.Length && i < ColorRecipes.MixedColors.Length; i++)
            {
                var color = ColorRecipes.MixedColors[i];
                var holder = rowBackgrounds != null && i < rowBackgrounds.Length && rowBackgrounds[i] != null
                    ? rowBackgrounds[i].gameObject : rows[i].gameObject;
                holder.SetActive(color != Paint.Brown || showBrown);
                rows[i].Set(color);
                if (rowBackgrounds != null && i < rowBackgrounds.Length && rowBackgrounds[i] != null)
                    rowBackgrounds[i].color = color == highlight ? highlightRow : normalRow;
            }
            if (panel != null)
            {
                int visible = showBrown ? ColorRecipes.MixedColors.Length : ColorRecipes.MixedColors.Length - 1;
                panel.sizeDelta = new Vector2(panel.sizeDelta.x, baseHeight + visible * rowHeight);
            }
        }
    }
}
