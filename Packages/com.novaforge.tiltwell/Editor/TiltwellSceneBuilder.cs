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
        public const string ThemePath = "Assets/Tiltwell/TiltwellTheme.asset";
        public const string LevelsPath = "Packages/com.novaforge.tiltwell/Runtime/Levels/levels.json";
        public const string PrivacyPolicyUrl = "https://umutsonmez.github.io/NovaForge/tiltwell/privacy.html";
        public const string SupportEmail = "tiltwell.support@gmail.com";
        public const string BundleId = "com.umutsonmez.tiltwell";
        /// <summary>Optional AdMobSettings asset (game data, not in the package). Missing = mock ads in development.</summary>
        public const string AdMobSettingsPath = "Assets/Tiltwell/AdMobSettings.asset";

        private static UITheme T => UIMenu.Theme;

        [MenuItem("NOVAFORGE/Tiltwell/Build Game Scene", priority = 0)]
        public static void BuildScene()
        {
            UIMenu.Theme = CreateTheme();
            try { Build(); }
            finally { UIMenu.Theme = null; }
        }

        private static void Build()
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
            appSo.FindProperty("privacyPolicyUrl").stringValue = PrivacyPolicyUrl;
            appSo.FindProperty("supportEmail").stringValue = SupportEmail;
            appSo.FindProperty("adMobSettings").objectReferenceValue = AssetDatabase.LoadAssetAtPath<ScriptableObject>(AdMobSettingsPath);
            appSo.ApplyModifiedPropertiesWithoutUndo();
            var audio = appGo.AddComponent<AudioService>();
            var library = TiltwellSoundBuilder.LoadLibrary();
            if (library == null)
            {
                TiltwellSoundBuilder.Build();
                library = TiltwellSoundBuilder.LoadLibrary();
            }
            var audioSo = new SerializedObject(audio);
            audioSo.FindProperty("library").objectReferenceValue = library;
            audioSo.ApplyModifiedPropertiesWithoutUndo();
            appGo.SetActive(true);

            var root = UIMenu.CreateUIRoot();
            var bg = UIMenu.Background(root.transform);
            BackgroundBubbles(bg.transform);

            BuildMainMenu();
            BuildLevelMap();
            BuildGameplay();
            UIMenu.CreateShopScreen();
            UIMenu.CreateSettingsPopup();
            UIMenu.CreateConfirmPopup();
            UIMenu.CreateLevelResultPopup();
            UIMenu.CreateDailyRewardPopup();
            BuildColorGuide();

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

            PlayerSettings.productName = "Tiltwell";
            PlayerSettings.companyName = "Umut Sönmez";
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android, BundleId);
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.iOS, BundleId);
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            Debug.Log($"[Tiltwell] Scene built: {ScenePath}");
        }

        /// <summary>Tiltwell's theme = package default with a sky-to-lilac background (re-created every build).</summary>
        private static UITheme CreateTheme()
        {
            var baseTheme = ThemeAssets.LoadDefault() ?? throw new FileNotFoundException(ThemeAssets.DefaultThemePath);
            Directory.CreateDirectory(Path.GetDirectoryName(ThemePath));
            AssetDatabase.DeleteAsset(ThemePath);
            var theme = Object.Instantiate(baseTheme);
            theme.name = "TiltwellTheme";
            theme.backgroundTop = TiltwellVisuals.BackgroundTop;
            theme.backgroundBottom = TiltwellVisuals.BackgroundBottom;
            theme.dim = new Color(0.12f, 0.1f, 0.3f, 0.6f);
            AssetDatabase.CreateAsset(theme, ThemePath);
            AssetDatabase.SaveAssets();
            return theme;
        }

        private static void BackgroundBubbles(Transform bg)
        {
            var spots = new[]
            {
                (new Vector2(0.05f, 0.92f), 560f), (new Vector2(0.95f, 0.70f), 420f), (new Vector2(0.10f, 0.35f), 360f),
                (new Vector2(0.90f, 0.12f), 620f), (new Vector2(0.55f, 0.50f), 260f),
            };
            for (int i = 0; i < spots.Length; i++)
            {
                var (anchor, size) = spots[i];
                var c = UIMenu.Img(bg, "Bubble" + i, T.circle, new Color(1, 1, 1, 0.16f));
                c.rectTransform.anchorMin = c.rectTransform.anchorMax = anchor;
                c.rectTransform.sizeDelta = new Vector2(size, size);
                var f = c.gameObject.AddComponent<UIFloat>();
                var so = new SerializedObject(f);
                so.FindProperty("distance").floatValue = 30f;
                so.FindProperty("period").floatValue = 7f + i;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        // ------------------------------------------------------------------ main menu

        private static void BuildMainMenu()
        {
            var view = UIMenu.CreateView<MainMenuView>("MainMenu", UIViewKind.Screen, ViewIds.MainMenu);
            var t = view.transform;

            var coins = UIMenu.Chip(t, "CoinChip", UIIcons.Coin, T.accentTop, T.accentBottom, 260);
            UIMenu.Place(UIMenu.ChipRoot(coins), new Vector2(0, 1), new Vector2(36, -40), new Vector2(260, 100));
            Currency(coins, "coin");
            var hints = UIMenu.Chip(t, "HintChip", UIIcons.Lightbulb, new Color32(0xB9, 0x8C, 0xFF, 0xFF), new Color32(0x7C, 0x4D, 0xEE, 0xFF), 220);
            UIMenu.Place(UIMenu.ChipRoot(hints), new Vector2(0, 1), new Vector2(320, -40), new Vector2(220, 100));
            Currency(hints, TiltwellIds.Hint);
            var settings = UIMenu.IconButton(t, "Settings", UIIcons.Settings, UIButtonStyle.Secondary, 112);
            UIMenu.Place((RectTransform)settings.transform, new Vector2(1, 1), new Vector2(-36, -34), new Vector2(112, 112));

            // Logo with gradient and a soft drop shadow.
            var logo = UIMenu.Text(t, "Title", "Tiltwell", 200, FontStyles.Bold, 260);
            UIMenu.Localized(logo, TiltwellStrings.Title);
            UIMenu.OnColor(logo, Color.white);
            logo.enableVertexGradient = true;
            logo.colorGradient = new VertexGradient(new Color32(0x4F, 0x8C, 0xFF, 0xFF), new Color32(0x4F, 0x8C, 0xFF, 0xFF),
                new Color32(0x8B, 0x5C, 0xF6, 0xFF), new Color32(0x8B, 0x5C, 0xF6, 0xFF));
            logo.characterSpacing = -2f;
            UIMenu.Place(logo.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 560), new Vector2(1000, 260));
            logo.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            logo.rectTransform.anchoredPosition = new Vector2(0, 560);
            logo.gameObject.AddComponent<UIFloat>();

            Hero(t);

            var play = UIMenu.Button(t, "Play", null, out var playLabel, UIButtonStyle.Success, UIIcons.Play, 190);
            UIMenu.Place((RectTransform)play.transform, new Vector2(0.5f, 0), new Vector2(0, 400), new Vector2(720, 190));
            play.transform.localScale = Vector3.one;
            var pulse = play.gameObject.AddComponent<UIPulse>();
            var pso = new SerializedObject(pulse);
            pso.FindProperty("amount").floatValue = 0.035f;
            pso.ApplyModifiedPropertiesWithoutUndo();

            var levels = NavButton(t, "Levels", UIIcons.Map, UIButtonStyle.Primary, TiltwellStrings.Levels, 0);
            var puzzle = NavButton(t, "Puzzle", UIIcons.Star, UIButtonStyle.Success, TiltwellStrings.DailyPuzzle, 1);
            var puzzleBadge = UIMenu.Badge(puzzle.transform, "Badge", 64);
            var shop = NavButton(t, "Shop", UIIcons.Store, UIButtonStyle.Accent, CoreStrings.ShopTitle, 2);
            var daily = NavButton(t, "Daily", UIIcons.Gift, UIButtonStyle.Danger, CoreStrings.DailyTitle, 3);

            var so = new SerializedObject(view);
            so.FindProperty("playLabel").objectReferenceValue = playLabel;
            so.FindProperty("playButton").objectReferenceValue = play;
            so.FindProperty("levelsButton").objectReferenceValue = levels;
            so.FindProperty("shopButton").objectReferenceValue = shop;
            so.FindProperty("settingsButton").objectReferenceValue = settings;
            so.FindProperty("dailyButton").objectReferenceValue = daily;
            so.FindProperty("puzzleButton").objectReferenceValue = puzzle;
            so.FindProperty("puzzleBadge").objectReferenceValue = puzzleBadge;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Decorative mini board: tilted card with tiles, a wall, a well and droplets.</summary>
        private static void Hero(Transform parent)
        {
            const float cell = 104f;
            const int n = 4;
            var card = UIMenu.Card(parent, "Hero", new Vector2(cell * n + 60, cell * n + 60), Color.white, 56, out var face);
            UIMenu.Place((RectTransform)card, new Vector2(0.5f, 0.5f), new Vector2(0, 110), new Vector2(cell * n + 60, cell * n + 60));
            ((RectTransform)card).pivot = new Vector2(0.5f, 0.5f);
            ((RectTransform)card).anchoredPosition = new Vector2(0, 110);
            card.localRotation = Quaternion.Euler(0, 0, -7f);
            var fl = card.gameObject.AddComponent<UIFloat>();
            var fso = new SerializedObject(fl);
            fso.FindProperty("distance").floatValue = 10f;
            fso.FindProperty("rotation").floatValue = 1.5f;
            fso.ApplyModifiedPropertiesWithoutUndo();

            Vector2 At(int x, int y) => new Vector2((x - (n - 1) / 2f) * cell, ((n - 1) / 2f - y) * cell);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    bool wall = (x == 2 && y == 1) || (x == 0 && y == 3);
                    var tile = UIMenu.Img(face, "Tile", T.rounded, wall ? TiltwellVisuals.WallEdge : (x + y) % 2 == 0 ? TiltwellVisuals.Floor : TiltwellVisuals.FloorAlt, 20);
                    Center(tile.rectTransform, At(x, y), Vector2.one * (cell - 8));
                    if (!wall) continue;
                    var top = UIMenu.Img(face, "WallTop", T.rounded, Color.white, 20);
                    Center(top.rectTransform, At(x, y) + new Vector2(0, 5), new Vector2(cell - 8, cell - 18));
                    top.gameObject.AddComponent<UIGradient>().Set(TiltwellVisuals.WallTop, TiltwellVisuals.WallBottom);
                }
            var well = TiltwellVisuals.Of(Paint.Purple);
            var basin = UIMenu.Img(face, "Basin", T.circle, new Color(well.r, well.g, well.b, 0.2f));
            Center(basin.rectTransform, At(3, 0), Vector2.one * cell * 0.84f);
            var fill = UIMenu.Img(face, "WellFill", T.circle, new Color(well.r, well.g, well.b, 0.45f));
            Center(fill.rectTransform, At(3, 0), Vector2.one * cell * 0.46f);
            Drop(face, "DropRed", Paint.Red, At(1, 0), cell * 0.74f);
            Drop(face, "DropBlue", Paint.Blue, At(1, 2), cell * 0.74f);
            Drop(face, "DropYellow", Paint.Yellow, At(3, 3), cell * 0.74f);

            // Loose droplets floating around the logo.
            var floats = new[] { (Paint.Orange, new Vector2(-400, 760), 84f), (Paint.Green, new Vector2(410, 720), 70f),
                (Paint.Blue, new Vector2(-440, 380), 60f), (Paint.Red, new Vector2(430, 420), 92f), (Paint.Yellow, new Vector2(-330, -160), 66f),
                (Paint.Purple, new Vector2(360, -190), 76f) };
            foreach (var (p, pos, size) in floats)
            {
                var d = Drop(parent, "Float" + p, p, pos, size);
                d.gameObject.AddComponent<UIFloat>();
            }
        }

        private static RectTransform Drop(Transform parent, string name, Paint paint, Vector2 pos, float size)
        {
            var root = new GameObject(name, typeof(RectTransform)).transform as RectTransform;
            root.SetParent(parent, false);
            Center(root, pos, Vector2.one * size);
            var shadow = UIMenu.Img(root, "Shadow", T.circle, new Color(0.1f, 0.12f, 0.3f, 0.2f));
            UIMenu.Stretch(shadow.gameObject, root);
            shadow.rectTransform.anchoredPosition = new Vector2(0, -size * 0.08f);
            var body = UIMenu.Img(root, "Body", T.circle, TiltwellVisuals.Of(paint));
            UIMenu.Stretch(body.gameObject, root);
            body.gameObject.AddComponent<UIGradient>().Set(Color.white, new Color(0.78f, 0.78f, 0.84f));
            var shine = UIMenu.Img(root, "Shine", T.circle, new Color(1, 1, 1, 0.7f));
            Center(shine.rectTransform, new Vector2(-size * 0.16f, size * 0.2f), new Vector2(size * 0.27f, size * 0.2f));
            shine.rectTransform.localRotation = Quaternion.Euler(0, 0, 35f);
            return root;
        }

        /// <summary>Round nav button with a label under it. <paramref name="slot"/> 0..count-1 spreads them across the width.</summary>
        private static Button NavButton(Transform parent, string name, string icon, UIButtonStyle style, string key, int slot, int count = 4)
        {
            var anchor = new Vector2((slot + 0.5f) / count, 0);
            var b = UIMenu.IconButton(parent, name, icon, style, 160);
            UIMenu.Place((RectTransform)b.transform, anchor, new Vector2(0, 150), new Vector2(160, 160));
            ((RectTransform)b.transform).pivot = new Vector2(0.5f, 0);
            var label = UIMenu.Text(parent, name + "Label", name, 34, FontStyles.Bold, 60);
            UIMenu.Localized(label, key);
            label.enableAutoSizing = true;
            label.fontSizeMin = 20;
            label.fontSizeMax = 34;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            UIMenu.Place(label.rectTransform, anchor, new Vector2(0, 84), new Vector2(240, 56));
            label.rectTransform.pivot = new Vector2(0.5f, 0);
            label.rectTransform.anchorMin = new Vector2(anchor.x - 0.5f / count, 0);
            label.rectTransform.anchorMax = new Vector2(anchor.x + 0.5f / count, 0);
            label.rectTransform.sizeDelta = new Vector2(-12, 56);
            label.rectTransform.anchoredPosition = new Vector2(0, 84);
            return b;
        }

        // ------------------------------------------------------------------ level map

        private static void BuildLevelMap()
        {
            var view = UIMenu.CreateView<LevelMapView>("LevelMap", UIViewKind.Screen, TiltwellViewIds.Levels);
            var t = view.transform;

            var scrollGo = new GameObject("Map", typeof(RectTransform), typeof(ScrollRect));
            UIMenu.Stretch(scrollGo, t);
            var viewport = UIMenu.Stretch(new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D), typeof(Image)), scrollGo.transform);
            viewport.GetComponent<Image>().color = new Color(1, 1, 1, 0); // raycast target for dragging
            var content = new GameObject("Content", typeof(RectTransform)).transform as RectTransform;
            content.SetParent(viewport, false);
            content.anchorMin = new Vector2(0, 0);
            content.anchorMax = new Vector2(1, 0);
            content.pivot = new Vector2(0.5f, 0);
            content.sizeDelta = new Vector2(0, 4000);
            var scroll = scrollGo.GetComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.viewport = (RectTransform)viewport;
            scroll.content = content;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.decelerationRate = 0.12f;
            scroll.scrollSensitivity = 40f;

            // Zone behind each world.
            var zone = UIMenu.Img(content, "ZoneTemplate", T.rounded, Color.white, 90);
            zone.rectTransform.anchorMin = new Vector2(0, 0);
            zone.rectTransform.anchorMax = new Vector2(1, 0);
            zone.rectTransform.pivot = new Vector2(0.5f, 0);
            zone.rectTransform.sizeDelta = new Vector2(-70, 1000);
            zone.gameObject.AddComponent<UIGradient>();

            var roadBase = Road(content, "RoadBase", 70f, 0f, new Color(1, 1, 1, 0.92f));
            var roadTodo = Road(content, "RoadTodo", 14f, 26f, new Color32(0xBC, 0xC5, 0xDE, 0xFF));
            var roadDone = Road(content, "RoadDone", 30f, 0f, T.gold);

            // World banner.
            var banner = UIMenu.Card(content, "BannerTemplate", new Vector2(420, 104), Color.white, 52, out var bannerFace);
            var brt = (RectTransform)banner;
            brt.anchorMin = brt.anchorMax = new Vector2(0.5f, 0);
            brt.pivot = new Vector2(0.5f, 0.5f);
            bannerFace.gameObject.AddComponent<UIGradient>();
            var bannerText = UIMenu.Text(bannerFace, "Text", "World 1", 50, FontStyles.Bold, 104);
            UIMenu.Stretch(bannerText.gameObject, bannerFace);
            UIMenu.OnColor(bannerText);

            var node = NodeTemplate(content);

            // Header over the map: fade strip, back, title pill, star counter.
            var fade = UIMenu.Img(t, "HeaderFade", null, Color.white);
            fade.rectTransform.anchorMin = new Vector2(0, 1);
            fade.rectTransform.anchorMax = new Vector2(1, 1);
            fade.rectTransform.pivot = new Vector2(0.5f, 1);
            fade.rectTransform.sizeDelta = new Vector2(0, 260);
            fade.gameObject.AddComponent<UIGradient>().Set(T.backgroundTop, UIMenu.WithAlpha(T.backgroundTop, 0f));
            var back = UIMenu.IconButton(t, "Back", UIIcons.Back, UIButtonStyle.Secondary, 112);
            UIMenu.Place((RectTransform)back.transform, new Vector2(0, 1), new Vector2(36, -34), new Vector2(112, 112));
            var titleCard = UIMenu.Card(t, "TitlePill", new Vector2(440, 104), Color.white, 52, out var titleFace);
            UIMenu.Place((RectTransform)titleCard, new Vector2(0.5f, 1), new Vector2(0, -38), new Vector2(440, 104));
            var title = UIMenu.Text(titleFace, "Title", "Levels", 54, FontStyles.Bold, 104);
            UIMenu.Stretch(title.gameObject, titleFace);
            UIMenu.Localized(title, TiltwellStrings.Levels);
            var stars = UIMenu.Chip(t, "StarChip", UIIcons.Star, T.accentTop, T.accentBottom, 250);
            UIMenu.Place(UIMenu.ChipRoot(stars), new Vector2(1, 1), new Vector2(-36, -40), new Vector2(250, 100));

            var so = new SerializedObject(view);
            so.FindProperty("scroll").objectReferenceValue = scroll;
            so.FindProperty("content").objectReferenceValue = content;
            so.FindProperty("nodeTemplate").objectReferenceValue = node;
            so.FindProperty("bannerTemplate").objectReferenceValue = brt;
            so.FindProperty("zoneTemplate").objectReferenceValue = zone.rectTransform;
            so.FindProperty("roadBase").objectReferenceValue = roadBase;
            so.FindProperty("roadDone").objectReferenceValue = roadDone;
            so.FindProperty("roadTodo").objectReferenceValue = roadTodo;
            so.FindProperty("backButton").objectReferenceValue = back;
            so.FindProperty("starsLabel").objectReferenceValue = stars;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static UIPolyline Road(Transform content, string name, float thickness, float dash, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(UIPolyline));
            var rt = (RectTransform)go.transform;
            rt.SetParent(content, false);
            // Stretch over the content (so RectMask2D never culls it); pivot bottom-centre = map coordinates.
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var line = go.GetComponent<UIPolyline>();
            line.color = color;
            line.raycastTarget = false;
            line.Thickness = thickness;
            line.Dash = dash;
            return line;
        }

        private static LevelMapNode NodeTemplate(Transform content)
        {
            const float size = 156f;
            var go = new GameObject("NodeTemplate", typeof(RectTransform), typeof(Button), typeof(LevelMapNode));
            var rt = (RectTransform)go.transform;
            rt.SetParent(content, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(size, size);

            var ring = UIMenu.Img(rt, "CurrentRing", T.circle, new Color(1, 1, 1, 0.55f));
            Center(ring.rectTransform, Vector2.zero, Vector2.one * (size + 64));
            var rp = ring.gameObject.AddComponent<UIPulse>();
            var rpo = new SerializedObject(rp);
            rpo.FindProperty("amount").floatValue = 0.12f;
            rpo.FindProperty("period").floatValue = 1.2f;
            rpo.ApplyModifiedPropertiesWithoutUndo();

            var shadow = UIMenu.Img(rt, "Shadow", T.circle, new Color(0.1f, 0.12f, 0.3f, 0.25f));
            Center(shadow.rectTransform, new Vector2(0, -12), Vector2.one * size);
            var edge = UIMenu.Img(rt, "Edge", T.circle, Color.white);
            UIMenu.Stretch(edge.gameObject, rt);
            edge.raycastTarget = true;
            var face = UIMenu.Img(rt, "Face", T.circle, Color.white);
            UIMenu.Stretch(face.gameObject, rt);
            face.rectTransform.offsetMin = new Vector2(4, 14);
            face.rectTransform.offsetMax = new Vector2(-4, 0);
            var grad = face.gameObject.AddComponent<UIGradient>();
            var gloss = UIMenu.Img(face.transform, "Gloss", T.circle, new Color(1, 1, 1, 0.28f));
            Center(gloss.rectTransform, new Vector2(0, size * 0.2f), new Vector2(size * 0.62f, size * 0.34f));
            var label = UIMenu.Text(face.transform, "Number", "1", 66, FontStyles.Bold, size);
            UIMenu.Stretch(label.gameObject, face.transform);
            UIMenu.OnColor(label);
            var lockIcon = UIMenu.Icon(face.transform, "Lock", UIIcons.Lock, 70, new Color(1, 1, 1, 0.95f));
            UIMenu.Stretch(lockIcon.gameObject, face.transform);

            var starRow = new GameObject("Stars", typeof(RectTransform)).transform as RectTransform;
            starRow.SetParent(rt, false);
            Center(starRow, new Vector2(0, -size * 0.5f - 18), new Vector2(200, 60));
            var stars = new Image[3];
            for (int i = 0; i < 3; i++)
            {
                float sz = i == 1 ? 58f : 48f;
                var st = UIMenu.Img(starRow, "Star" + (i + 1), T.star, Color.white);
                Center(st.rectTransform, new Vector2((i - 1) * 54f, i == 1 ? 4f : -4f), new Vector2(sz, sz));
                st.rectTransform.localRotation = Quaternion.Euler(0, 0, (1 - i) * 12f);
                st.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(0, -3);
                stars[i] = st;
            }

            // "You are here" pin above the current node.
            var marker = new GameObject("Marker", typeof(RectTransform)).transform as RectTransform;
            marker.SetParent(rt, false);
            Center(marker, new Vector2(0, size * 0.5f + 86), new Vector2(110, 110));
            UIMenu.IconBubble(marker, "Pin", UIIcons.Drop, T.accentTop, T.accentBottom, 110);
            marker.gameObject.AddComponent<UIFloat>();

            var button = go.GetComponent<Button>();
            button.targetGraphic = face;
            var colors = button.colors;
            colors.pressedColor = new Color(0.9f, 0.9f, 0.93f);
            colors.disabledColor = Color.white; // locked look comes from LevelMapNode colors
            button.colors = colors;
            var fb = go.AddComponent<UIButtonFeedback>();
            var fso = new SerializedObject(fb);
            fso.FindProperty("face").objectReferenceValue = face.rectTransform;
            fso.ApplyModifiedPropertiesWithoutUndo();

            var n = go.GetComponent<LevelMapNode>();
            var so = new SerializedObject(n);
            so.FindProperty("label").objectReferenceValue = label;
            so.FindProperty("edge").objectReferenceValue = edge;
            so.FindProperty("face").objectReferenceValue = grad;
            so.FindProperty("lockIcon").objectReferenceValue = lockIcon.gameObject;
            var sp = so.FindProperty("stars");
            sp.arraySize = 3;
            for (int i = 0; i < 3; i++) sp.GetArrayElementAtIndex(i).objectReferenceValue = stars[i];
            so.FindProperty("currentRing").objectReferenceValue = ring.gameObject;
            so.FindProperty("marker").objectReferenceValue = marker.gameObject;
            so.FindProperty("starOn").colorValue = T.gold;
            so.ApplyModifiedPropertiesWithoutUndo();
            return n;
        }

        // ------------------------------------------------------------------ gameplay

        private static void BuildGameplay()
        {
            var view = UIMenu.CreateView<GameplayView>("Gameplay", UIViewKind.Screen, ViewIds.Gameplay);
            var t = view.transform;

            // Full-screen swipe catcher behind everything else.
            var catcher = new GameObject("SwipeArea", typeof(RectTransform), typeof(Image), typeof(SwipeInput));
            UIMenu.Stretch(catcher, t);
            catcher.GetComponent<Image>().color = new Color(0, 0, 0, 0);

            var home = UIMenu.IconButton(t, "Home", UIIcons.Home, UIButtonStyle.Secondary, 116);
            UIMenu.Place((RectTransform)home.transform, new Vector2(0, 1), new Vector2(36, -34), new Vector2(116, 116));
            var level = UIMenu.Chip(t, "LevelChip", UIIcons.Map, T.primaryTop, T.primaryBottom, 330);
            UIMenu.Place(UIMenu.ChipRoot(level), new Vector2(0.5f, 1), new Vector2(0, -40), new Vector2(330, 100));
            var moves = UIMenu.Chip(t, "MovesChip", UIIcons.Swipe, T.successTop, T.successBottom, 380, 88);
            UIMenu.Place(UIMenu.ChipRoot(moves), new Vector2(0.5f, 1), new Vector2(0, -170), new Vector2(380, 88));
            var hint = UIMenu.IconButton(t, "Hint", UIIcons.Lightbulb, UIButtonStyle.Accent, 128);
            UIMenu.Place((RectTransform)hint.transform, new Vector2(1, 1), new Vector2(-36, -34), new Vector2(128, 128));
            var hintCount = UIMenu.Badge(hint.transform, "Count", 60);

            var boardGo = new GameObject("Board", typeof(RectTransform), typeof(BoardView));
            boardGo.transform.SetParent(t, false);
            var brt = (RectTransform)boardGo.transform;
            brt.anchorMin = new Vector2(0, 0);
            brt.anchorMax = new Vector2(1, 1);
            brt.offsetMin = new Vector2(30, 290);
            brt.offsetMax = new Vector2(-30, -290);
            var bso = new SerializedObject(boardGo.GetComponent<BoardView>());
            bso.FindProperty("rounded").objectReferenceValue = T.rounded;
            bso.FindProperty("circle").objectReferenceValue = T.circle;
            bso.FindProperty("softShadow").objectReferenceValue = T.softShadow;
            bso.FindProperty("iconFont").objectReferenceValue = T.iconFont;
            bso.ApplyModifiedPropertiesWithoutUndo();

            var arrow = UIMenu.Img(t, "HintArrow", T.circle, new Color(1, 1, 1, 0.55f));
            UIMenu.Place(arrow.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(190, 190));
            arrow.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            arrow.rectTransform.anchoredPosition = Vector2.zero;
            arrow.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(0, -6);
            var arrowIcon = UIMenu.Icon(arrow.transform, "Icon", UIIcons.Forward, 140, T.accentBottom);
            UIMenu.Stretch(arrowIcon.gameObject, arrow.transform);
            arrow.gameObject.AddComponent<UIPulse>();
            arrow.gameObject.SetActive(false);

            var bubble = UIMenu.Card(t, "MessageBubble", new Vector2(960, 150), new Color(1, 1, 1, 0.97f), 44, out var bubbleFace);
            UIMenu.Place((RectTransform)bubble, new Vector2(0.5f, 0), new Vector2(0, 225), new Vector2(960, 150));
            var message = UIMenu.Text(bubbleFace, "Message", "", 38, FontStyles.Normal, 150);
            UIMenu.Stretch(message.gameObject, bubbleFace);
            message.rectTransform.offsetMin = new Vector2(36, 10);
            message.rectTransform.offsetMax = new Vector2(-36, -10);
            message.enableAutoSizing = true;
            message.fontSizeMin = 26;
            message.fontSizeMax = 38;

            // Recipe bar: which primaries make the colors this level needs. Tapping opens the color guide.
            var barCard = UIMenu.Card(t, "RecipeBar", new Vector2(980, 104), new Color(1, 1, 1, 0.94f), 52, out var barFace);
            UIMenu.Place((RectTransform)barCard, new Vector2(0.5f, 1), new Vector2(0, -282), new Vector2(980, 104));
            var barButton = barFace.gameObject.AddComponent<Button>();
            barButton.targetGraphic = barFace.GetComponent<Image>();
            var barRow = barFace.gameObject.AddComponent<HorizontalLayoutGroup>();
            barRow.padding = new RectOffset(44, 104, 0, 0);
            barRow.spacing = 34;
            barRow.childAlignment = TextAnchor.MiddleCenter;
            barRow.childControlWidth = true;
            barRow.childControlHeight = false;
            barRow.childForceExpandWidth = barRow.childForceExpandHeight = false;
            var recipeTemplate = RecipeRowObject(barFace, "Recipe", 54);
            var help = UIMenu.Img(barCard, "Help", T.circle, T.primaryBottom);
            UIMenu.Place(help.rectTransform, new Vector2(1, 0.5f), new Vector2(-24, 0), new Vector2(60, 60));
            help.rectTransform.pivot = new Vector2(1, 0.5f);
            help.rectTransform.anchoredPosition = new Vector2(-24, 0);
            var q = UIMenu.Text(help.transform, "Q", "?", 42, FontStyles.Bold, 60);
            UIMenu.OnColor(q);
            UIMenu.Stretch(q.gameObject, help.transform);
            barCard.gameObject.SetActive(false);

            var undo = UIMenu.IconButton(t, "Undo", UIIcons.Undo, UIButtonStyle.Secondary, 140);
            UIMenu.Place((RectTransform)undo.transform, new Vector2(0.5f, 0), new Vector2(-110, 50), new Vector2(140, 140));
            ((RectTransform)undo.transform).pivot = new Vector2(0.5f, 0);
            var restart = UIMenu.IconButton(t, "Restart", UIIcons.Restart, UIButtonStyle.Secondary, 140);
            UIMenu.Place((RectTransform)restart.transform, new Vector2(0.5f, 0), new Vector2(110, 50), new Vector2(140, 140));
            ((RectTransform)restart.transform).pivot = new Vector2(0.5f, 0);

            var so = new SerializedObject(view);
            so.FindProperty("board").objectReferenceValue = boardGo.GetComponent<BoardView>();
            so.FindProperty("swipe").objectReferenceValue = catcher.GetComponent<SwipeInput>();
            so.FindProperty("levelLabel").objectReferenceValue = level;
            so.FindProperty("movesLabel").objectReferenceValue = moves;
            so.FindProperty("messageLabel").objectReferenceValue = message;
            so.FindProperty("messageBubble").objectReferenceValue = bubble.gameObject;
            so.FindProperty("hintCountLabel").objectReferenceValue = hintCount;
            so.FindProperty("homeButton").objectReferenceValue = home;
            so.FindProperty("undoButton").objectReferenceValue = undo;
            so.FindProperty("restartButton").objectReferenceValue = restart;
            so.FindProperty("hintButton").objectReferenceValue = hint;
            so.FindProperty("hintArrow").objectReferenceValue = arrow.rectTransform;
            so.FindProperty("recipeBar").objectReferenceValue = barButton;
            so.FindProperty("recipeBarRoot").objectReferenceValue = barCard.gameObject;
            so.FindProperty("recipeTemplate").objectReferenceValue = recipeTemplate;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ------------------------------------------------------------------ color guide

        private static void BuildColorGuide()
        {
            var view = UIMenu.CreateView<ColorGuideView>("ColorGuidePopup", UIViewKind.Popup, TiltwellViewIds.ColorGuide);
            var panel = UIMenu.CreatePanel(view.transform, new Vector2(900, 1180));
            var panelRoot = (RectTransform)panel.parent;
            var layout = UIMenu.VerticalPadded(panel, 56, 50, 56, 22);
            UIMenu.Localized(UIMenu.Text(layout, "Title", "Mixing colors", 64, FontStyles.Bold, 90), TiltwellStrings.ColorGuideTitle);
            var sub = UIMenu.Text(layout, "Subtitle", "", 38, FontStyles.Normal, 100);
            sub.color = T.textMuted;
            UIMenu.Localized(sub, TiltwellStrings.ColorGuideText);

            int n = ColorRecipes.MixedColors.Length;
            var rows = new RecipeRow[n];
            var backs = new Image[n];
            for (int i = 0; i < n; i++)
            {
                var back = UIMenu.Img(layout, "Row" + i, T.rounded, T.surface, 40);
                var le = back.gameObject.AddComponent<LayoutElement>();
                le.preferredHeight = 150;
                rows[i] = RecipeRowObject(back.transform, "Recipe", 84);
                var rt = (RectTransform)rows[i].transform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(600, 110);
                rows[i].gameObject.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;
                backs[i] = back;
            }
            UIMenu.Spacer(layout, 8);
            var ok = UIMenu.Button(layout, "Ok", TiltwellStrings.GotIt, out _, UIButtonStyle.Success, UIIcons.Check, 128);

            var so = new SerializedObject(view);
            var rp = so.FindProperty("rows");
            var bp = so.FindProperty("rowBackgrounds");
            rp.arraySize = bp.arraySize = n;
            for (int i = 0; i < n; i++)
            {
                rp.GetArrayElementAtIndex(i).objectReferenceValue = rows[i];
                bp.GetArrayElementAtIndex(i).objectReferenceValue = backs[i];
            }
            so.FindProperty("okButton").objectReferenceValue = ok;
            so.FindProperty("panel").objectReferenceValue = panelRoot;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Empty RecipeRow (children are built at runtime) with a size the parent layout can read.</summary>
        private static RecipeRow RecipeRowObject(Transform parent, string name, float dropSize)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(RecipeRow));
            go.transform.SetParent(parent, false);
            ((RectTransform)go.transform).sizeDelta = new Vector2(300, dropSize * 1.4f);
            var h = go.GetComponent<HorizontalLayoutGroup>();
            h.spacing = dropSize * 0.18f;
            h.childAlignment = TextAnchor.MiddleCenter;
            h.childControlWidth = h.childControlHeight = false;
            h.childForceExpandWidth = h.childForceExpandHeight = false;
            var so = new SerializedObject(go.GetComponent<RecipeRow>());
            so.FindProperty("circle").objectReferenceValue = T.circle;
            so.FindProperty("font").objectReferenceValue = T.displayFont;
            so.FindProperty("dropSize").floatValue = dropSize;
            so.FindProperty("signColor").colorValue = T.text;
            so.ApplyModifiedPropertiesWithoutUndo();
            return go.GetComponent<RecipeRow>();
        }

        // ------------------------------------------------------------------ helpers

        private static void Center(RectTransform rt, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        private static void Currency(TMP_Text value, string resourceId)
        {
            var cd = value.gameObject.AddComponent<CurrencyDisplay>();
            var so = new SerializedObject(cd);
            so.FindProperty("resourceId").stringValue = resourceId;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
