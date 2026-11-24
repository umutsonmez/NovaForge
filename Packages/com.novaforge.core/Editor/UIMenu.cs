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
    /// NOVAFORGE ▸ UI menu: builds a working, themed UI hierarchy and the standard views.
    /// Everything visual comes from <see cref="Theme"/> (default: the package's DefaultTheme). A game sets
    /// <c>UIMenu.Theme</c> to its own UITheme before building to restyle every view.
    /// Results are plain GameObjects: restyle them freely and save as prefabs.
    /// </summary>
    public static class UIMenu
    {
        private const string Root = "NOVAFORGE/UI/";
        private static readonly Vector2 Reference = new Vector2(1080, 1920);
        private static UITheme _theme;

        public static UITheme Theme
        {
            get
            {
                if (_theme == null) _theme = ThemeAssets.LoadDefault();
                if (_theme == null) throw new InvalidOperationException("Default UI theme missing. Run NOVAFORGE ▸ UI ▸ Regenerate Default Theme Assets.");
                return _theme;
            }
            set => _theme = value;
        }

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
            dimImage.color = Theme.dim;
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

        /// <summary>Full-screen theme gradient behind everything (outside the safe area, so it fills notches).</summary>
        public static Image Background(Transform canvasRoot)
        {
            var img = Img(canvasRoot, "Background", null, Color.white);
            Stretch(img.gameObject, canvasRoot);
            img.transform.SetAsFirstSibling();
            img.gameObject.AddComponent<UIGradient>().Set(Theme.backgroundTop, Theme.backgroundBottom);
            return img;
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

        // ------------------------------------------------------------------ standard views

        [MenuItem(Root + "Create Confirm Popup", priority = 40)]
        public static void CreateConfirmPopup()
        {
            var view = CreateView<ConfirmPopup>("ConfirmPopup", UIViewKind.Popup, ViewIds.Confirm);
            var panel = CreatePanel(view.transform, new Vector2(880, 680));
            var layout = Vertical(panel, 56, 28);

            var title = Text(layout, "Title", "Title", 60, FontStyles.Bold, 90);
            var message = Text(layout, "Message", "Message", 42, FontStyles.Normal, 230);
            message.color = Theme.textMuted;
            var buttons = HRow(layout, "Buttons", 130, 28);
            var cancel = Button(buttons, "Cancel", CoreStrings.Cancel, out var cancelLabel, UIButtonStyle.Secondary, null, 130);
            var confirm = Button(buttons, "Confirm", CoreStrings.Ok, out var confirmLabel, UIButtonStyle.Primary, null, 130);

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
            var panel = CreatePanel(view.transform, new Vector2(920, 1340));
            var layout = Vertical(panel, 44, 20);
            Localized(Text(layout, "Title", "Settings", 64, FontStyles.Bold, 100), CoreStrings.SettingsTitle);
            var close = CornerClose(panel);
            UnityEditor.Events.UnityEventTools.AddVoidPersistentListener(close.onClick, view.Close);

            var music = Slider(Row(layout, "Music", CoreStrings.Music, UIIcons.Music), "Slider");
            var sfx = Slider(Row(layout, "Sound", CoreStrings.Sound, UIIcons.Volume), "Slider");
            var haptics = Switch(Row(layout, "Vibration", CoreStrings.Haptics, UIIcons.Vibration), "Switch");
            var notifications = Switch(Row(layout, "Notifications", CoreStrings.Notifications, UIIcons.Notifications), "Switch");
            var language = Dropdown(Row(layout, "Language", CoreStrings.Language, UIIcons.Language));

            Spacer(layout, 6);
            var privacyOptions = Button(layout, "PrivacyOptions", CoreStrings.PrivacyOptions, out _, UIButtonStyle.Secondary, UIIcons.Privacy, 104);
            var privacyPolicy = Button(layout, "PrivacyPolicy", CoreStrings.PrivacyPolicy, out _, UIButtonStyle.Secondary, UIIcons.Document, 104);
            var terms = Button(layout, "Terms", CoreStrings.Terms, out _, UIButtonStyle.Secondary, UIIcons.Document, 104);
            var support = Button(layout, "Support", CoreStrings.Support, out _, UIButtonStyle.Secondary, UIIcons.Mail, 104);
            var version = Text(layout, "Version", "v0.0.0", 30, FontStyles.Normal, 50);
            version.color = Theme.textMuted;

            var so = new SerializedObject(view);
            so.FindProperty("musicVolume").objectReferenceValue = music;
            so.FindProperty("sfxVolume").objectReferenceValue = sfx;
            so.FindProperty("haptics").objectReferenceValue = haptics;
            so.FindProperty("notifications").objectReferenceValue = notifications;
            so.FindProperty("language").objectReferenceValue = language;
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
            var panel = CreatePanel(view.transform, new Vector2(900, 1060));
            var layout = VerticalPadded(panel, 56, 130, 48, 24);

            // Ribbon title overlapping the panel's top edge.
            var ribbon = Card(panel.parent, "Ribbon", new Vector2(660, 150), Theme.accentTop, 48, out var ribbonFace);
            Place((RectTransform)ribbon, new Vector2(0.5f, 1f), new Vector2(0, 70), new Vector2(660, 150));
            ribbonFace.gameObject.AddComponent<UIGradient>().Set(Theme.accentTop, Theme.accentBottom);
            var title = Text(ribbonFace, "Title", "Level Complete!", 60, FontStyles.Bold, 150);
            Stretch(title.gameObject, ribbonFace);
            OnColor(title);

            var starArea = new GameObject("Stars", typeof(RectTransform), typeof(LayoutElement));
            starArea.transform.SetParent(layout, false);
            starArea.GetComponent<LayoutElement>().preferredHeight = 240;
            var stars = new GameObject[3];
            var slots = new[] { (-215f, -24f, 165f, 12f), (0f, 14f, 215f, 0f), (215f, -24f, 165f, -12f) };
            for (int i = 0; i < 3; i++)
            {
                var (x, y, size, rot) = slots[i];
                var back = Img(starArea.transform, "Slot" + (i + 1), Theme.star, new Color32(0xDC, 0xE1, 0xEC, 0xFF));
                Place(back.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(x, y), new Vector2(size, size));
                back.rectTransform.localRotation = Quaternion.Euler(0, 0, rot);
                var star = Img(back.transform, "Star" + (i + 1), Theme.star, Theme.gold);
                Stretch(star.gameObject, back.transform);
                star.gameObject.AddComponent<UIGradient>().Set(new Color(1f, 0.93f, 0.55f), Color.white);
                star.gameObject.AddComponent<UIPopIn>();
                stars[i] = star.gameObject;
            }

            var rewardRow = HRow(layout, "RewardRow", 110, 18);
            rewardRow.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = false;
            rewardRow.GetComponent<HorizontalLayoutGroup>().childForceExpandHeight = false;
            IconBubble(rewardRow, "CoinIcon", UIIcons.Coin, Theme.accentTop, Theme.accentBottom, 92);
            var reward = Text(rewardRow, "Reward", "+50", 56, FontStyles.Bold, 110);

            var doubleReward = Button(layout, "DoubleReward", CoreStrings.DoubleReward, out _, UIButtonStyle.Accent, UIIcons.Video, 128);
            var next = Button(layout, "Next", CoreStrings.Next, out _, UIButtonStyle.Success, UIIcons.Forward, 144);
            var retry = Button(layout, "Retry", CoreStrings.Retry, out _, UIButtonStyle.Primary, UIIcons.Replay, 144);
            var home = Button(layout, "Home", CoreStrings.Home, out _, UIButtonStyle.Secondary, UIIcons.Home, 112);

            var confettiGo = new GameObject("Confetti", typeof(RectTransform), typeof(ConfettiBurst));
            confettiGo.transform.SetParent(view.transform, false);
            Place((RectTransform)confettiGo.transform, new Vector2(0.5f, 0.5f), new Vector2(0, 380), Vector2.zero);

            var so = new SerializedObject(view);
            so.FindProperty("title").objectReferenceValue = title;
            var starsProp = so.FindProperty("stars");
            starsProp.arraySize = 3;
            for (int i = 0; i < 3; i++) starsProp.GetArrayElementAtIndex(i).objectReferenceValue = stars[i];
            so.FindProperty("unearnedStarAlpha").floatValue = 0f;
            so.FindProperty("rewardLabel").objectReferenceValue = reward;
            so.FindProperty("nextButton").objectReferenceValue = next;
            so.FindProperty("retryButton").objectReferenceValue = retry;
            so.FindProperty("homeButton").objectReferenceValue = home;
            so.FindProperty("doubleRewardButton").objectReferenceValue = doubleReward;
            so.FindProperty("confetti").objectReferenceValue = confettiGo.GetComponent<ConfettiBurst>();
            so.ApplyModifiedPropertiesWithoutUndo();
            Select(view);
        }

        [MenuItem(Root + "Create Daily Reward Popup", priority = 43)]
        public static void CreateDailyRewardPopup()
        {
            var view = CreateView<DailyRewardView>("DailyRewardPopup", UIViewKind.Popup, ViewIds.DailyReward);
            var panel = CreatePanel(view.transform, new Vector2(960, 1380));
            var layout = Vertical(panel, 44, 22);
            Localized(Text(layout, "Title", "Daily Reward", 64, FontStyles.Bold, 100), CoreStrings.DailyTitle);
            var close = CornerClose(panel);

            var grid = new GameObject("Days", typeof(RectTransform), typeof(GridLayoutGroup), typeof(LayoutElement));
            grid.transform.SetParent(layout, false);
            grid.GetComponent<LayoutElement>().preferredHeight = 3 * 290 + 2 * 22;
            var g = grid.GetComponent<GridLayoutGroup>();
            g.cellSize = new Vector2(268, 290);
            g.spacing = new Vector2(22, 22);
            g.childAlignment = TextAnchor.UpperCenter;
            g.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            g.constraintCount = 3;

            var cells = new DailyRewardDayCell[7];
            for (int i = 0; i < 7; i++)
            {
                var cellImg = Img(grid.transform, "Day" + (i + 1), Theme.rounded, Theme.surface, 32);
                cellImg.gameObject.AddComponent<UIGradient>().Set(Color.white, new Color(0.93f, 0.94f, 0.97f));
                var v = cellImg.gameObject.AddComponent<VerticalLayoutGroup>();
                v.childAlignment = TextAnchor.MiddleCenter;
                v.childControlWidth = v.childControlHeight = true;
                v.childForceExpandWidth = true;
                v.childForceExpandHeight = false;
                v.padding = new RectOffset(10, 10, 14, 14);
                var cell = cellImg.gameObject.AddComponent<DailyRewardDayCell>();
                cell.dayLabel = Text(cellImg.transform, "Day", "Day " + (i + 1), 32, FontStyles.Normal, 46);
                cell.dayLabel.color = Theme.textMuted;
                var gift = Icon(cellImg.transform, "Gift", UIIcons.Gift, 104, i == 6 ? Theme.badge : Theme.accentBottom);
                gift.gameObject.AddComponent<LayoutElement>().preferredHeight = 120;
                cell.rewardLabel = Text(cellImg.transform, "Reward", "50", 34, FontStyles.Bold, 80);
                cell.currentHighlight = Overlay(cellImg.transform, "Current", new Color(1f, 0.78f, 0.2f, 0.32f), 32);
                cell.currentHighlight.AddComponent<UIPulse>();
                cell.claimedMark = Overlay(cellImg.transform, "Claimed", WithAlpha(Theme.successBottom, 0.88f), 32);
                var check = Icon(cell.claimedMark.transform, "Check", UIIcons.Check, 130, Color.white);
                Stretch(check.gameObject, cell.claimedMark.transform);
                cells[i] = cell;
            }

            var claim = Button(layout, "Claim", CoreStrings.DailyClaim, out _, UIButtonStyle.Success, UIIcons.Gift, 144);
            var countdown = Text(layout, "Countdown", "12:00:00", 38, FontStyles.Normal, 60);
            countdown.color = Theme.textMuted;

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
            bg.color = Color.white;
            view.gameObject.AddComponent<UIGradient>().Set(Theme.backgroundTop, Theme.backgroundBottom);
            var t = view.transform;

            var back = IconButton(t, "Back", UIIcons.Back, UIButtonStyle.Secondary, 120);
            Place((RectTransform)back.transform, new Vector2(0, 1), new Vector2(40, -40), new Vector2(120, 120));
            var title = Text(t, "Title", "Shop", 72, FontStyles.Bold, 120);
            Localized(title, CoreStrings.ShopTitle);
            Place(title.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -40), new Vector2(560, 120));
            var coins = Chip(t, "CoinChip", UIIcons.Coin, Theme.accentTop, Theme.accentBottom, 280);
            Place(ChipRoot(coins), new Vector2(1, 1), new Vector2(-40, -50), new Vector2(280, 100));
            coins.gameObject.AddComponent<CurrencyDisplay>();
            var cso = new SerializedObject(coins.GetComponent<CurrencyDisplay>());
            cso.FindProperty("resourceId").stringValue = "coin";
            cso.ApplyModifiedPropertiesWithoutUndo();

            var scrollGo = new GameObject("Items", typeof(RectTransform), typeof(ScrollRect));
            scrollGo.transform.SetParent(t, false);
            var srt = (RectTransform)scrollGo.transform;
            srt.anchorMin = Vector2.zero;
            srt.anchorMax = Vector2.one;
            srt.offsetMin = new Vector2(24, 200);
            srt.offsetMax = new Vector2(-24, -200);
            var viewport = Stretch(new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D)), srt);
            var content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            content.transform.SetParent(viewport, false);
            var crt = (RectTransform)content.transform;
            crt.anchorMin = new Vector2(0, 1);
            crt.anchorMax = new Vector2(1, 1);
            crt.pivot = new Vector2(0.5f, 1);
            crt.sizeDelta = Vector2.zero;
            var cv = content.GetComponent<VerticalLayoutGroup>();
            cv.spacing = 26;
            cv.padding = new RectOffset(16, 16, 20, 40);
            cv.childControlWidth = cv.childControlHeight = true;
            cv.childForceExpandWidth = true;
            cv.childForceExpandHeight = false;
            content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = scrollGo.GetComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.viewport = (RectTransform)viewport;
            scroll.content = crt;
            scroll.movementType = ScrollRect.MovementType.Elastic;

            var card = Card(content.transform, "ItemTemplate", new Vector2(0, 220), Color.white, 40, out var face);
            card.gameObject.AddComponent<LayoutElement>().preferredHeight = 220;
            var rh = face.gameObject.AddComponent<HorizontalLayoutGroup>();
            rh.padding = new RectOffset(28, 28, 24, 24);
            rh.spacing = 24;
            rh.childAlignment = TextAnchor.MiddleLeft;
            rh.childControlWidth = rh.childControlHeight = true;
            rh.childForceExpandWidth = false;
            rh.childForceExpandHeight = false;
            IconBubble(face, "Icon", UIIcons.Gift, Theme.primaryTop, Theme.primaryBottom, 140);
            var info = new GameObject("Info", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
            info.transform.SetParent(face, false);
            info.GetComponent<LayoutElement>().flexibleWidth = 1;
            var iv = info.GetComponent<VerticalLayoutGroup>();
            iv.childControlWidth = iv.childControlHeight = true;
            iv.childForceExpandWidth = true;
            iv.childForceExpandHeight = false;
            iv.childAlignment = TextAnchor.MiddleLeft;
            var item = card.gameObject.AddComponent<ShopItemView>();
            item.titleLabel = Text(info.transform, "Title", "Item", 46, FontStyles.Bold, 64);
            item.titleLabel.alignment = TextAlignmentOptions.MidlineLeft;
            item.contentLabel = Text(info.transform, "Content", "100 coin", 34, FontStyles.Normal, 52);
            item.contentLabel.alignment = TextAlignmentOptions.MidlineLeft;
            item.contentLabel.color = Theme.textMuted;
            item.buyButton = Button(face, "Buy", null, out var priceLabel, UIButtonStyle.Success, null, 112);
            item.buyButton.GetComponent<LayoutElement>().preferredWidth = 270;
            item.priceLabel = priceLabel;
            priceLabel.text = "$0.99";
            var owned = Text(face, "Owned", "Owned", 40, FontStyles.Bold, 112);
            owned.color = Theme.successBottom;
            owned.GetComponent<LayoutElement>().preferredWidth = 270;
            Localized(owned, CoreStrings.ShopOwned);
            item.ownedMark = owned.gameObject;

            var restore = Button(t, "Restore", CoreStrings.ShopRestore, out _, UIButtonStyle.Secondary, UIIcons.Replay, 110);
            Place((RectTransform)restore.transform, new Vector2(0.5f, 0), new Vector2(0, 50), new Vector2(620, 110));

            var so = new SerializedObject(view);
            so.FindProperty("itemTemplate").objectReferenceValue = item;
            so.FindProperty("itemContainer").objectReferenceValue = content.transform;
            so.FindProperty("restoreButton").objectReferenceValue = restore;
            so.FindProperty("closeButton").objectReferenceValue = back;
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

        // ------------------------------------------------------------------ building blocks

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

        /// <summary>Centered card with soft shadow. Returns the face (add layout/content to it); its parent is the "Panel" node.</summary>
        public static Transform CreatePanel(Transform parent, Vector2 size)
        {
            var card = Card(parent, "Panel", size, Color.white, 56, out var face);
            Place((RectTransform)card, new Vector2(0.5f, 0.5f), Vector2.zero, size);
            face.gameObject.AddComponent<UIGradient>().Set(Theme.panelTop, Theme.panelBottom);
            return face;
        }

        /// <summary>Wrapper with a soft shadow and a rounded face filling it. Works inside layout groups.</summary>
        public static Transform Card(Transform parent, string name, Vector2 size, Color color, float radius, out RectTransform face)
        {
            var wrapper = new GameObject(name, typeof(RectTransform));
            wrapper.transform.SetParent(parent, false);
            ((RectTransform)wrapper.transform).sizeDelta = size;
            var shadow = Img(wrapper.transform, "Shadow", Theme.softShadow, Theme.shadow);
            shadow.type = Image.Type.Sliced;
            Stretch(shadow.gameObject, wrapper.transform);
            shadow.rectTransform.offsetMin = new Vector2(-30, -42);
            shadow.rectTransform.offsetMax = new Vector2(30, 18);
            var faceImg = Img(wrapper.transform, "Face", Theme.rounded, color, radius);
            Stretch(faceImg.gameObject, wrapper.transform);
            faceImg.raycastTarget = true; // blocks clicks through panels
            face = faceImg.rectTransform;
            return wrapper.transform;
        }

        public static Transform Vertical(Transform panel, int padding, float spacing) => VerticalPadded(panel, padding, padding, padding, spacing);

        public static Transform VerticalPadded(Transform panel, int horizontal, int top, int bottom, float spacing)
        {
            var v = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(horizontal, horizontal, top, bottom);
            v.spacing = spacing;
            v.childAlignment = TextAnchor.UpperCenter;
            v.childControlWidth = v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
            return panel;
        }

        public static Transform HRow(Transform parent, string name, float height, float spacing)
        {
            var row = new GameObject(name, typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            row.transform.SetParent(parent, false);
            row.GetComponent<LayoutElement>().preferredHeight = height;
            var h = row.GetComponent<HorizontalLayoutGroup>();
            h.spacing = spacing;
            h.childAlignment = TextAnchor.MiddleCenter;
            h.childControlWidth = h.childControlHeight = true;
            h.childForceExpandWidth = true;
            h.childForceExpandHeight = true;
            return row.transform;
        }

        /// <summary>Settings-style row: rounded surface, optional icon bubble, label; add the control to the returned row.</summary>
        public static Transform Row(Transform parent, string label, string key, string icon = null)
        {
            var bg = Img(parent, label + "Row", Theme.rounded, Theme.surface, 30);
            bg.gameObject.AddComponent<LayoutElement>().preferredHeight = 112;
            var h = bg.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.padding = new RectOffset(20, 28, 0, 0);
            h.spacing = 22;
            h.childAlignment = TextAnchor.MiddleLeft;
            h.childControlWidth = h.childControlHeight = true;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = false;
            if (icon != null) IconBubble(bg.transform, "Icon", icon, Theme.primaryTop, Theme.primaryBottom, 76);
            var text = Text(bg.transform, "Label", label, 40, FontStyles.Normal, 112);
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.GetComponent<LayoutElement>().preferredWidth = 300;
            Localized(text, key);
            return bg.transform;
        }

        public static TMP_Text Text(Transform parent, string name, string value, float size, FontStyles style, float height)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<TextMeshProUGUI>();
            bool display = (style & FontStyles.Bold) != 0;
            t.font = display ? Theme.displayFont : Theme.font;
            t.fontStyle = display ? style & ~FontStyles.Bold : style;
            t.fontSize = size;
            t.alignment = TextAlignmentOptions.Center;
            t.color = Theme.text;
            t.raycastTarget = false;
            t.text = value;
            go.GetComponent<LayoutElement>().preferredHeight = height;
            ((RectTransform)go.transform).sizeDelta = new Vector2(400, height);
            return t;
        }

        /// <summary>White text with the display shadow material, for use on colored or gradient backgrounds.</summary>
        public static void OnColor(TMP_Text t, Color? color = null)
        {
            t.color = color ?? Theme.textOnColor;
            t.font = Theme.displayFont;
            if (Theme.displayShadowMaterial != null) t.fontSharedMaterial = Theme.displayShadowMaterial;
        }

        public static TMP_Text Icon(Transform parent, string name, string glyph, float size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<TextMeshProUGUI>();
            t.font = Theme.iconFont;
            t.text = glyph;
            t.fontSize = size;
            t.color = color;
            t.alignment = TextAlignmentOptions.Center;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Overflow;
            ((RectTransform)go.transform).sizeDelta = new Vector2(size, size);
            return t;
        }

        /// <summary>Circle with a gradient and a white icon (settings rows, currency chips, shop items).</summary>
        public static Image IconBubble(Transform parent, string name, string glyph, Color top, Color bottom, float size)
        {
            var bubble = Img(parent, name, Theme.circle, Color.white);
            bubble.gameObject.AddComponent<UIGradient>().Set(top, bottom);
            var le = bubble.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = le.preferredHeight = le.minWidth = le.minHeight = size;
            bubble.rectTransform.sizeDelta = new Vector2(size, size);
            var icon = Icon(bubble.transform, "Glyph", glyph, size * 0.58f, Color.white);
            Stretch(icon.gameObject, bubble.transform);
            return bubble;
        }

        public static Button Button(Transform parent, string name, string key, out TMP_Text label) =>
            Button(parent, name, key, out label, UIButtonStyle.Primary, null, 128);

        /// <summary>
        /// Chunky game button: darker "edge" image with a raised gradient face, gloss and optional icon. The face
        /// sinks on press (UIButtonFeedback). Corner radius follows the height so short buttons become pills.
        /// </summary>
        public static Button Button(Transform parent, string name, string key, out TMP_Text label, UIButtonStyle style, string icon, float height)
        {
            var c = Theme.Button(style);
            float radius = Mathf.Min(height / 2f, 46f);
            var root = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            root.transform.SetParent(parent, false);
            ((RectTransform)root.transform).sizeDelta = new Vector2(420, height);
            root.GetComponent<LayoutElement>().preferredHeight = height;
            var edge = root.GetComponent<Image>();
            Rounded(edge, c.edge, radius);

            var face = Img(root.transform, "Face", Theme.rounded, Color.white, radius);
            Stretch(face.gameObject, root.transform);
            face.rectTransform.offsetMin = new Vector2(0, 10);
            face.gameObject.AddComponent<UIGradient>().Set(c.top, c.bottom);
            var gloss = Img(face.transform, "Gloss", Theme.gloss, new Color(1, 1, 1, style == UIButtonStyle.Secondary ? 0.7f : 0.5f), radius);
            gloss.rectTransform.anchorMin = new Vector2(0, 0.4f);
            gloss.rectTransform.anchorMax = Vector2.one;
            gloss.rectTransform.offsetMin = new Vector2(6, 0);
            gloss.rectTransform.offsetMax = new Vector2(-6, -4);

            var content = new GameObject("Content", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            Stretch(content, face.transform);
            ((RectTransform)content.transform).offsetMin = new Vector2(24, 0);
            ((RectTransform)content.transform).offsetMax = new Vector2(-24, 0);
            var h = content.GetComponent<HorizontalLayoutGroup>();
            h.childAlignment = TextAnchor.MiddleCenter;
            h.spacing = 14;
            h.childControlWidth = h.childControlHeight = true;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = true;
            if (icon != null)
            {
                var ic = Icon(content.transform, "Icon", icon, height * 0.42f, c.label);
                ic.gameObject.AddComponent<LayoutElement>().preferredWidth = height * 0.46f;
            }
            label = Text(content.transform, "Label", "", Mathf.Round(height * 0.34f), FontStyles.Bold, height);
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.enableAutoSizing = true;
            label.fontSizeMax = label.fontSize;
            label.fontSizeMin = label.fontSize * 0.6f;
            if (icon == null) label.GetComponent<LayoutElement>().flexibleWidth = 1f; // text-only: fill, centred
            if (c.label == Theme.textOnColor) OnColor(label);
            else label.color = c.label;
            if (key != null) Localized(label, key);

            var button = root.GetComponent<Button>();
            button.targetGraphic = face;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = Color.white;
            colors.selectedColor = Color.white;
            colors.pressedColor = new Color(0.9f, 0.9f, 0.93f);
            colors.disabledColor = new Color(0.7f, 0.7f, 0.74f, 0.75f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            AddFeedback(root, face.rectTransform);
            return button;
        }

        /// <summary>Round icon button with the same raised look.</summary>
        public static Button IconButton(Transform parent, string name, string glyph, UIButtonStyle style, float size)
        {
            var c = Theme.Button(style);
            var root = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            root.transform.SetParent(parent, false);
            ((RectTransform)root.transform).sizeDelta = new Vector2(size, size);
            var le = root.GetComponent<LayoutElement>();
            le.preferredWidth = le.preferredHeight = size;
            Rounded(root.GetComponent<Image>(), c.edge, size / 2f);
            var face = Img(root.transform, "Face", Theme.rounded, Color.white, size / 2f);
            Stretch(face.gameObject, root.transform);
            face.rectTransform.offsetMin = new Vector2(0, 9);
            face.gameObject.AddComponent<UIGradient>().Set(c.top, c.bottom);
            var gloss = Img(face.transform, "Gloss", Theme.gloss, new Color(1, 1, 1, 0.5f), size / 2f);
            Stretch(gloss.gameObject, face.transform);
            gloss.rectTransform.offsetMin = new Vector2(5, size * 0.35f);
            gloss.rectTransform.offsetMax = new Vector2(-5, -3);
            var icon = Icon(face.transform, "Icon", glyph, size * 0.52f, c.label);
            Stretch(icon.gameObject, face.transform);
            var button = root.GetComponent<Button>();
            button.targetGraphic = face;
            var colors = button.colors;
            colors.pressedColor = new Color(0.9f, 0.9f, 0.93f);
            colors.disabledColor = new Color(0.7f, 0.7f, 0.74f, 0.75f);
            button.colors = colors;
            AddFeedback(root, face.rectTransform);
            return button;
        }

        /// <summary>White pill with a colored icon bubble on the left; returns the value text (put CurrencyDisplay on it).</summary>
        public static TMP_Text Chip(Transform parent, string name, string glyph, Color top, Color bottom, float width, float height = 100)
        {
            var card = Card(parent, name, new Vector2(width, height), WithAlpha(Color.white, 0.96f), height / 2f, out var face);
            var le = card.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = width;
            le.preferredHeight = height;
            var shadow = card.Find("Shadow") as RectTransform;
            shadow.offsetMin = new Vector2(-22, -30);
            shadow.offsetMax = new Vector2(22, 12);
            var bubble = IconBubble(face, "Icon", glyph, top, bottom, height - 14);
            Place(bubble.rectTransform, new Vector2(0, 0.5f), new Vector2(7, 0), new Vector2(height - 14, height - 14));
            bubble.GetComponent<LayoutElement>().ignoreLayout = true;
            var value = Text(face, "Value", "0", height * 0.44f, FontStyles.Bold, height);
            value.textWrappingMode = TextWrappingModes.NoWrap;
            value.enableAutoSizing = true;
            value.fontSizeMax = value.fontSize;
            value.fontSizeMin = value.fontSize * 0.6f;
            Stretch(value.gameObject, face);
            value.rectTransform.offsetMin = new Vector2(height, 0);
            value.rectTransform.offsetMax = new Vector2(-24, 0);
            value.GetComponent<LayoutElement>().ignoreLayout = true;
            return value;
        }

        /// <summary>The placeable wrapper of a chip, from the value text <see cref="Chip"/> returns.</summary>
        public static RectTransform ChipRoot(TMP_Text chipValue) => (RectTransform)chipValue.transform.parent.parent;

        public static TMP_Text Badge(Transform parent, string name, float size = 58)
        {
            var bubble = Img(parent, name, Theme.circle, Theme.badge);
            Place(bubble.rectTransform, new Vector2(1, 1), new Vector2(8, 8), new Vector2(size, size));
            if (bubble.TryGetComponent<LayoutElement>(out var le)) le.ignoreLayout = true;
            var text = Text(bubble.transform, "Count", "0", size * 0.52f, FontStyles.Bold, size);
            Stretch(text.gameObject, bubble.transform);
            text.color = Color.white;
            return text;
        }

        public static Slider Slider(Transform row, string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Slider), typeof(LayoutElement));
            go.transform.SetParent(row, false);
            var le = go.GetComponent<LayoutElement>();
            le.flexibleWidth = 1;
            le.preferredHeight = 80;
            var track = Img(go.transform, "Background", Theme.rounded, new Color32(0xD5, 0xDB, 0xE8, 0xFF), 13);
            Anchor(track.rectTransform, new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0, 26));
            var fillArea = new GameObject("Fill Area", typeof(RectTransform));
            fillArea.transform.SetParent(go.transform, false);
            Anchor((RectTransform)fillArea.transform, new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0, 26));
            var fill = Img(fillArea.transform, "Fill", Theme.rounded, Color.white, 13);
            fill.gameObject.AddComponent<UIGradient>().Set(Theme.primaryTop, Theme.primaryBottom);
            fill.rectTransform.sizeDelta = Vector2.zero;
            var handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
            Stretch(handleArea, go.transform);
            ((RectTransform)handleArea.transform).offsetMin = new Vector2(30, 0);
            ((RectTransform)handleArea.transform).offsetMax = new Vector2(-30, 0);
            var handle = Img(handleArea.transform, "Handle", Theme.circle, Color.white);
            handle.raycastTarget = true;
            handle.rectTransform.anchorMin = new Vector2(0, 0.5f);
            handle.rectTransform.anchorMax = new Vector2(0, 0.5f);
            handle.rectTransform.sizeDelta = new Vector2(62, 62);
            handle.gameObject.AddComponent<UIGradient>().Set(Color.white, new Color(0.88f, 0.9f, 0.95f));
            handle.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(0, -4);
            var slider = go.GetComponent<Slider>();
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 1f;
            handle.rectTransform.sizeDelta = new Vector2(62, -18); // slider stretches the handle vertically
            return slider;
        }

        public static Toggle Switch(Transform row, string name)
        {
            var spacer = new GameObject("Spacer", typeof(RectTransform), typeof(LayoutElement));
            spacer.transform.SetParent(row, false);
            spacer.GetComponent<LayoutElement>().flexibleWidth = 1;
            var go = new GameObject(name, typeof(RectTransform), typeof(Toggle), typeof(LayoutElement));
            go.transform.SetParent(row, false);
            var le = go.GetComponent<LayoutElement>();
            le.preferredWidth = 132;
            le.preferredHeight = 76;
            var track = Img(go.transform, "Track", Theme.rounded, Theme.secondaryEdge, 38);
            track.raycastTarget = true;
            Stretch(track.gameObject, go.transform);
            var knob = Img(track.transform, "Knob", Theme.circle, Color.white);
            knob.rectTransform.anchorMin = knob.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            knob.rectTransform.sizeDelta = new Vector2(62, 62);
            knob.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(0, -3);
            var toggle = go.GetComponent<Toggle>();
            toggle.targetGraphic = track;
            toggle.transition = Selectable.Transition.None;
            toggle.graphic = null;
            var sw = go.AddComponent<UIToggleSwitch>();
            var so = new SerializedObject(sw);
            so.FindProperty("track").objectReferenceValue = track;
            so.FindProperty("knob").objectReferenceValue = knob.rectTransform;
            so.FindProperty("onColor").colorValue = Theme.successBottom;
            so.FindProperty("offColor").colorValue = Theme.secondaryEdge;
            so.ApplyModifiedPropertiesWithoutUndo();
            return toggle;
        }

        public static TMP_Dropdown Dropdown(Transform row)
        {
            var go = TMP_DefaultControls.CreateDropdown(TmpResources());
            go.transform.SetParent(row, false);
            var le = go.AddComponent<LayoutElement>();
            le.flexibleWidth = 1;
            le.preferredHeight = 84;
            Rounded(go.GetComponent<Image>(), Color.white, 26);
            foreach (var t in go.GetComponentsInChildren<TMP_Text>(true))
            {
                t.font = Theme.font;
                t.color = Theme.text;
                t.fontSize = 38;
            }
            var arrow = go.transform.Find("Arrow") as RectTransform;
            if (arrow != null)
            {
                arrow.GetComponent<Image>().color = Theme.textMuted;
                arrow.sizeDelta = new Vector2(36, 36);
                arrow.anchoredPosition = new Vector2(-30, 0);
            }
            var template = go.transform.Find("Template") as RectTransform;
            if (template != null)
            {
                Rounded(template.GetComponent<Image>(), Color.white, 26);
                template.sizeDelta = new Vector2(template.sizeDelta.x, 560);
                template.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(0, -6);
                var item = template.Find("Viewport/Content/Item") as RectTransform;
                if (item != null)
                {
                    item.sizeDelta = new Vector2(item.sizeDelta.x, 84);
                    var itemBg = item.Find("Item Background");
                    if (itemBg != null) itemBg.GetComponent<Image>().color = Theme.surface;
                    var check = item.Find("Item Checkmark");
                    if (check != null) check.GetComponent<Image>().color = Theme.primaryBottom;
                }
                var content = template.Find("Viewport/Content") as RectTransform;
                if (content != null) content.sizeDelta = new Vector2(content.sizeDelta.x, 84);
            }
            return go.GetComponent<TMP_Dropdown>();
        }

        /// <summary>Round close button in a panel's top-right corner (outside layout).</summary>
        public static Button CornerClose(Transform panelFace)
        {
            var close = IconButton(panelFace, "Close", UIIcons.Close, UIButtonStyle.Secondary, 100);
            close.GetComponent<LayoutElement>().ignoreLayout = true;
            Place((RectTransform)close.transform, new Vector2(1, 1), new Vector2(-26, -26), new Vector2(100, 100));
            return close;
        }

        public static GameObject Overlay(Transform parent, string name, Color color, float radius = 0f)
        {
            var img = Img(parent, name, radius > 0 ? Theme.rounded : null, color, radius);
            img.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            Stretch(img.gameObject, parent);
            img.gameObject.SetActive(false);
            return img.gameObject;
        }

        public static Image Img(Transform parent, string name, Sprite sprite, Color color, float radius = 0f)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.raycastTarget = false;
            if (radius > 0f && sprite == Theme.rounded) Rounded(img, color, radius);
            else
            {
                img.sprite = sprite;
                img.color = color;
            }
            return img;
        }

        /// <summary>Rounded 9-slice with an exact corner radius in canvas units (sprite radius is 48px).</summary>
        public static void Rounded(Image img, Color color, float radius)
        {
            img.sprite = Theme.rounded;
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = 48f / Mathf.Max(1f, radius);
            img.color = color;
        }

        public static void Place(RectTransform rt, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            if (rt.TryGetComponent<LayoutElement>(out var le)) le.ignoreLayout = true;
        }

        private static void Anchor(RectTransform rt, Vector2 min, Vector2 max, Vector2 size)
        {
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.sizeDelta = size;
            rt.anchoredPosition = Vector2.zero;
        }

        public static void Spacer(Transform parent, float height)
        {
            var s = new GameObject("Spacer", typeof(RectTransform), typeof(LayoutElement));
            s.transform.SetParent(parent, false);
            s.GetComponent<LayoutElement>().preferredHeight = height;
        }

        public static Color WithAlpha(Color c, float a) => new Color(c.r, c.g, c.b, a);

        private static void AddFeedback(GameObject root, RectTransform face)
        {
            var fb = root.AddComponent<UIButtonFeedback>();
            var so = new SerializedObject(fb);
            so.FindProperty("face").objectReferenceValue = face;
            so.ApplyModifiedPropertiesWithoutUndo();
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
