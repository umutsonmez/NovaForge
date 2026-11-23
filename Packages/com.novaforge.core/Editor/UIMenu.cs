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

        // ------------------------------------------------------------------ helpers

        private static T CreateView<T>(string name, UIViewKind kind, string id) where T : UIView
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

        private static Transform CreatePanel(Transform parent, Vector2 size)
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

        private static Transform Vertical(Transform panel, int padding, float spacing)
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

        private static Transform Row(Transform parent, string label, string key)
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

        private static TMP_Text Text(Transform parent, string name, string value, float size, FontStyles style, float height)
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

        private static Button Button(Transform parent, string name, string key, out TMP_Text label)
        {
            var go = TMP_DefaultControls.CreateButton(TmpResources());
            go.name = name;
            go.transform.SetParent(parent, false);
            go.AddComponent<LayoutElement>().preferredHeight = 120;
            label = go.GetComponentInChildren<TMP_Text>();
            label.fontSize = 44;
            Localized(label, key);
            return go.GetComponent<Button>();
        }

        private static void Localized(TMP_Text text, string key)
        {
            var loc = text.gameObject.AddComponent<LocalizedText>();
            var so = new SerializedObject(loc);
            so.FindProperty("key").stringValue = key;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Flexible(GameObject go, float weight)
        {
            if (!go.TryGetComponent<LayoutElement>(out var le)) le = go.AddComponent<LayoutElement>();
            le.flexibleWidth = weight;
        }

        private static Transform Stretch(GameObject go, Transform parent)
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

        private static DefaultControls.Resources UiResources() => new DefaultControls.Resources
        {
            standard = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd"),
            background = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd"),
            inputField = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/InputFieldBackground.psd"),
            knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd"),
            checkmark = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Checkmark.psd"),
            dropdown = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/DropdownArrow.psd"),
            mask = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UIMask.psd"),
        };

        private static TMP_DefaultControls.Resources TmpResources()
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
