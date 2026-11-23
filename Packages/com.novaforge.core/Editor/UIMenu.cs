using System;
using NovaForge.Core.Localization;
using NovaForge.Core.UI;
using NovaForge.UI;
using NovaForge.Unity;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NovaForge.Editor
{
    /// <summary>
    /// NOVAFORGE ▸ UI menu: builds a working UI hierarchy and the standard views from uGUI/TMP default controls.
    /// Results are plain GameObjects: restyle them freely and save as prefabs.
    /// TMP Essential Resources must be imported once per project (Window ▸ TextMeshPro ▸ Import TMP Essential Resources).
    /// </summary>
    public static class UIMenu
    {
        private const string Root = "NOVAFORGE/UI/";
        private static readonly Vector2 Reference = new Vector2(1080, 1920);

        [MenuItem(Root + "Create UI Root", priority = 0)]
        public static GameObject CreateUIRoot()
        {
            var existing = UnityEngine.Object.FindAnyObjectByType<UIManager>(FindObjectsInactive.Include);
            if (existing != null)
            {
                Selection.activeGameObject = existing.gameObject;
                Debug.Log("[NovaForge] Scene already has a UIManager.");
                return existing.gameObject;
            }

            var canvasGo = new GameObject("NovaForge UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Undo.RegisterCreatedObjectUndo(canvasGo, "Create NovaForge UI");
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = Reference;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            var safe = Stretch(new GameObject("SafeArea", typeof(RectTransform), typeof(SafeAreaFitter)), canvasGo.transform);
            var screens = Stretch(new GameObject("Screens", typeof(RectTransform)), safe);
            var popups = Stretch(new GameObject("Popups", typeof(RectTransform)), safe);
            var dim = Stretch(new GameObject("PopupDim", typeof(RectTransform), typeof(Image)), popups);
            var dimImage = dim.GetComponent<Image>();
            dimImage.color = new Color(0f, 0f, 0f, 0.6f);
            dim.gameObject.SetActive(false);

            var manager = canvasGo.AddComponent<UIManager>();
            var so = new SerializedObject(manager);
            so.FindProperty("screenRoot").objectReferenceValue = screens;
            so.FindProperty("popupRoot").objectReferenceValue = popups;
            so.FindProperty("popupDim").objectReferenceValue = dimImage;
            so.ApplyModifiedPropertiesWithoutUndo();

            EnsureEventSystem();
            Selection.activeGameObject = canvasGo;
            return canvasGo;
        }

        [MenuItem(Root + "Create Screen", priority = 20)]
        public static void CreateScreen() => Select(CreateView<UIView>("NewScreen", UIViewKind.Screen, null));

        [MenuItem(Root + "Create Popup", priority = 21)]
        public static void CreatePopup()
        {
            var view = CreateView<UIView>("NewPopup", UIViewKind.Popup, null);
            CreatePanel(view.transform, new Vector2(860, 900));
            Select(view);
        }

        [MenuItem(Root + "Create Confirm Popup", priority = 40)]
        public static void CreateConfirmPopup()
        {
            var view = CreateView<ConfirmPopup>("ConfirmPopup", UIViewKind.Popup, ViewIds.Confirm);
            var panel = CreatePanel(view.transform, new Vector2(860, 620));
            var layout = Vertical(panel, 40, 32);

            var title = Text(layout, "Title", "Title", 64, FontStyles.Bold, 90);
            var message = Text(layout, "Message", "Message", 44, FontStyles.Normal, 220);
            var buttons = new GameObject("Buttons", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            buttons.transform.SetParent(layout, false);
            buttons.GetComponent<LayoutElement>().preferredHeight = 140;
            var h = buttons.GetComponent<HorizontalLayoutGroup>();
            h.spacing = 32;
            h.childControlWidth = h.childControlHeight = true;
            h.childForceExpandWidth = h.childForceExpandHeight = true;
            var cancel = Button(buttons.transform, "Cancel", CoreStrings.Cancel, out var cancelLabel);
            var confirm = Button(buttons.transform, "Confirm", CoreStrings.Ok, out var confirmLabel);

            var so = new SerializedObject(view);
            so.FindProperty("title").objectReferenceValue = title;
            so.FindProperty("message").objectReferenceValue = message;
            so.FindProperty("confirmButton").objectReferenceValue = confirm;
            so.FindProperty("confirmLabel").objectReferenceValue = confirmLabel;
            so.FindProperty("cancelButton").objectReferenceValue = cancel;
            so.FindProperty("cancelLabel").objectReferenceValue = cancelLabel;
            so.ApplyModifiedPropertiesWithoutUndo();
            Select(view);
        }

        [MenuItem(Root + "Create Settings Popup", priority = 41)]
        public static void CreateSettingsPopup()
        {
            var view = CreateView<SettingsView>("SettingsPopup", UIViewKind.Popup, ViewIds.Settings);
            var panel = CreatePanel(view.transform, new Vector2(900, 1500));
            var layout = Vertical(panel, 48, 20);
            Localized(Text(layout, "Title", "Settings", 64, FontStyles.Bold, 110), CoreStrings.SettingsTitle);

            var res = TmpResources();
            var uiRes = UiResources();
            Slider SliderRow(string label, string key)
            {
                var row = Row(layout, label, key);
                var go = DefaultControls.CreateSlider(uiRes);
                go.transform.SetParent(row, false);
                Flexible(go, 1);
                return go.GetComponent<Slider>();
            }
            Toggle ToggleRow(string label, string key)
            {
                var row = Row(layout, label, key);
                var go = DefaultControls.CreateToggle(uiRes);
                go.transform.SetParent(row, false);
                var t = go.transform.Find("Label");
                if (t != null) UnityEngine.Object.DestroyImmediate(t.gameObject);
                var le = go.AddComponent<LayoutElement>();
                le.preferredWidth = 90;
                return go.GetComponent<Toggle>();
            }

            var music = SliderRow("Music", CoreStrings.Music);
            var sfx = SliderRow("Sound", CoreStrings.Sound);
            var haptics = ToggleRow("Vibration", CoreStrings.Haptics);
            var notifications = ToggleRow("Notifications", CoreStrings.Notifications);

            var langRow = Row(layout, "Language", CoreStrings.Language);
            var dropdownGo = TMP_DefaultControls.CreateDropdown(res);
            dropdownGo.transform.SetParent(langRow, false);
            Flexible(dropdownGo, 1);

            var privacyOptions = Button(layout, "PrivacyOptions", CoreStrings.PrivacyOptions, out _);
            var privacyPolicy = Button(layout, "PrivacyPolicy", CoreStrings.PrivacyPolicy, out _);
            var terms = Button(layout, "Terms", CoreStrings.Terms, out _);
            var support = Button(layout, "Support", CoreStrings.Support, out _);
            var close = Button(layout, "Close", CoreStrings.Close, out _);
            UnityEditor.Events.UnityEventTools.AddVoidPersistentListener(close.onClick, view.Close);
            var version = Text(layout, "Version", "v0.0.0", 30, FontStyles.Normal, 50);

            var so = new SerializedObject(view);
            so.FindProperty("musicVolume").objectReferenceValue = music;
            so.FindProperty("sfxVolume").objectReferenceValue = sfx;
            so.FindProperty("haptics").objectReferenceValue = haptics;
            so.FindProperty("notifications").objectReferenceValue = notifications;
            so.FindProperty("language").objectReferenceValue = dropdownGo.GetComponent<TMP_Dropdown>();
            so.FindProperty("privacyOptions").objectReferenceValue = privacyOptions;
            so.FindProperty("privacyPolicy").objectReferenceValue = privacyPolicy;
            so.FindProperty("terms").objectReferenceValue = terms;
            so.FindProperty("support").objectReferenceValue = support;
            so.FindProperty("versionLabel").objectReferenceValue = version;
            so.ApplyModifiedPropertiesWithoutUndo();
            Select(view);
        }

        [MenuItem(Root + "Create Level Result Popup", priority = 42)]
        public static void CreateLevelResultPopup()
        {
            var view = CreateView<LevelResultView>("LevelResultPopup", UIViewKind.Popup, ViewIds.LevelResult);
            var panel = CreatePanel(view.transform, new Vector2(880, 1100));
            var layout = Vertical(panel, 48, 28);
            var title = Text(layout, "Title", "Level Complete!", 72, FontStyles.Bold, 120);

            var starRow = new GameObject("Stars", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            starRow.transform.SetParent(layout, false);
            starRow.GetComponent<LayoutElement>().preferredHeight = 160;
            var h = starRow.GetComponent<HorizontalLayoutGroup>();
            h.childAlignment = TextAnchor.MiddleCenter;
            h.spacing = 24;
            h.childControlWidth = h.childControlHeight = false;
            var stars = new GameObject[3];
            for (int i = 0; i < 3; i++)
            {
                var star = new GameObject("Star" + (i + 1), typeof(RectTransform), typeof(Image));
                star.transform.SetParent(starRow.transform, false);
                ((RectTransform)star.transform).sizeDelta = new Vector2(140, 140);
                star.GetComponent<Image>().color = new Color(1f, 0.8f, 0.2f);
                stars[i] = star;
            }

            var reward = Text(layout, "Reward", "+50 coin", 52, FontStyles.Bold, 90);
            var doubleReward = Button(layout, "DoubleReward", CoreStrings.DoubleReward, out _);
            var next = Button(layout, "Next", CoreStrings.Next, out _);
            var retry = Button(layout, "Retry", CoreStrings.Retry, out _);
            var home = Button(layout, "Home", CoreStrings.Home, out _);

            var so = new SerializedObject(view);
            so.FindProperty("title").objectReferenceValue = title;
            var starsProp = so.FindProperty("stars");
            starsProp.arraySize = 3;
            for (int i = 0; i < 3; i++) starsProp.GetArrayElementAtIndex(i).objectReferenceValue = stars[i];
            so.FindProperty("rewardLabel").objectReferenceValue = reward;
            so.FindProperty("nextButton").objectReferenceValue = next;
            so.FindProperty("retryButton").objectReferenceValue = retry;
            so.FindProperty("homeButton").objectReferenceValue = home;
            so.FindProperty("doubleRewardButton").objectReferenceValue = doubleReward;
            so.ApplyModifiedPropertiesWithoutUndo();
            Select(view);
        }

        [MenuItem(Root + "Create Daily Reward Popup", priority = 43)]
        public static void CreateDailyRewardPopup()
        {
            var view = CreateView<DailyRewardView>("DailyRewardPopup", UIViewKind.Popup, ViewIds.DailyReward);
            var panel = CreatePanel(view.transform, new Vector2(940, 1300));
            var layout = Vertical(panel, 40, 24);
            Localized(Text(layout, "Title", "Daily Reward", 64, FontStyles.Bold, 110), CoreStrings.DailyTitle);

            var grid = new GameObject("Days", typeof(RectTransform), typeof(GridLayoutGroup), typeof(LayoutElement));
            grid.transform.SetParent(layout, false);
            grid.GetComponent<LayoutElement>().preferredHeight = 760;
            var g = grid.GetComponent<GridLayoutGroup>();
            g.cellSize = new Vector2(250, 240);
            g.spacing = new Vector2(20, 20);
            g.childAlignment = TextAnchor.UpperCenter;
            g.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            g.constraintCount = 3;

            var cells = new DailyRewardDayCell[7];
            for (int i = 0; i < 7; i++)
            {
                var cellGo = new GameObject("Day" + (i + 1), typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
                cellGo.transform.SetParent(grid.transform, false);
                cellGo.GetComponent<Image>().color = new Color(0.24f, 0.26f, 0.33f);
                var v = cellGo.GetComponent<VerticalLayoutGroup>();
                v.childAlignment = TextAnchor.MiddleCenter;
                v.childControlWidth = v.childControlHeight = true;
                v.childForceExpandWidth = true;
                v.padding = new RectOffset(8, 8, 8, 8);
                var cell = cellGo.AddComponent<DailyRewardDayCell>();
                cell.dayLabel = Text(cellGo.transform, "Day", "Day " + (i + 1), 34, FontStyles.Bold, 60);
                cell.rewardLabel = Text(cellGo.transform, "Reward", "50 coin", 32, FontStyles.Normal, 100);
                cell.claimedMark = Overlay(cellGo.transform, "Claimed", new Color(0.2f, 0.8f, 0.4f, 0.35f));
                cell.currentHighlight = Overlay(cellGo.transform, "Current", new Color(1f, 0.85f, 0.2f, 0.3f));
                cells[i] = cell;
            }

            var claim = Button(layout, "Claim", CoreStrings.DailyClaim, out _);
            var countdown = Text(layout, "Countdown", "Next reward in 12:00:00", 36, FontStyles.Normal, 60);
            var close = Button(layout, "Close", CoreStrings.Close, out _);

            var so = new SerializedObject(view);
            var daysProp = so.FindProperty("days");
            daysProp.arraySize = cells.Length;
            for (int i = 0; i < cells.Length; i++) daysProp.GetArrayElementAtIndex(i).objectReferenceValue = cells[i];
            so.FindProperty("claimButton").objectReferenceValue = claim;
            so.FindProperty("closeButton").objectReferenceValue = close;
            so.FindProperty("countdownLabel").objectReferenceValue = countdown;
            so.ApplyModifiedPropertiesWithoutUndo();
            Select(view);
        }

        [MenuItem(Root + "Create Shop Screen", priority = 44)]
        public static void CreateShopScreen()
        {
            var view = CreateView<ShopView>("ShopScreen", UIViewKind.Screen, ViewIds.Shop);
            var bg = view.gameObject.AddComponent<Image>();
            bg.color = new Color(0.12f, 0.13f, 0.17f);
            var layout = Vertical(view.transform, 40, 24);
            Localized(Text(layout, "Title", "Shop", 72, FontStyles.Bold, 130), CoreStrings.ShopTitle);

            var scrollGo = DefaultControls.CreateScrollView(UiResources());
            scrollGo.name = "Items";
            scrollGo.transform.SetParent(layout, false);
            scrollGo.AddComponent<LayoutElement>().flexibleHeight = 1;
            var scroll = scrollGo.GetComponent<ScrollRect>();
            scroll.horizontal = false;
            var hBar = scroll.horizontalScrollbar;
            scroll.horizontalScrollbar = null;
            if (hBar != null) UnityEngine.Object.DestroyImmediate(hBar.gameObject);
            var content = scroll.content;
            var cv = content.gameObject.AddComponent<VerticalLayoutGroup>();
            cv.spacing = 20;
            cv.padding = new RectOffset(16, 16, 16, 16);
            cv.childControlWidth = cv.childControlHeight = true;
            cv.childForceExpandWidth = true;
            cv.childForceExpandHeight = false;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var rowGo = new GameObject("ItemTemplate", typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            rowGo.transform.SetParent(content, false);
            rowGo.GetComponent<Image>().color = new Color(0.2f, 0.22f, 0.28f);
            rowGo.GetComponent<LayoutElement>().preferredHeight = 200;
            var rh = rowGo.GetComponent<HorizontalLayoutGroup>();
            rh.padding = new RectOffset(28, 28, 20, 20);
            rh.spacing = 20;
            rh.childAlignment = TextAnchor.MiddleLeft;
            rh.childControlWidth = rh.childControlHeight = true;
            rh.childForceExpandWidth = false;
            rh.childForceExpandHeight = true;
            var info = new GameObject("Info", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
            info.transform.SetParent(rowGo.transform, false);
            info.GetComponent<LayoutElement>().flexibleWidth = 1;
            var iv = info.GetComponent<VerticalLayoutGroup>();
            iv.childControlWidth = iv.childControlHeight = true;
            iv.childForceExpandWidth = true;
            var item = rowGo.AddComponent<ShopItemView>();
            item.titleLabel = Text(info.transform, "Title", "Item", 44, FontStyles.Bold, 70);
            item.titleLabel.alignment = TextAlignmentOptions.MidlineLeft;
            item.contentLabel = Text(info.transform, "Content", "100 coin", 34, FontStyles.Normal, 60);
            item.contentLabel.alignment = TextAlignmentOptions.MidlineLeft;
            item.buyButton = Button(rowGo.transform, "Buy", null, out var priceLabel);
            item.priceLabel = priceLabel;
            item.buyButton.GetComponent<LayoutElement>().preferredWidth = 300;
            priceLabel.text = "$0.99";
            var owned = Text(rowGo.transform, "Owned", "Owned", 40, FontStyles.Bold, 120);
            owned.GetComponent<LayoutElement>().preferredWidth = 300;
            Localized(owned, CoreStrings.ShopOwned);
            item.ownedMark = owned.gameObject;

            var restore = Button(layout, "Restore", CoreStrings.ShopRestore, out _);
            var close = Button(layout, "Close", CoreStrings.Close, out _);

            var so = new SerializedObject(view);
            so.FindProperty("itemTemplate").objectReferenceValue = item;
            so.FindProperty("itemContainer").objectReferenceValue = content;
            so.FindProperty("restoreButton").objectReferenceValue = restore;
            so.FindProperty("closeButton").objectReferenceValue = close;
            so.ApplyModifiedPropertiesWithoutUndo();
            Select(view);
        }

        [MenuItem("NOVAFORGE/Audio/Create Audio Service", priority = 0)]
        public static void CreateAudioService()
        {
            var existing = UnityEngine.Object.FindAnyObjectByType<NovaForge.Unity.Audio.AudioService>(FindObjectsInactive.Include);
            if (existing != null) { Selection.activeGameObject = existing.gameObject; return; }
            var app = UnityEngine.Object.FindAnyObjectByType<NovaForgeApp>(FindObjectsInactive.Include);
            var host = app != null ? app.gameObject : new GameObject("Audio");
            if (app == null) Undo.RegisterCreatedObjectUndo(host, "Create Audio Service");
            Undo.AddComponent<NovaForge.Unity.Audio.AudioService>(host);
            Selection.activeGameObject = host;
        }

        public static GameObject Overlay(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            go.GetComponent<LayoutElement>().ignoreLayout = true;
            Stretch(go, parent);
            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            go.SetActive(false);
            return go;
        }

        // ------------------------------------------------------------------ helpers

        public static T CreateView<T>(string name, UIViewKind kind, string id) where T : UIView
        {
            var manager = UnityEngine.Object.FindAnyObjectByType<UIManager>(FindObjectsInactive.Include);
            if (manager == null) manager = CreateUIRoot().GetComponent<UIManager>();
            var mso = new SerializedObject(manager);
            var root = mso.FindProperty(kind == UIViewKind.Popup ? "popupRoot" : "screenRoot").objectReferenceValue as Transform;
            var parent = root != null ? root : manager.transform;

            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup));
            Undo.RegisterCreatedObjectUndo(go, "Create " + name);
            Stretch(go, parent);
            var view = go.AddComponent<T>();
            var so = new SerializedObject(view);
            so.FindProperty("kind").enumValueIndex = (int)kind;
            so.FindProperty("transition").enumValueIndex = (int)(kind == UIViewKind.Popup ? UITransition.FadeScale : UITransition.Fade);
            if (id != null) so.FindProperty("id").stringValue = id;
            so.ApplyModifiedPropertiesWithoutUndo();
            return view;
        }

        public static Transform CreatePanel(Transform parent, Vector2 size)
        {
            var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(parent, false);
            var rt = (RectTransform)panel.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            var img = panel.GetComponent<Image>();
            img.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            img.type = Image.Type.Sliced;
            img.color = new Color(0.16f, 0.17f, 0.22f, 1f);
            return panel.transform;
        }

        public static Transform Vertical(Transform panel, int padding, float spacing)
        {
            var v = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(padding, padding, padding, padding);
            v.spacing = spacing;
            v.childAlignment = TextAnchor.UpperCenter;
            v.childControlWidth = v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
            return panel;
        }

        public static Transform Row(Transform parent, string label, string key)
        {
            var row = new GameObject(label + "Row", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            row.transform.SetParent(parent, false);
            row.GetComponent<LayoutElement>().preferredHeight = 100;
            var h = row.GetComponent<HorizontalLayoutGroup>();
            h.spacing = 24;
            h.childAlignment = TextAnchor.MiddleLeft;
            h.childControlWidth = h.childControlHeight = true;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = true;
            var text = Text(row.transform, "Label", label, 42, FontStyles.Normal, 100);
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.GetComponent<LayoutElement>().preferredWidth = 360;
            Localized(text, key);
            return row.transform;
        }

        public static TMP_Text Text(Transform parent, string name, string value, float size, FontStyles style, float height)
        {
            var go = TMP_DefaultControls.CreateText(TmpResources());
            go.name = name;
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<TextMeshProUGUI>();
            t.text = value;
            t.fontSize = size;
            t.fontStyle = style;
            t.alignment = TextAlignmentOptions.Center;
            t.color = Color.white;
            go.AddComponent<LayoutElement>().preferredHeight = height;
            return t;
        }

        public static Button Button(Transform parent, string name, string key, out TMP_Text label)
        {
            var go = TMP_DefaultControls.CreateButton(TmpResources());
            go.name = name;
            go.transform.SetParent(parent, false);
            go.AddComponent<LayoutElement>().preferredHeight = 120;
            label = go.GetComponentInChildren<TMP_Text>();
            label.fontSize = 44;
            if (key != null) Localized(label, key);
            go.AddComponent<UIButtonFeedback>();
            return go.GetComponent<Button>();
        }

        public static void Localized(TMP_Text text, string key)
        {
            var loc = text.gameObject.AddComponent<LocalizedText>();
            var so = new SerializedObject(loc);
            so.FindProperty("key").stringValue = key;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void Flexible(GameObject go, float weight)
        {
            if (!go.TryGetComponent<LayoutElement>(out var le)) le = go.AddComponent<LayoutElement>();
            le.flexibleWidth = weight;
        }

        public static Transform Stretch(GameObject go, Transform parent)
        {
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return go.transform;
        }

        private static void EnsureEventSystem()
        {
            if (UnityEngine.Object.FindAnyObjectByType<EventSystem>(FindObjectsInactive.Include) != null) return;
            var es = new GameObject("EventSystem", typeof(EventSystem));
            Undo.RegisterCreatedObjectUndo(es, "Create EventSystem");
            // Prefer the Input System module when that package is active; avoid a hard assembly reference.
            var inputSystemModule = Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
#if ENABLE_INPUT_SYSTEM
            if (inputSystemModule != null) { es.AddComponent(inputSystemModule); return; }
#endif
            es.AddComponent<StandaloneInputModule>();
        }

        private static void Select(Component c)
        {
            Selection.activeGameObject = c.gameObject;
            EditorGUIUtility.PingObject(c.gameObject);
        }

        public static DefaultControls.Resources UiResources() => new DefaultControls.Resources
        {
            standard = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd"),
            background = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd"),
            inputField = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/InputFieldBackground.psd"),
            knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd"),
            checkmark = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Checkmark.psd"),
            dropdown = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/DropdownArrow.psd"),
            mask = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UIMask.psd"),
        };

        public static TMP_DefaultControls.Resources TmpResources()
        {
            var r = UiResources();
            return new TMP_DefaultControls.Resources
            {
                standard = r.standard, background = r.background, inputField = r.inputField,
                knob = r.knob, checkmark = r.checkmark, dropdown = r.dropdown, mask = r.mask,
            };
        }
    }
}
