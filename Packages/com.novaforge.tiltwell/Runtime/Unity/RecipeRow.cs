using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NovaForge.Tiltwell.Unity
{
    /// <summary>
    /// Draws one color recipe as droplets: "red + yellow = orange". Children are rebuilt by <see cref="Set"/>,
    /// so the row needs only a HorizontalLayoutGroup (added if missing), a circle sprite and a font.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class RecipeRow : MonoBehaviour
    {
        [SerializeField] private Sprite circle;
        [SerializeField] private TMP_FontAsset font;
        [SerializeField] private float dropSize = 64f;
        [SerializeField] private float resultScale = 1.25f;
        [SerializeField] private Color signColor = new Color32(0x2A, 0x2D, 0x52, 0xFF);

        public Paint Color { get; private set; }

        public void Set(Paint result)
        {
            Color = result;
            for (int i = transform.childCount - 1; i >= 0; i--) Destroy(transform.GetChild(i).gameObject);
            if (!TryGetComponent<HorizontalLayoutGroup>(out var h))
            {
                h = gameObject.AddComponent<HorizontalLayoutGroup>();
                h.spacing = dropSize * 0.18f;
                h.childAlignment = TextAnchor.MiddleCenter;
                h.childControlWidth = h.childControlHeight = false;
                h.childForceExpandWidth = h.childForceExpandHeight = false;
            }
            var parts = ColorRecipes.Ingredients(result);
            for (int i = 0; i < parts.Count; i++)
            {
                if (i > 0) Sign("+");
                Drop(parts[i], dropSize);
            }
            Sign("=");
            Drop(result, dropSize * resultScale);
        }

        private void Drop(Paint paint, float size)
        {
            var go = new GameObject(paint.ToString(), typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(transform, false);
            ((RectTransform)go.transform).sizeDelta = new Vector2(size, size);
            var img = go.GetComponent<Image>();
            img.sprite = circle != null ? circle : TiltwellVisuals.Circle;
            img.color = TiltwellVisuals.Of(paint);
            img.raycastTarget = false;

            // Small highlight so it reads as the same glossy drop as on the board.
            var shine = new GameObject("Shine", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            shine.transform.SetParent(go.transform, false);
            var srt = (RectTransform)shine.transform;
            srt.anchorMin = srt.anchorMax = new Vector2(0.34f, 0.7f);
            srt.sizeDelta = new Vector2(size * 0.28f, size * 0.2f);
            var si = shine.GetComponent<Image>();
            si.sprite = img.sprite;
            si.color = new Color(1, 1, 1, 0.75f);
            si.raycastTarget = false;
        }

        private void Sign(string s)
        {
            var go = new GameObject(s == "+" ? "Plus" : "Equals", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.transform.SetParent(transform, false);
            ((RectTransform)go.transform).sizeDelta = new Vector2(dropSize * 0.55f, dropSize);
            var t = go.GetComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.text = s;
            t.fontSize = dropSize * 0.75f;
            t.color = signColor;
            t.alignment = TextAlignmentOptions.Center;
            t.raycastTarget = false;
        }
    }
}
