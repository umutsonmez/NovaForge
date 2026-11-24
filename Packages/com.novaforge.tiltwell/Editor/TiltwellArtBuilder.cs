using System.IO;
using NovaForge.Editor;
using NovaForge.Tiltwell.Unity;
using NovaForge.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace NovaForge.Tiltwell.Editor
{
    /// <summary>
    /// Renders the app icon and the store feature graphic with the game's own drop and board visuals, so the art always
    /// matches the game. Writes the icon into the project (and sets it as the default player icon) and the store files
    /// into <c>docs/store/</c> of the repository.
    /// </summary>
    public static class TiltwellArtBuilder
    {
        public const string IconPath = "Assets/Tiltwell/Art/AppIcon.png";

        private static UITheme T => UIMenu.Theme;
        private static readonly Color SkyTop = new Color32(0x6F, 0xB6, 0xFF, 0xFF);
        private static readonly Color SkyBottom = new Color32(0x8B, 0x5C, 0xF6, 0xFF);

        [MenuItem("NOVAFORGE/Tiltwell/Regenerate App Icon and Store Art")]
        public static void Build()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            string storeDir = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../docs/store"));
            Directory.CreateDirectory(storeDir);
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(IconPath)));

            var icon = Render(1024, 1024, BuildIcon);
            File.WriteAllBytes(Path.GetFullPath(IconPath), icon.EncodeToPNG());
            File.WriteAllBytes(Path.Combine(storeDir, "icon-512.png"), Scale(icon, 512).EncodeToPNG());
            File.WriteAllBytes(Path.Combine(storeDir, "feature-1024x500.png"), Render(1024, 500, BuildFeature).EncodeToPNG());

            AssetDatabase.ImportAsset(IconPath);
            var importer = (TextureImporter)AssetImporter.GetAtPath(IconPath);
            importer.textureType = TextureImporterType.Default;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            var asset = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
            PlayerSettings.SetIcons(UnityEditor.Build.NamedBuildTarget.Unknown, new[] { asset }, IconKind.Any);
            AssetDatabase.SaveAssets();
            Debug.Log("[Tiltwell] Art written: " + IconPath + " and " + storeDir);
        }

        // ---------------------------------------------------------------- compositions

        private static void BuildIcon(RectTransform root)
        {
            Background(root);
            var board = Board(root, new Vector2(0, -20), 660, -8f);
            // Purple well in the bottom-right cell, a googly purple drop heading for it, red + blue drops behind.
            Well(board, Cell(2, 2), 150, TiltwellVisuals.Of(Paint.Purple));
            Trail(board, Cell(0, 1), Cell(1, 1), TiltwellVisuals.Of(Paint.Purple));
            Drop(board, Cell(1, 1), 190, Paint.Purple, DropSkin.Eyes);
            Drop(board, Cell(0, 0), 140, Paint.Red, DropSkin.Classic);
            Drop(board, Cell(2, 0), 140, Paint.Blue, DropSkin.Classic);
        }

        private static void BuildFeature(RectTransform root)
        {
            Background(root);
            var logo = UIMenu.Text(root, "Logo", "Tiltwell", 150, FontStyles.Bold, 220);
            logo.color = Color.white;
            logo.enableAutoSizing = false;
            logo.textWrappingMode = TextWrappingModes.NoWrap;
            logo.characterSpacing = -2f;
            logo.alignment = TextAlignmentOptions.Center;
            UIMenu.Place(logo.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(620, 220));
            logo.rectTransform.anchoredPosition = new Vector2(-200, 60);
            var shadowMat = T.displayShadowMaterial;
            if (shadowMat != null && logo.font == T.displayFont) logo.fontSharedMaterial = shadowMat;
            var tag = UIMenu.Text(root, "Tagline", "Tilt  ·  Mix  ·  Match", 56, FontStyles.Bold, 80);
            tag.color = new Color(1f, 1f, 1f, 0.92f);
            tag.enableAutoSizing = false;
            tag.textWrappingMode = TextWrappingModes.NoWrap;
            tag.alignment = TextAlignmentOptions.Center;
            UIMenu.Place(tag.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(600, 80));
            tag.rectTransform.anchoredPosition = new Vector2(-200, -80);

            var board = Board(root, new Vector2(300, 0), 360, 10f);
            Well(board, Cell(2, 2), 150, TiltwellVisuals.Of(Paint.Orange));
            Drop(board, Cell(1, 1), 190, Paint.Orange, DropSkin.Eyes);
            Drop(board, Cell(0, 0), 140, Paint.Red, DropSkin.Classic);
            Drop(board, Cell(2, 0), 140, Paint.Yellow, DropSkin.Classic);
        }

        // ---------------------------------------------------------------- pieces

        private static void Background(RectTransform root)
        {
            var bg = UIMenu.Img(root, "Background", null, Color.white);
            Stretch(bg.rectTransform);
            bg.gameObject.AddComponent<UIGradient>().Set(SkyTop, SkyBottom);
            var rnd = new System.Random(7);
            var size = root.rect.size;
            for (int i = 0; i < 7; i++)
            {
                float d = (float)(0.18 + rnd.NextDouble() * 0.3) * size.y;
                var c = UIMenu.Img(root, "Bubble", T.circle, new Color(1, 1, 1, 0.1f));
                UIMenu.Place(c.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(d, d));
                c.rectTransform.anchoredPosition = new Vector2((float)(rnd.NextDouble() - 0.5) * size.x, (float)(rnd.NextDouble() - 0.5) * size.y);
            }
        }

        /// <summary>White rounded board with a 3 × 3 floor; children use a 600 × 600 local space (see <see cref="Cell"/>).</summary>
        private static RectTransform Board(RectTransform root, Vector2 pos, float size, float angle)
        {
            var holder = new GameObject("Board", typeof(RectTransform)).transform as RectTransform;
            holder.SetParent(root, false);
            holder.sizeDelta = new Vector2(600, 600);
            holder.anchoredPosition = pos;
            holder.localScale = Vector3.one * (size / 600f);
            holder.localRotation = Quaternion.Euler(0, 0, angle);

            var shadow = UIMenu.Img(holder, "Shadow", T.rounded, new Color(0.12f, 0.1f, 0.35f, 0.3f), 90);
            Stretch(shadow.rectTransform);
            shadow.rectTransform.anchoredPosition = new Vector2(0, -26);
            var face = UIMenu.Img(holder, "Face", T.rounded, Color.white, 90);
            Stretch(face.rectTransform);
            for (int y = 0; y < 3; y++)
            for (int x = 0; x < 3; x++)
            {
                var tile = UIMenu.Img(holder, "Tile", T.rounded, (x + y) % 2 == 0 ? TiltwellVisuals.Floor : TiltwellVisuals.FloorAlt, 34);
                tile.rectTransform.sizeDelta = new Vector2(168, 168);
                tile.rectTransform.anchoredPosition = Cell(x, y);
            }
            return holder;
        }

        private static Vector2 Cell(int x, int y) => new Vector2((x - 1) * 180f, (1 - y) * 180f);

        private static void Well(RectTransform board, Vector2 pos, float size, Color color)
        {
            var ring = UIMenu.Img(board, "Well", TiltwellVisuals.Ring, color);
            ring.rectTransform.sizeDelta = Vector2.one * size;
            ring.rectTransform.anchoredPosition = pos;
            var inner = UIMenu.Img(board, "WellInner", T.circle, new Color(color.r, color.g, color.b, 0.3f));
            inner.rectTransform.sizeDelta = Vector2.one * size * 0.62f;
            inner.rectTransform.anchoredPosition = pos;
        }

        private static void Trail(RectTransform board, Vector2 from, Vector2 to, Color color)
        {
            for (int i = 1; i <= 3; i++)
            {
                float t = i / 4f;
                var dot = UIMenu.Img(board, "Trail", T.circle, new Color(color.r, color.g, color.b, 0.12f + 0.1f * i));
                dot.rectTransform.sizeDelta = Vector2.one * (50 + 22 * i);
                dot.rectTransform.anchoredPosition = Vector2.Lerp(from, to, t * 0.8f);
            }
        }

        private static void Drop(RectTransform board, Vector2 pos, float size, Paint paint, DropSkin skin)
        {
            var root = new GameObject("Drop", typeof(RectTransform)).transform as RectTransform;
            root.SetParent(board, false);
            root.sizeDelta = Vector2.one * size;
            root.anchoredPosition = pos;
            DropVisual.Create(root, size, TiltwellVisuals.Of(paint), skin, new DropSprites(T.circle, T.rounded, 48f, T.star));
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        // ---------------------------------------------------------------- rendering

        private static Texture2D Render(int w, int h, System.Action<RectTransform> build)
        {
            var camGo = new GameObject("ArtCamera", typeof(Camera));
            var cam = camGo.GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.clear;
            cam.orthographic = true;
            var canvasGo = new GameObject("ArtCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            var canvas = canvasGo.GetComponent<Canvas>();
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 8 };
            cam.targetTexture = rt;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 5f;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1f;
            var root = (RectTransform)canvasGo.transform;
            Canvas.ForceUpdateCanvases();
            build(root);
            foreach (var t in canvasGo.GetComponentsInChildren<TMP_Text>(true)) t.ForceMeshUpdate(true, true);
            Canvas.ForceUpdateCanvases();
            cam.Render();

            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            cam.targetTexture = null;
            Object.DestroyImmediate(canvasGo);
            Object.DestroyImmediate(camGo);
            rt.Release();
            return tex;
        }

        private static Texture2D Scale(Texture2D src, int size)
        {
            var rt = RenderTexture.GetTemporary(size, size, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(src, rt);
            RenderTexture.active = rt;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, size, size), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(rt);
            return tex;
        }
    }
}
