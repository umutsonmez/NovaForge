using TMPro;
using UnityEngine;

namespace NovaForge.UI
{
    public enum UIButtonStyle { Primary, Secondary, Accent, Success, Danger }

    /// <summary>
    /// Visual identity of a game: fonts, generated shape sprites and the color palette. The editor builders
    /// (NOVAFORGE ▸ UI) read it when they create views, so a game restyles every standard screen by swapping the theme.
    /// The default theme ships with the package; a game copies it and changes colors (see Tiltwell's scene builder).
    /// </summary>
    [CreateAssetMenu(menuName = "NovaForge/UI Theme", fileName = "UITheme")]
    public sealed class UITheme : ScriptableObject
    {
        [Header("Fonts")]
        public TMP_FontAsset font;
        public TMP_FontAsset displayFont;
        [Tooltip("Display font material with a soft drop shadow, for titles on busy backgrounds.")]
        public Material displayShadowMaterial;
        public TMP_FontAsset iconFont;

        [Header("Shapes (white, tinted at use)")]
        public Sprite rounded;
        public Sprite circle;
        public Sprite softShadow;
        public Sprite gloss;
        public Sprite star;

        [Header("Surfaces")]
        public Color backgroundTop = new Color32(0xF3, 0xF5, 0xFB, 0xFF);
        public Color backgroundBottom = new Color32(0xE2, 0xE8, 0xF5, 0xFF);
        public Color panelTop = Color.white;
        public Color panelBottom = new Color32(0xF3, 0xF5, 0xFA, 0xFF);
        public Color surface = new Color32(0xEC, 0xEF, 0xF6, 0xFF);
        public Color shadow = new Color(0.08f, 0.1f, 0.25f, 0.28f);
        public Color dim = new Color(0.06f, 0.08f, 0.2f, 0.62f);

        [Header("Text")]
        public Color text = new Color32(0x1F, 0x25, 0x44, 0xFF);
        public Color textMuted = new Color32(0x6B, 0x73, 0x93, 0xFF);
        public Color textOnColor = Color.white;

        [Header("Buttons (top, bottom, edge)")]
        public Color primaryTop = new Color32(0x5B, 0x96, 0xFF, 0xFF);
        public Color primaryBottom = new Color32(0x2F, 0x6B, 0xEA, 0xFF);
        public Color primaryEdge = new Color32(0x22, 0x51, 0xB8, 0xFF);
        public Color secondaryTop = Color.white;
        public Color secondaryBottom = new Color32(0xE9, 0xED, 0xF5, 0xFF);
        public Color secondaryEdge = new Color32(0xC4, 0xCB, 0xDB, 0xFF);
        public Color accentTop = new Color32(0xFF, 0xC9, 0x4B, 0xFF);
        public Color accentBottom = new Color32(0xFF, 0x9F, 0x1C, 0xFF);
        public Color accentEdge = new Color32(0xD2, 0x7A, 0x00, 0xFF);
        public Color successTop = new Color32(0x5B, 0xDE, 0x8E, 0xFF);
        public Color successBottom = new Color32(0x22, 0xB4, 0x5E, 0xFF);
        public Color successEdge = new Color32(0x17, 0x8A, 0x46, 0xFF);
        public Color dangerTop = new Color32(0xFF, 0x7B, 0x7B, 0xFF);
        public Color dangerBottom = new Color32(0xE9, 0x48, 0x48, 0xFF);
        public Color dangerEdge = new Color32(0xB8, 0x32, 0x32, 0xFF);

        [Header("Highlights")]
        public Color gold = new Color32(0xFF, 0xC8, 0x3A, 0xFF);
        public Color badge = new Color32(0xFF, 0x4D, 0x6D, 0xFF);

        public (Color top, Color bottom, Color edge, Color label) Button(UIButtonStyle style) => style switch
        {
            UIButtonStyle.Secondary => (secondaryTop, secondaryBottom, secondaryEdge, text),
            UIButtonStyle.Accent => (accentTop, accentBottom, accentEdge, textOnColor),
            UIButtonStyle.Success => (successTop, successBottom, successEdge, textOnColor),
            UIButtonStyle.Danger => (dangerTop, dangerBottom, dangerEdge, textOnColor),
            _ => (primaryTop, primaryBottom, primaryEdge, textOnColor),
        };
    }
}
