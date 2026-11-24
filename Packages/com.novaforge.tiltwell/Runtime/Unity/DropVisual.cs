using NovaForge.UI;
using UnityEngine;
using UnityEngine.UI;

namespace NovaForge.Tiltwell.Unity
{
    /// <summary>Sprites a droplet can be built from (theme sprites, with generated fallbacks).</summary>
    public readonly struct DropSprites
    {
        public readonly Sprite Circle, Rounded, Star;
        public readonly float RoundedPx;

        public DropSprites(Sprite circle, Sprite rounded, float roundedPx, Sprite star)
        {
            Circle = circle != null ? circle : TiltwellVisuals.Circle;
            Rounded = rounded != null ? rounded : TiltwellVisuals.Rounded;
            RoundedPx = rounded != null ? roundedPx : 14f;
            Star = star;
        }
    }

    /// <summary>
    /// Builds a droplet inside <c>root</c> in the given skin. Returns the body: the only tinted part, a direct child of
    /// root, so callers recolor it with <c>body.color</c> and move/scale <c>root</c>. Shared by the board and the skin shop.
    /// </summary>
    public static class DropVisual
    {
        public static Image Create(RectTransform root, float size, Color color, DropSkin skin, DropSprites sprites)
        {
            var bodySprite = skin == DropSkin.Star && sprites.Star != null ? sprites.Star : sprites.Circle;
            var shadow = Img("Shadow", root, bodySprite, new Color(0.1f, 0.12f, 0.3f, 0.22f));
            Stretch(shadow.rectTransform);
            shadow.rectTransform.anchoredPosition = new Vector2(0, -size * 0.08f);

            Image body;
            switch (skin)
            {
                case DropSkin.Jelly:
                    // Soft jelly cube: rounded square standing straight (diamonds are paint pads on the board).
                    shadow.sprite = sprites.Rounded;
                    Slice(shadow, sprites, size * 0.26f);
                    shadow.rectTransform.sizeDelta = new Vector2(-size * 0.1f, -size * 0.1f);
                    body = Img("Body", root, sprites.Rounded, color);
                    Slice(body, sprites, size * 0.26f);
                    Stretch(body.rectTransform);
                    body.rectTransform.sizeDelta = new Vector2(-size * 0.1f, -size * 0.1f);
                    body.gameObject.AddComponent<UIGradient>().Set(Color.white, new Color(0.72f, 0.72f, 0.82f));
                    var gloss = Img("Gloss", root, sprites.Rounded, new Color(1f, 1f, 1f, 0.35f));
                    Slice(gloss, sprites, size * 0.1f);
                    gloss.rectTransform.sizeDelta = new Vector2(size * 0.62f, size * 0.16f);
                    gloss.rectTransform.anchoredPosition = new Vector2(0, size * 0.24f);
                    break;
                case DropSkin.Bubble:
                    shadow.color = new Color(0.1f, 0.12f, 0.3f, 0.12f);
                    body = Img("Body", root, sprites.Circle, color);
                    Stretch(body.rectTransform);
                    body.gameObject.AddComponent<UIGradient>().Set(new Color(1f, 1f, 1f, 0.45f), new Color(1f, 1f, 1f, 0.85f));
                    var rim = Img("Rim", root, TiltwellVisuals.Ring, new Color(1f, 1f, 1f, 0.55f));
                    Stretch(rim.rectTransform);
                    var dot = Img("Dot", root, sprites.Circle, new Color(1f, 1f, 1f, 0.8f));
                    dot.rectTransform.sizeDelta = Vector2.one * size * 0.1f;
                    dot.rectTransform.anchoredPosition = new Vector2(size * 0.2f, -size * 0.16f);
                    break;
                default:
                    body = Img("Body", root, bodySprite, color);
                    Stretch(body.rectTransform);
                    if (skin == DropSkin.Star) body.rectTransform.sizeDelta = Vector2.one * size * 0.12f;
                    body.gameObject.AddComponent<UIGradient>().Set(Color.white, new Color(0.78f, 0.78f, 0.84f));
                    break;
            }

            var shine = Img("Shine", root, sprites.Circle, new Color(1f, 1f, 1f, skin == DropSkin.Bubble ? 0.85f : 0.7f));
            shine.rectTransform.sizeDelta = new Vector2(size * 0.27f, size * 0.2f);
            shine.rectTransform.anchoredPosition = new Vector2(-size * 0.16f, size * 0.2f);
            shine.rectTransform.localRotation = Quaternion.Euler(0, 0, 35f);

            if (skin == DropSkin.Eyes)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    var eye = Img("Eye", root, sprites.Circle, Color.white);
                    eye.rectTransform.sizeDelta = Vector2.one * size * 0.27f;
                    eye.rectTransform.anchoredPosition = new Vector2(side * size * 0.16f, size * 0.02f);
                    var pupil = Img("Pupil", eye.rectTransform, sprites.Circle, new Color32(0x1F, 0x25, 0x44, 0xFF));
                    pupil.rectTransform.sizeDelta = Vector2.one * size * 0.14f;
                    pupil.rectTransform.anchoredPosition = new Vector2(size * 0.03f, -size * 0.03f);
                }
                shine.rectTransform.anchoredPosition = new Vector2(-size * 0.2f, size * 0.3f);
                shine.rectTransform.sizeDelta = new Vector2(size * 0.18f, size * 0.12f);
            }
            return body;
        }

        private static Image Img(string name, RectTransform parent, Sprite sprite, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        private static void Slice(Image img, DropSprites sprites, float radius)
        {
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = sprites.RoundedPx / Mathf.Max(1f, radius);
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
    }
}
