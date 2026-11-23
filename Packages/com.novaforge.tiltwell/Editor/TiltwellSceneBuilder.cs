using System.IO;
using NovaForge.Core.Localization;
using NovaForge.Core.UI;
using NovaForge.Editor;
using NovaForge.Tiltwell.Unity;
using NovaForge.UI;
using NovaForge.Unity.Audio;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace NovaForge.Tiltwell.Editor
{
    /// <summary>
    /// NOVAFORGE ▸ Tiltwell ▸ Build Game Scene: generates the whole playable scene from code (no hand-made scene to
    /// drift out of sync), saves it to Assets/Tiltwell/Tiltwell.unity and makes it the only build scene.
    /// </summary>
    public static class TiltwellSceneBuilder
    {
        public const string ScenePath = "Assets/Tiltwell/Tiltwell.unity";
        public const string LevelsPath = "Packages/com.novaforge.tiltwell/Runtime/Levels/levels.json";

        [MenuItem("NOVAFORGE/Tiltwell/Build Game Scene", priority = 0)]
        public static void BuildScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cam = new GameObject("Main Camera", typeof(Camera)).GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = TiltwellVisuals.Background;
            cam.orthographic = true;
            cam.tag = "MainCamera";
            cam.gameObject.AddComponent<AudioListener>();

            var appGo = new GameObject("Tiltwell");
            appGo.SetActive(false); // assign fields before Awake runs
            var app = appGo.AddComponent<TiltwellApp>();
            var appSo = new SerializedObject(app);
            appSo.FindProperty("levelPack").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TextAsset>(LevelsPath)
                ?? throw new FileNotFoundException(LevelsPath);
            appSo.ApplyModifiedPropertiesWithoutUndo();
            appGo.AddComponent<AudioService>();
            appGo.SetActive(true);

            var root = UIMenu.CreateUIRoot();
            var bg = new GameObject("Background", typeof(RectTransform), typeof(Image));
            UIMenu.Stretch(bg, root.transform);
            bg.transform.SetAsFirstSibling();
            bg.GetComponent<Image>().color = TiltwellVisuals.Background;

            BuildMainMenu();
            BuildLevelSelect();
            BuildGameplay();
            UIMenu.CreateShopScreen();
            UIMenu.CreateSettingsPopup();
            UIMenu.CreateConfirmPopup();
            UIMenu.CreateLevelResultPopup();
            UIMenu.CreateDailyRewardPopup();

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

            PlayerSettings.productName = "Tiltwell";
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            Debug.Log($"[Tiltwell] Scene built: {ScenePath}");
        }

        // ------------------------------------------------------------------ views

        private static void BuildMainMenu()
        {
            var view = UIMenu.CreateView<MainMenuView>("MainMenu", UIViewKind.Screen, ViewIds.MainMenu);
            var layout = UIMenu.Vertical(view.transform, 80, 40);
            ((VerticalLayoutGroup)layout.GetComponent<VerticalLayoutGroup>()).childAlignment = TextAnchor.MiddleCenter;

            var currencies = Row(layout, 110);
            Currency(currencies, "coin");
            Currency(currencies, TiltwellIds.Hint);

            Spacer(layout, 200);
            var title = UIMenu.Text(layout, "Title", "Tiltwell", 150, FontStyles.Bold, 260);
            UIMenu.Localized(title, TiltwellStrings.Title);
            DropRow(layout);
            Spacer(layout, 140);

            var play = UIMenu.Button(layout, "Play", null, out var playLabel);
            play.GetComponent<LayoutElement>().preferredHeight = 180;
            playLabel.fontSize = 64;
            Tint(play, Paint.Green);
            var levels = UIMenu.Button(layout, "Levels", TiltwellStrings.Levels, out _);
            var shop = UIMenu.Button(layout, "Shop", CoreStrings.ShopTitle, out _);
            var settings = UIMenu.Button(layout, "Settings", CoreStrings.SettingsTitle, out _);

            var so = new SerializedObject(view);
            so.FindProperty("playLabel").objectReferenceValue = playLabel;
            so.FindProperty("playButton").objectReferenceValue = play;
            so.FindProperty("levelsButton").objectReferenceValue = levels;
            so.FindProperty("shopButton").objectReferenceValue = shop;
            so.FindProperty("settingsButton").objectReferenceValue = settings;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void BuildLevelSelect()
        {
            var view = UIMenu.CreateView<LevelSelectView>("LevelSelect", UIViewKind.Screen, TiltwellViewIds.Levels);
            var layout = UIMenu.Vertical(view.transform, 48, 24);
            UIMenu.Localized(UIMenu.Text(layout, "Title", "Levels", 80, FontStyles.Bold, 140), TiltwellStrings.Levels);

            var scrollGo = DefaultControls.CreateScrollView(UIMenu.UiResources());
            scrollGo.name = "Grid";
            scrollGo.transform.SetParent(layout, false);
            scrollGo.AddComponent<LayoutElement>().flexibleHeight = 1;
            var scroll = scrollGo.GetComponent<ScrollRect>();
            scroll.horizontal = false;
            scrollGo.GetComponent<Image>().color = Color.clear;
            if (scroll.horizontalScrollbar != null) Object.DestroyImmediate(scroll.horizontalScrollbar.gameObject);
            scroll.horizontalScrollbar = null;
            var content = scroll.content;
            var grid = content.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(170, 190);
            grid.spacing = new Vector2(24, 24);
            grid.padding = new RectOffset(16, 16, 16, 16);
            grid.childAlignment = TextAnchor.UpperCenter;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 5;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var cell = UIMenu.Button(content, "LevelTemplate", null, out var label);
            label.fontSize = 64;
            label.fontStyle = FontStyles.Bold;
            Tint(cell, Paint.Blue);
            var stars = new GameObject("Stars", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            stars.transform.SetParent(cell.transform, false);
            var srt = (RectTransform)stars.transform;
            srt.anchorMin = new Vector2(0, 0);
            srt.anchorMax = new Vector2(1, 0);
            srt.pivot = new Vector2(0.5f, 0);
            srt.sizeDelta = new Vector2(0, 40);
            srt.anchoredPosition = new Vector2(0, 12);
            var h = stars.GetComponent<HorizontalLayoutGroup>();
            h.childAlignment = TextAnchor.MiddleCenter;
            h.spacing = 6;
            h.childControlWidth = h.childControlHeight = false;
            for (int i = 1; i <= 3; i++)
            {
                var s = new GameObject("Star" + i, typeof(RectTransform), typeof(Image));
                s.transform.SetParent(stars.transform, false);
                ((RectTransform)s.transform).sizeDelta = new Vector2(32, 32);
                var img = s.GetComponent<Image>();
                img.sprite = TiltwellVisualsEditor.CircleAsset();
                img.color = TiltwellVisuals.Of(Paint.Yellow);
            }
            label.rectTransform.offsetMin = new Vector2(0, 40);

            var back = UIMenu.Button(layout, "Back", CoreStrings.Close, out _);
            var so = new SerializedObject(view);
            so.FindProperty("cellTemplate").objectReferenceValue = cell;
            so.FindProperty("backButton").objectReferenceValue = back;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void BuildGameplay()
        {
            var view = UIMenu.CreateView<GameplayView>("Gameplay", UIViewKind.Screen, ViewIds.Gameplay);
            var t = view.transform;

            // Full-screen swipe catcher behind everything else.
            var catcher = new GameObject("SwipeArea", typeof(RectTransform), typeof(Image), typeof(SwipeInput));
            UIMenu.Stretch(catcher, t);
            catcher.GetComponent<Image>().color = new Color(0, 0, 0, 0);

            var home = Anchored(UIMenu.Button(t, "Home", CoreStrings.Home, out _).gameObject, new Vector2(0, 1), new Vector2(40, -40), new Vector2(260, 120));
            var level = UIMenu.Text(t, "Level", "Level 1", 60, FontStyles.Bold, 120);
            Anchored(level.gameObject, new Vector2(0.5f, 1), new Vector2(0, -40), new Vector2(500, 120));
            var hint = UIMenu.Button(t, "Hint", TiltwellStrings.Hint, out _);
            Anchored(hint.gameObject, new Vector2(1, 1), new Vector2(-40, -40), new Vector2(260, 120));
            Tint(hint, Paint.Yellow);
            var hintCount = UIMenu.Text(hint.transform, "Count", "3", 34, FontStyles.Bold, 50);
            Anchored(hintCount.gameObject, new Vector2(1, 1), new Vector2(10, 10), new Vector2(64, 64));
            var moves = UIMenu.Text(t, "Moves", "Moves 0/10", 44, FontStyles.Normal, 80);
            Anchored(moves.gameObject, new Vector2(0.5f, 1), new Vector2(0, -180), new Vector2(700, 80));

            var boardGo = new GameObject("Board", typeof(RectTransform), typeof(BoardView));
            boardGo.transform.SetParent(t, false);
            var brt = (RectTransform)boardGo.transform;
            brt.anchorMin = new Vector2(0, 0);
            brt.anchorMax = new Vector2(1, 1);
            brt.offsetMin = new Vector2(40, 400);
            brt.offsetMax = new Vector2(-40, -300);

            var arrow = UIMenu.Text(t, "HintArrow", ">", 400, FontStyles.Bold, 400);
            arrow.color = new Color(1, 1, 1, 0.55f);
            arrow.raycastTarget = false;
            Anchored(arrow.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0, 50), new Vector2(400, 400));
            arrow.gameObject.SetActive(false);

            var message = UIMenu.Text(t, "Message", "", 40, FontStyles.Italic, 150);
            message.raycastTarget = false;
            Anchored(message.gameObject, new Vector2(0.5f, 0), new Vector2(0, 220), new Vector2(960, 150));

            var undo = UIMenu.Button(t, "Undo", TiltwellStrings.Undo, out _);
            Anchored(undo.gameObject, new Vector2(0.5f, 0), new Vector2(-220, 60), new Vector2(380, 140));
            var restart = UIMenu.Button(t, "Restart", TiltwellStrings.Restart, out _);
            Anchored(restart.gameObject, new Vector2(0.5f, 0), new Vector2(220, 60), new Vector2(380, 140));

            var so = new SerializedObject(view);
            so.FindProperty("board").objectReferenceValue = boardGo.GetComponent<BoardView>();
            so.FindProperty("swipe").objectReferenceValue = catcher.GetComponent<SwipeInput>();
            so.FindProperty("levelLabel").objectReferenceValue = level;
            so.FindProperty("movesLabel").objectReferenceValue = moves;
            so.FindProperty("messageLabel").objectReferenceValue = message;
            so.FindProperty("hintCountLabel").objectReferenceValue = hintCount;
            so.FindProperty("homeButton").objectReferenceValue = home.GetComponent<Button>();
            so.FindProperty("undoButton").objectReferenceValue = undo;
            so.FindProperty("restartButton").objectReferenceValue = restart;
            so.FindProperty("hintButton").objectReferenceValue = hint;
            so.FindProperty("hintArrow").objectReferenceValue = arrow.rectTransform;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ------------------------------------------------------------------ helpers

        private static GameObject Anchored(GameObject go, Vector2 anchor, Vector2 position, Vector2 size)
        {
            var rt = (RectTransform)go.transform;
            if (go.TryGetComponent<LayoutElement>(out var le)) le.ignoreLayout = true;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            return go;
        }

        private static Transform Row(Transform parent, float height)
        {
            var row = new GameObject("Row", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            row.transform.SetParent(parent, false);
            row.GetComponent<LayoutElement>().preferredHeight = height;
            var h = row.GetComponent<HorizontalLayoutGroup>();
            h.spacing = 40;
            h.childAlignment = TextAnchor.MiddleCenter;
            h.childControlWidth = h.childControlHeight = true;
            h.childForceExpandWidth = true;
            return row.transform;
        }

        private static void Currency(Transform parent, string resourceId)
        {
            var text = UIMenu.Text(parent, resourceId, "0", 52, FontStyles.Bold, 110);
            text.gameObject.AddComponent<CurrencyDisplay>();
            var so = new SerializedObject(text.GetComponent<CurrencyDisplay>());
            so.FindProperty("resourceId").stringValue = resourceId;
            so.ApplyModifiedPropertiesWithoutUndo();
            var dot = new GameObject("Icon", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            dot.transform.SetParent(text.transform, false);
            dot.GetComponent<LayoutElement>().ignoreLayout = true;
            var rt = (RectTransform)dot.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0, 0.5f);
            rt.sizeDelta = new Vector2(56, 56);
            rt.anchoredPosition = new Vector2(60, 0);
            dot.GetComponent<Image>().sprite = TiltwellVisualsEditor.CircleAsset();
            dot.GetComponent<Image>().color = TiltwellVisuals.Of(resourceId == "coin" ? Paint.Yellow : Paint.Purple);
        }

        private static void DropRow(Transform parent)
        {
            var row = Row(parent, 90);
            row.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = false;
            row.GetComponent<HorizontalLayoutGroup>().childControlWidth = false;
            row.GetComponent<HorizontalLayoutGroup>().childControlHeight = false;
            foreach (var p in new[] { Paint.Red, Paint.Orange, Paint.Yellow, Paint.Green, Paint.Blue, Paint.Purple })
            {
                var d = new GameObject(p.ToString(), typeof(RectTransform), typeof(Image));
                d.transform.SetParent(row, false);
                ((RectTransform)d.transform).sizeDelta = new Vector2(70, 70);
                d.GetComponent<Image>().sprite = TiltwellVisualsEditor.CircleAsset();
                d.GetComponent<Image>().color = TiltwellVisuals.Of(p);
            }
        }

        private static void Spacer(Transform parent, float height)
        {
            var s = new GameObject("Spacer", typeof(RectTransform), typeof(LayoutElement));
            s.transform.SetParent(parent, false);
            s.GetComponent<LayoutElement>().preferredHeight = height;
        }

        private static void Tint(Button b, Paint p)
        {
            var img = b.GetComponent<Image>();
            if (img != null) img.color = Color.Lerp(TiltwellVisuals.Of(p), Color.white, 0.15f);
        }
    }

    /// <summary>Circle sprite saved as an asset (runtime-generated sprites can't be referenced by a saved scene).</summary>
    internal static class TiltwellVisualsEditor
    {
        private const string Path = "Assets/Tiltwell/Circle.png";

        public static Sprite CircleAsset()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(Path);
            if (existing != null) return existing;
            System.IO.Directory.CreateDirectory("Assets/Tiltwell");
            const int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float r = size / 2f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                    tex.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(r - 1 - d)));
                }
            System.IO.File.WriteAllBytes(Path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(Path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(Path);
            importer.textureType = TextureImporterType.Sprite;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(Path);
        }
    }
}
