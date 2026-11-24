using System.Collections.Generic;
using NovaForge.Core.Foundation;
using NovaForge.Core.UI;
using NovaForge.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NovaForge.Tiltwell.Unity
{
    public static class TiltwellViewIds
    {
        public const string Levels = "levels";
    }

    /// <summary>
    /// Level map (view id "levels"): a winding road from the bottom (level 1) to the top. Each tier is a "world" with
    /// its own color and banner; the travelled part of the road is gold, the rest dashed. Opens scrolled to the
    /// current level.
    /// </summary>
    public sealed class LevelMapView : UIView
    {
        [SerializeField] private ScrollRect scroll;
        [SerializeField] private RectTransform content;
        [SerializeField] private LevelMapNode nodeTemplate;
        [SerializeField] private RectTransform bannerTemplate;
        [SerializeField] private RectTransform zoneTemplate;
        [SerializeField] private UIPolyline roadBase;
        [SerializeField] private UIPolyline roadDone;
        [SerializeField] private UIPolyline roadTodo;
        [SerializeField] private Button backButton;
        [SerializeField] private TMP_Text starsLabel;

        [Header("Layout")]
        [SerializeField] private float spacing = 250f;
        [SerializeField] private float amplitude = 280f;
        [SerializeField] private float frequency = 0.85f;
        [SerializeField] private float bottomPadding = 260f;
        [SerializeField] private float topPadding = 420f;
        [SerializeField] private float worldGap = 170f;

        [Header("World colors (top, bottom, edge) per tier; cycles")]
        [SerializeField] private Color[] worldTop =
        {
            new Color32(0x6F, 0xA8, 0xFF, 0xFF), new Color32(0x5C, 0xDB, 0x95, 0xFF), new Color32(0xFF, 0xBE, 0x55, 0xFF),
            new Color32(0xFF, 0x86, 0xBE, 0xFF), new Color32(0xAD, 0x8B, 0xFF, 0xFF),
            new Color32(0x4F, 0xD8, 0xD0, 0xFF), new Color32(0xFF, 0x8A, 0x7A, 0xFF), new Color32(0x7E, 0xC8, 0xFF, 0xFF),
            new Color32(0xB8, 0xE0, 0x5A, 0xFF), new Color32(0x8C, 0x9B, 0xFF, 0xFF),
        };
        [SerializeField] private Color[] worldBottom =
        {
            new Color32(0x3A, 0x78, 0xF2, 0xFF), new Color32(0x26, 0xB0, 0x66, 0xFF), new Color32(0xF5, 0x8E, 0x12, 0xFF),
            new Color32(0xEF, 0x4B, 0x93, 0xFF), new Color32(0x7C, 0x4D, 0xEE, 0xFF),
            new Color32(0x14, 0xA8, 0x9E, 0xFF), new Color32(0xF0, 0x4E, 0x3C, 0xFF), new Color32(0x1E, 0x9B, 0xE8, 0xFF),
            new Color32(0x7C, 0xB3, 0x1C, 0xFF), new Color32(0x4A, 0x5A, 0xE0, 0xFF),
        };
        [SerializeField] private Color[] worldEdge =
        {
            new Color32(0x25, 0x56, 0xB8, 0xFF), new Color32(0x17, 0x84, 0x4B, 0xFF), new Color32(0xC0, 0x67, 0x00, 0xFF),
            new Color32(0xBB, 0x2C, 0x6E, 0xFF), new Color32(0x58, 0x2F, 0xBE, 0xFF),
            new Color32(0x0A, 0x7A, 0x72, 0xFF), new Color32(0xB8, 0x2E, 0x20, 0xFF), new Color32(0x10, 0x6E, 0xB0, 0xFF),
            new Color32(0x55, 0x80, 0x10, 0xFF), new Color32(0x30, 0x3C, 0xA8, 0xFF),
        };

        private readonly List<GameObject> _spawned = new List<GameObject>();

        protected override void Awake()
        {
            base.Awake();
            nodeTemplate.gameObject.SetActive(false);
            if (bannerTemplate != null) bannerTemplate.gameObject.SetActive(false);
            if (zoneTemplate != null) zoneTemplate.gameObject.SetActive(false);
            backButton.onClick.AddListener(Close);
        }

        protected override void OnShowing(object args)
        {
            foreach (var go in _spawned) Destroy(go);
            _spawned.Clear();

            var app = TiltwellApp.Instance;
            var progression = Core.Progression;
            int count = progression.Catalog.Count;
            var currentId = app.NextPlayableLevelId();
            int current = progression.Catalog.IndexOf(currentId);

            // Positions: bottom-up, with a little extra gap where a new world starts.
            var points = new List<Vector2>(count);
            var tiers = new int[count];
            float y = bottomPadding;
            for (int i = 0; i < count; i++)
            {
                tiers[i] = TierOf(app, progression.Catalog.GetLevelId(i));
                if (i > 0) y += spacing + (tiers[i] != tiers[i - 1] ? worldGap : 0f);
                points.Add(new Vector2(Mathf.Sin(i * frequency) * amplitude, y));
            }
            float height = y + topPadding;
            content.sizeDelta = new Vector2(content.sizeDelta.x, height);

            // World zones + banners (behind the road).
            int totalStars = 0;
            for (int i = 0; i < count; i++)
            {
                if (i > 0 && tiers[i] == tiers[i - 1]) continue;
                int last = i;
                while (last + 1 < count && tiers[last + 1] == tiers[i]) last++;
                int w = WorldIndex(tiers[i]);
                if (zoneTemplate != null)
                {
                    var zone = Spawn(zoneTemplate);
                    float y0 = points[i].y - spacing * 0.6f, y1 = points[last].y + spacing * 0.6f;
                    zone.anchoredPosition = new Vector2(0, y0);
                    zone.sizeDelta = new Vector2(zone.sizeDelta.x, y1 - y0);
                    if (zone.TryGetComponent<UIGradient>(out var zg)) zg.Set(Fade(Color.Lerp(worldTop[w], Color.white, 0.72f), 0.85f), Fade(Color.Lerp(worldBottom[w], Color.white, 0.62f), 0.85f));
                    zone.SetAsFirstSibling();
                }
                if (bannerTemplate != null)
                {
                    var banner = Spawn(bannerTemplate);
                    banner.anchoredPosition = new Vector2(0, points[i].y - spacing * 0.5f - (i == 0 ? 40f : worldGap * 0.15f));
                    var bg = banner.GetComponentInChildren<UIGradient>(true);
                    if (bg != null) bg.Set(worldTop[w], worldBottom[w]);
                    var text = banner.GetComponentInChildren<TMP_Text>(true);
                    if (text != null) text.text = Core.Localization.Format(TiltwellStrings.WorldN, WorldNumber(tiers[i]));
                }
            }

            roadBase.SetPoints(points);
            int doneUpTo = Mathf.Clamp(current, 0, count - 1);
            roadDone.SetPoints(points.GetRange(0, doneUpTo + 1));
            roadDone.gameObject.SetActive(doneUpTo > 0);
            roadTodo.SetPoints(points.GetRange(doneUpTo, count - doneUpTo));

            for (int i = 0; i < count; i++)
            {
                var id = progression.Catalog.GetLevelId(i);
                int stars = progression.Get(id)?.BestStars ?? 0;
                totalStars += stars;
                var state = i == current ? LevelNodeState.Current
                    : progression.IsUnlocked(i) ? LevelNodeState.Completed
                    : LevelNodeState.Locked;
                var node = Instantiate(nodeTemplate, content, false);
                node.gameObject.SetActive(true);
                node.name = "Level" + (i + 1);
                _spawned.Add(node.gameObject);
                ((RectTransform)node.transform).anchoredPosition = points[i];
                int w = WorldIndex(tiers[i]);
                node.Set(i + 1, state, stars, worldTop[w], worldBottom[w], worldEdge[w]);
                node.Button.onClick.AddListener(() => Navigation.ResetToAsync(ViewIds.Gameplay, id).Forget());
            }
            // The current node draws on top of its neighbours (marker overlaps the road above).
            if (current >= 0 && current < _spawned.Count) _spawned[_spawned.Count - count + current].transform.SetAsLastSibling();

            if (starsLabel != null) starsLabel.text = totalStars + "/" + count * 3;

            Canvas.ForceUpdateCanvases();
            float viewport = ((RectTransform)scroll.viewport.transform).rect.height;
            float target = points[Mathf.Clamp(current, 0, count - 1)].y - viewport * 0.4f;
            scroll.verticalNormalizedPosition = height > viewport ? Mathf.Clamp01(target / (height - viewport)) : 0f;
            scroll.velocity = Vector2.zero;
        }

        private RectTransform Spawn(RectTransform template)
        {
            var rt = Instantiate(template, content, false);
            rt.gameObject.SetActive(true);
            _spawned.Add(rt.gameObject);
            return rt;
        }

        private static int TierOf(TiltwellApp app, string levelId)
        {
            var level = app.Levels.Find(l => l.Id == levelId);
            return level != null ? level.Tier : 1;
        }

        /// <summary>World number shown to players: tiers are 1-based in the level data.</summary>
        private static int WorldNumber(int tier) => Mathf.Max(1, tier);

        private int WorldIndex(int tier) => (Mathf.Max(1, tier) - 1) % worldTop.Length;

        private static Color Fade(Color c, float a) => new Color(c.r, c.g, c.b, a);
    }
}
