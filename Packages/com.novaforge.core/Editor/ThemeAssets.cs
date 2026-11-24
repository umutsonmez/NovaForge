using System;
using System.IO;
using NovaForge.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace NovaForge.Editor
{
    /// <summary>
    /// Generates the default theme: shape sprites (signed-distance math, supersampled), TMP font assets from the
    /// bundled fonts, and DefaultTheme.asset. Outputs are committed to the package; re-run only to change them.
    /// NOVAFORGE ▸ UI ▸ Regenerate Default Theme Assets.
    /// </summary>
    public static class ThemeAssets
    {
        public const string Dir = "Packages/com.novaforge.core/Runtime/UI/Theme";
        public const string DefaultThemePath = Dir + "/DefaultTheme.asset";
        private const string SpriteDir = Dir + "/Sprites";
        private const string FontDir = Dir + "/Fonts";

        public static UITheme LoadDefault() => AssetDatabase.LoadAssetAtPath<UITheme>(DefaultThemePath);

        [MenuItem("NOVAFORGE/UI/Regenerate Default Theme Assets", priority = 100)]
        public static void Generate()
        {
            Directory.CreateDirectory(SpriteDir);
            // Rounded rect: radius 48 in a 128 texture, 9-slice border 52 (radius + AA margin).
            var rounded = SpriteAsset("Rounded", 128, 52, (x, y) => Coverage(x, y, 128, p => RoundedBox(p, 64, 64, 48)));
            var circle = SpriteAsset("Circle", 256, 0, (x, y) => Coverage(x, y, 256, p => p.magnitude - 127f));
            var shadow = SpriteAsset("SoftShadow", 128, 60, (x, y) =>
            {
                float d = RoundedBox(new Vector2(x + 0.5f - 64, y + 0.5f - 64), 40, 40, 30);
                return 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-14f, 22f, d));
            });
            var gloss = SpriteAsset("Gloss", 128, 52, (x, y) =>
            {
                float shape = Coverage(x, y, 128, p => RoundedBox(p, 64, 64, 48));
                float k = Mathf.Clamp01((y / 128f - 0.42f) / 0.58f);
                return shape * k * k * 0.55f;
            });
            var star = SpriteAsset("Star", 256, 0, (x, y) => Coverage(x, y, 256, StarDistance));

            Directory.CreateDirectory(FontDir);
            var body = FontAsset("Nunito-Bold.ttf", "Nunito-Bold SDF");
            var display = FontAsset("Nunito-Black.ttf", "Nunito-Black SDF");
            var icons = FontAsset("MaterialIconsRound-Regular.otf", "MaterialIconsRound SDF");
            var shadowMat = ShadowMaterial(display);

            var theme = LoadDefault();
            bool created = theme == null;
            if (created) theme = ScriptableObject.CreateInstance<UITheme>();
            theme.font = body;
            theme.displayFont = display;
            theme.displayShadowMaterial = shadowMat;
            theme.iconFont = icons;
            theme.rounded = rounded;
            theme.circle = circle;
            theme.softShadow = shadow;
            theme.gloss = gloss;
            theme.star = star;
            if (created) AssetDatabase.CreateAsset(theme, DefaultThemePath);
            EditorUtility.SetDirty(theme);
            AssetDatabase.SaveAssets();
            Debug.Log("[NovaForge] Default theme assets generated.");
        }

        // ------------------------------------------------------------------ shapes

        private static float RoundedBox(Vector2 p, float halfW, float halfH, float r)
        {
            var q = new Vector2(Mathf.Abs(p.x) - halfW + r, Mathf.Abs(p.y) - halfH + r);
            return Vector2.Max(q, Vector2.zero).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - r;
        }

        private static float StarDistance(Vector2 p)
        {
            // Inigo Quilez's 5-point star SDF, slightly rounded.
            const float r = 116f, rf = 0.56f, round = 12f;
            var k1 = new Vector2(0.809016994375f, -0.587785252292f);
            var k2 = new Vector2(-k1.x, k1.y);
            p.y += 10f; // texture y points up; nudge down so the visual mass is centred
            p.x = Mathf.Abs(p.x);
            p -= 2f * Mathf.Max(Vector2.Dot(k1, p), 0f) * k1;
            p -= 2f * Mathf.Max(Vector2.Dot(k2, p), 0f) * k2;
            p.x = Mathf.Abs(p.x);
            p.y -= r - round;
            var ba = rf * new Vector2(-k1.y, k1.x) - new Vector2(0, 1);
            float h = Mathf.Clamp(Vector2.Dot(p, ba) / Vector2.Dot(ba, ba), 0f, r - round);
            return (p - ba * h).magnitude * Mathf.Sign(p.y * ba.x - p.x * ba.y) - round;
        }

        /// <summary>4x4 supersampled coverage of a signed distance field centred in a size x size texture.</summary>
        private static float Coverage(int x, int y, int size, Func<Vector2, float> sdf)
        {
            int hits = 0;
            float c = size / 2f;
            for (int sy = 0; sy < 4; sy++)
                for (int sx = 0; sx < 4; sx++)
                    if (sdf(new Vector2(x + (sx + 0.5f) / 4f - c, y + (sy + 0.5f) / 4f - c)) <= 0f) hits++;
            return hits / 16f;
        }

        private static Sprite SpriteAsset(string name, int size, int border, Func<int, int, float> alpha)
        {
            var path = $"{SpriteDir}/{name}.png";
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    px[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(255f * Mathf.Clamp01(alpha(x, y))));
            tex.SetPixels32(px);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            importer.spriteBorder = new Vector4(border, border, border, border);
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        // ------------------------------------------------------------------ fonts

        private static TMP_FontAsset FontAsset(string file, string name)
        {
            var path = $"{FontDir}/{name}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (existing != null) return existing;
            var font = AssetDatabase.LoadAssetAtPath<Font>($"{FontDir}/{file}") ?? throw new FileNotFoundException(file);
            var fa = TMP_FontAsset.CreateFontAsset(font, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
            fa.name = name;
            AssetDatabase.CreateAsset(fa, path);
            fa.atlasTextures[0].name = name + " Atlas";
            AssetDatabase.AddObjectToAsset(fa.atlasTextures[0], fa);
            fa.material.name = name + " Material";
            AssetDatabase.AddObjectToAsset(fa.material, fa);
            EditorUtility.SetDirty(fa);
            AssetDatabase.SaveAssets();
            return fa;
        }

        private static Material ShadowMaterial(TMP_FontAsset fa)
        {
            var path = $"{FontDir}/{fa.name} Shadow.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(fa.material) { name = fa.name + " Shadow" };
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.CopyPropertiesFromMaterial(fa.material);
            mat.EnableKeyword("UNDERLAY_ON");
            mat.SetColor("_UnderlayColor", new Color(0.05f, 0.07f, 0.25f, 0.35f));
            mat.SetFloat("_UnderlayOffsetX", 0f);
            mat.SetFloat("_UnderlayOffsetY", -0.55f);
            mat.SetFloat("_UnderlayDilate", 0.15f);
            mat.SetFloat("_UnderlaySoftness", 0.35f);
            EditorUtility.SetDirty(mat);
            return mat;
        }
    }
}
