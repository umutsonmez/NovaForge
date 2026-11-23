using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace NovaForge.Tiltwell.Unity
{
    /// <summary>Paint colors and generated sprites (no art assets needed; a game can swap them later).</summary>
    public static class TiltwellVisuals
    {
        public static Color Of(Paint p) => p switch
        {
            Paint.Red => new Color32(0xE8, 0x48, 0x55, 0xFF),
            Paint.Yellow => new Color32(0xF6, 0xC9, 0x3B, 0xFF),
            Paint.Blue => new Color32(0x3B, 0x82, 0xF0, 0xFF),
            Paint.Orange => new Color32(0xF2, 0x8C, 0x28, 0xFF),
            Paint.Purple => new Color32(0x9B, 0x59, 0xD0, 0xFF),
            Paint.Green => new Color32(0x3F, 0xB9, 0x6B, 0xFF),
            Paint.Brown => new Color32(0x8A, 0x5A, 0x3C, 0xFF),
            _ => Color.white,
        };

        public static readonly Color Background = new Color32(0x1B, 0x1E, 0x2B, 0xFF);
        public static readonly Color Floor = new Color32(0x2C, 0x31, 0x45, 0xFF);
        public static readonly Color Wall = new Color32(0x12, 0x14, 0x1D, 0xFF);
        public static readonly Color Hole = new Color32(0x05, 0x06, 0x0A, 0xFF);

        private static Sprite _circle, _ring, _rounded;
        public static Sprite Circle => _circle != null ? _circle : _circle = Make(128, (d, r) => d <= r - 1 ? 1f : Mathf.Clamp01(r - d));
        public static Sprite Ring => _ring != null ? _ring : _ring = Make(128, (d, r) =>
        {
            float outer = Mathf.Clamp01(r - d), inner = Mathf.Clamp01(d - r * 0.72f);
            return Mathf.Min(outer, inner);
        });
        public static Sprite Rounded => _rounded != null ? _rounded : _rounded = MakeRounded(64, 14);

        private static Sprite Make(int size, System.Func<float, float, float> alpha)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            float r = size / 2f;
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(255 * alpha(d, r)));
                }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        private static Sprite MakeRounded(int size, int radius)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float cx = Mathf.Clamp(x + 0.5f, radius, size - radius), cy = Mathf.Clamp(y + 0.5f, radius, size - radius);
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(255 * Mathf.Clamp01(radius - d)));
                }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect,
                new Vector4(radius, radius, radius, radius));
        }
    }

    /// <summary>
    /// Renders a board inside its RectTransform (fit to the shorter side) and animates tilt results.
    /// Pure presentation: all rules live in PuzzleSession.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class BoardView : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float cellsPerSecond = 16f;
        [SerializeField, Min(0f)] private float settleDuration = 0.18f;

        private readonly Dictionary<int, Image> _drops = new Dictionary<int, Image>();
        private readonly List<Image> _wells = new List<Image>();
        private readonly List<Image> _wellFills = new List<Image>();
        private RectTransform _rect, _layer;
        private Board _board;
        private float _cell;

        public bool IsAnimating { get; private set; }
        public float CellSize => _cell;

        private void Awake() => _rect = (RectTransform)transform;

        public void Build(Board board, PuzzleState state)
        {
            _board = board;
            if (_layer != null) Destroy(_layer.gameObject);
            _drops.Clear();
            _wells.Clear();
            _wellFills.Clear();

            _layer = NewRect("Board", _rect);
            var size = _rect.rect.size;
            _cell = Mathf.Floor(Mathf.Min(size.x / board.Width, size.y / board.Height));
            _layer.sizeDelta = new Vector2(_cell * board.Width, _cell * board.Height);

            for (int y = 0; y < board.Height; y++)
                for (int x = 0; x < board.Width; x++)
                {
                    var cell = board.At(x, y);
                    var tile = NewImage("Cell", _layer, TiltwellVisuals.Rounded, cell == Cell.Wall ? TiltwellVisuals.Wall : TiltwellVisuals.Floor);
                    tile.type = Image.Type.Sliced;
                    Place(tile.rectTransform, board.Pos(x, y), 0.94f);
                    if (cell == Cell.Hole)
                        Place(NewImage("Hole", _layer, TiltwellVisuals.Circle, TiltwellVisuals.Hole).rectTransform, board.Pos(x, y), 0.7f);
                }

            for (int w = 0; w < board.Wells.Count; w++)
            {
                var color = TiltwellVisuals.Of(board.WellColors[w]);
                var fill = NewImage("WellFill", _layer, TiltwellVisuals.Circle, color);
                Place(fill.rectTransform, board.Wells[w], 0.5f);
                var ring = NewImage("Well", _layer, TiltwellVisuals.Ring, color);
                Place(ring.rectTransform, board.Wells[w], 0.86f);
                _wellFills.Add(fill);
                _wells.Add(ring);
            }
            Sync(state);
        }

        /// <summary>Snaps the view to a state (after undo/restart).</summary>
        public void Sync(PuzzleState state)
        {
            foreach (var d in _drops.Values) if (d != null) Destroy(d.gameObject);
            _drops.Clear();
            for (int w = 0; w < _wells.Count; w++) SetWellFilled(w, !state.IsWellOpen(w), instant: true);
            foreach (var d in state.Droplets)
            {
                var img = NewImage("Drop" + d.Id, _layer, TiltwellVisuals.Circle, TiltwellVisuals.Of(d.Color));
                Place(img.rectTransform, d.Pos, 0.72f);
                _drops[d.Id] = img;
            }
        }

        public async Task Animate(TiltResult result)
        {
            IsAnimating = true;
            try
            {
                // Phase 1: everything slides at the same speed.
                var movers = new List<(RectTransform rt, Vector2 from, Vector2 to, float duration)>();
                float total = 0f;
                foreach (var s in result.Steps)
                {
                    if (s.Fate == DropletFate.Delivered || !_drops.TryGetValue(s.Id, out var img)) continue;
                    float dist = Mathf.Abs(_board.X(s.To) - _board.X(s.From)) + Mathf.Abs(_board.Y(s.To) - _board.Y(s.From));
                    if (dist <= 0f) continue;
                    float duration = Mathf.Max(0.05f, dist / cellsPerSecond);
                    movers.Add((img.rectTransform, CellCenter(s.From), CellCenter(s.To), duration));
                    total = Mathf.Max(total, duration);
                }
                for (float t = 0f; t < total;)
                {
                    await Awaitable.NextFrameAsync();
                    if (this == null) return;
                    t += Time.unscaledDeltaTime;
                    foreach (var m in movers)
                    {
                        if (m.rt == null) continue;
                        float k = Mathf.Clamp01(t / m.duration);
                        m.rt.anchoredPosition = Vector2.LerpUnclamped(m.from, m.to, EaseOutBack(k));
                    }
                }

                // Phase 2: merges recolor, holes swallow, wells fill.
                var shrinking = new List<RectTransform>();
                var pulsing = new List<RectTransform>();
                foreach (var s in result.Steps)
                {
                    if (!_drops.TryGetValue(s.Id, out var img)) continue;
                    switch (s.Fate)
                    {
                        case DropletFate.Merged:
                            Destroy(img.gameObject);
                            _drops.Remove(s.Id);
                            if (_drops.TryGetValue(s.Other, out var host))
                            {
                                host.color = TiltwellVisuals.Of(s.Color);
                                pulsing.Add(host.rectTransform);
                            }
                            break;
                        case DropletFate.FellInHole:
                        case DropletFate.Delivered:
                            shrinking.Add(img.rectTransform);
                            _drops.Remove(s.Id);
                            if (s.Fate == DropletFate.Delivered) SetWellFilled(s.Other, true, instant: false);
                            break;
                    }
                }
                for (float t = 0f; t < settleDuration && (shrinking.Count > 0 || pulsing.Count > 0);)
                {
                    await Awaitable.NextFrameAsync();
                    if (this == null) return;
                    t += Time.unscaledDeltaTime;
                    float k = Mathf.Clamp01(t / settleDuration);
                    foreach (var rt in shrinking) if (rt != null) rt.localScale = Vector3.one * (1f - k);
                    foreach (var rt in pulsing) if (rt != null) rt.localScale = Vector3.one * (1f + 0.25f * Mathf.Sin(k * Mathf.PI));
                    for (int w = 0; w < _wellFills.Count; w++) UpdateWellPulse(w, k);
                }
                foreach (var rt in shrinking) if (rt != null) Destroy(rt.gameObject);
                foreach (var rt in pulsing) if (rt != null) rt.localScale = Vector3.one;
            }
            finally { IsAnimating = false; }
        }

        /// <summary>Center of a cell in board-layer coordinates (for hint arrows, effects).</summary>
        public Vector2 CellCenter(int pos)
        {
            float x = (_board.X(pos) + 0.5f) * _cell - _layer.sizeDelta.x / 2f;
            float y = -((_board.Y(pos) + 0.5f) * _cell - _layer.sizeDelta.y / 2f);
            return new Vector2(x, y);
        }

        private readonly HashSet<int> _pulseWells = new HashSet<int>();

        private void SetWellFilled(int w, bool filled, bool instant)
        {
            if (w < 0 || w >= _wellFills.Count) return;
            var c = _wellFills[w].color;
            c.a = filled ? 1f : 0.22f;
            _wellFills[w].color = c;
            _wellFills[w].rectTransform.localScale = Vector3.one * (filled ? 1.5f : 1f);
            if (filled && !instant) _pulseWells.Add(w);
        }

        private void UpdateWellPulse(int w, float k)
        {
            if (!_pulseWells.Contains(w)) return;
            _wellFills[w].rectTransform.localScale = Vector3.one * (1.5f + 0.3f * Mathf.Sin(k * Mathf.PI));
            if (k >= 1f) _pulseWells.Remove(w);
        }

        private void Place(RectTransform rt, int pos, float scale)
        {
            rt.sizeDelta = Vector2.one * _cell * scale;
            rt.anchoredPosition = CellCenter(pos);
        }

        private static float EaseOutBack(float x)
        {
            const float c1 = 1.2f, c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(x - 1f, 3) + c1 * Mathf.Pow(x - 1f, 2);
        }

        private static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            return rt;
        }

        private static Image NewImage(string name, Transform parent, Sprite sprite, Color color)
        {
            var rt = NewRect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }
    }
}
