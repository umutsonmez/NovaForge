using System.Collections.Generic;
using System.Threading.Tasks;
using NovaForge.UI;
using UnityEngine;
using UnityEngine.UI;

namespace NovaForge.Tiltwell.Unity
{
    /// <summary>Paint colors, board palette and fallback sprites (no art assets needed; the scene assigns theme sprites).</summary>
    public static class TiltwellVisuals
    {
        public static Color Of(Paint p) => p switch
        {
            Paint.Red => new Color32(0xFF, 0x4F, 0x64, 0xFF),
            Paint.Yellow => new Color32(0xFF, 0xC8, 0x2E, 0xFF),
            Paint.Blue => new Color32(0x3D, 0x8B, 0xFF, 0xFF),
            Paint.Orange => new Color32(0xFF, 0x8A, 0x1F, 0xFF),
            Paint.Purple => new Color32(0xA2, 0x5C, 0xF0, 0xFF),
            Paint.Green => new Color32(0x2E, 0xC4, 0x6E, 0xFF),
            Paint.Brown => new Color32(0x9A, 0x63, 0x42, 0xFF),
            _ => Color.white,
        };

        public static readonly Color BackgroundTop = new Color32(0xA9, 0xD6, 0xFF, 0xFF);
        public static readonly Color BackgroundBottom = new Color32(0xE8, 0xDC, 0xFF, 0xFF);
        public static readonly Color Background = BackgroundBottom;
        public static readonly Color Panel = Color.white;
        public static readonly Color Floor = new Color32(0xEE, 0xF2, 0xFA, 0xFF);
        public static readonly Color FloorAlt = new Color32(0xE4, 0xE9, 0xF5, 0xFF);
        public static readonly Color WallTop = new Color32(0x97, 0xA3, 0xC9, 0xFF);
        public static readonly Color WallBottom = new Color32(0x76, 0x82, 0xAB, 0xFF);
        public static readonly Color WallEdge = new Color32(0x58, 0x63, 0x8C, 0xFF);
        public static readonly Color Hole = new Color32(0x26, 0x2B, 0x47, 0xFF);
        public static readonly Color Sticky = new Color32(0xF7, 0xD9, 0x8E, 0xFF);
        public static readonly Color StickyDots = new Color32(0xE3, 0xA9, 0x3A, 0xFF);
        public static readonly Color ArrowFloor = new Color32(0xC9, 0xDB, 0xFF, 0xFF);
        public static readonly Color ArrowGlyph = new Color32(0x24, 0x56, 0xD6, 0xFF);

        private static Sprite _circle, _ring, _rounded;
        public static Sprite Circle => _circle != null ? _circle : _circle = Make(128, (d, r) => d <= r - 1 ? 1f : Mathf.Clamp01(r - d));
        public static Sprite Ring => _ring != null ? _ring : _ring = Make(128, (d, r) =>
        {
            float outer = Mathf.Clamp01(r - d), inner = Mathf.Clamp01(d - r * 0.76f);
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
        [SerializeField, Min(0f)] private float settleDuration = 0.2f;

        [Header("Sprites (fallbacks are generated)")]
        [SerializeField] private Sprite rounded;
        [Tooltip("Corner radius of the rounded sprite in pixels (DefaultTheme: 48).")]
        [SerializeField] private float roundedRadiusPx = 48f;
        [SerializeField] private Sprite circle;
        [SerializeField] private Sprite softShadow;
        [Tooltip("Star sprite for the Star drop skin.")]
        [SerializeField] private Sprite star;
        [Tooltip("Icon font for arrow cells (Material Icons). Without it arrows show as a chevron of two bars.")]
        [SerializeField] private TMPro.TMP_FontAsset iconFont;

        private readonly Dictionary<int, Image> _drops = new Dictionary<int, Image>();
        private readonly List<Image> _wellFills = new List<Image>();
        private readonly List<Image> _wellGlows = new List<Image>();
        private RectTransform _rect, _layer, _fx;
        private Board _board;
        private float _cell;

        private struct Particle
        {
            public RectTransform Rt;
            public Image Img;
            public Vector2 Velocity;
            public float Age, Life, Size;
        }

        private readonly List<Particle> _particles = new List<Particle>();
        private readonly Stack<Image> _particlePool = new Stack<Image>();

        public bool IsAnimating { get; private set; }
        /// <summary>Droplet look; takes effect on the next Build/Sync.</summary>
        public DropSkin Skin { get; set; }
        public float CellSize => _cell;

        private Sprite RoundedSprite => rounded != null ? rounded : TiltwellVisuals.Rounded;
        private float RoundedPx => rounded != null ? roundedRadiusPx : 14f;
        private Sprite CircleSprite => circle != null ? circle : TiltwellVisuals.Circle;

        private void Awake() => _rect = (RectTransform)transform;

        public void Build(Board board, PuzzleState state)
        {
            _board = board;
            if (_layer != null) Destroy(_layer.parent == _rect ? _layer.gameObject : _layer.parent.gameObject);
            _drops.Clear();
            _wellFills.Clear();
            _wellGlows.Clear();
            ClearParticles();

            var size = _rect.rect.size;
            const float frame = 0.45f; // panel margin around the grid, in cells
            _cell = Mathf.Floor(Mathf.Min(size.x / (board.Width + frame * 2), size.y / (board.Height + frame * 2)));
            var grid = new Vector2(_cell * board.Width, _cell * board.Height);

            var root = NewRect("BoardRoot", _rect);
            root.sizeDelta = grid;

            // White panel with soft shadow.
            float pad = _cell * frame;
            if (softShadow != null)
            {
                var sh = NewImage("Shadow", root, softShadow, new Color(0.1f, 0.12f, 0.35f, 0.3f));
                sh.type = Image.Type.Sliced;
                sh.rectTransform.sizeDelta = grid + Vector2.one * (pad * 2 + 70);
                sh.rectTransform.anchoredPosition = new Vector2(0, -22);
            }
            var panel = NewImage("Panel", root, RoundedSprite, TiltwellVisuals.Panel);
            Slice(panel, _cell * 0.55f);
            panel.rectTransform.sizeDelta = grid + Vector2.one * pad * 2;
            panel.gameObject.AddComponent<UIGradient>().Set(Color.white, new Color(0.94f, 0.95f, 0.99f));

            _layer = NewRect("Board", root);
            _layer.sizeDelta = grid;

            for (int y = 0; y < board.Height; y++)
                for (int x = 0; x < board.Width; x++)
                {
                    int pos = board.Pos(x, y);
                    var cell = board.At(x, y);
                    if (cell == Cell.Wall)
                    {
                        var edge = NewImage("WallEdge", _layer, RoundedSprite, TiltwellVisuals.WallEdge);
                        Slice(edge, _cell * 0.2f);
                        Place(edge.rectTransform, pos, 0.94f);
                        var face = NewImage("Wall", _layer, RoundedSprite, Color.white);
                        Slice(face, _cell * 0.2f);
                        Place(face.rectTransform, pos, 0.94f);
                        face.rectTransform.sizeDelta -= new Vector2(0, _cell * 0.09f);
                        face.rectTransform.anchoredPosition += new Vector2(0, _cell * 0.045f);
                        face.gameObject.AddComponent<UIGradient>().Set(TiltwellVisuals.WallTop, TiltwellVisuals.WallBottom);
                        continue;
                    }
                    var tile = NewImage("Cell", _layer, RoundedSprite, (x + y) % 2 == 0 ? TiltwellVisuals.Floor : TiltwellVisuals.FloorAlt);
                    Slice(tile, _cell * 0.2f);
                    Place(tile.rectTransform, pos, 0.94f);
                    DrawSpecialFloor(cell, pos, tile);
                    if (cell == Cell.Hole)
                    {
                        var hole = NewImage("Hole", _layer, CircleSprite, TiltwellVisuals.Hole);
                        Place(hole.rectTransform, pos, 0.68f);
                        hole.gameObject.AddComponent<UIGradient>().Set(new Color(0.55f, 0.55f, 0.6f), Color.white);
                        var rim = NewImage("Rim", hole.transform, TiltwellVisuals.Ring, new Color(1f, 1f, 1f, 0.35f));
                        Stretch(rim.rectTransform);
                    }
                }

            for (int w = 0; w < board.Wells.Count; w++)
            {
                var color = TiltwellVisuals.Of(board.WellColors[w]);
                int pos = board.Wells[w];
                if (softShadow != null)
                {
                    var glow = NewImage("WellGlow", _layer, softShadow, Fade(color, 0f));
                    Place(glow.rectTransform, pos, 1.3f);
                    _wellGlows.Add(glow);
                }
                else _wellGlows.Add(null);
                var basin = NewImage("WellBasin", _layer, CircleSprite, Fade(color, 0.16f));
                Place(basin.rectTransform, pos, 0.84f);
                var fill = NewImage("WellFill", _layer, CircleSprite, color);
                Place(fill.rectTransform, pos, 0.5f);
                fill.gameObject.AddComponent<UIGradient>().Set(Color.white, new Color(0.85f, 0.85f, 0.9f));
                var ring = NewImage("Well", _layer, TiltwellVisuals.Ring, color);
                Place(ring.rectTransform, pos, 0.86f);
                _wellFills.Add(fill);
            }

            _fx = NewRect("Effects", root);
            _fx.sizeDelta = grid;
            Sync(state);
        }

        private void DrawSpecialFloor(Cell cell, int pos, Image tile)
        {
            if (cell == Cell.Sticky)
            {
                tile.color = TiltwellVisuals.Sticky;
                // A few honey dots so it reads as "sticky" without words.
                var dots = new[] { new Vector2(-0.2f, 0.18f), new Vector2(0.2f, 0.1f), new Vector2(-0.05f, -0.2f), new Vector2(0.22f, -0.22f) };
                float[] sizes = { 0.16f, 0.12f, 0.14f, 0.09f };
                for (int i = 0; i < dots.Length; i++)
                {
                    var dot = NewImage("Dot", _layer, CircleSprite, TiltwellVisuals.StickyDots);
                    Place(dot.rectTransform, pos, sizes[i]);
                    dot.rectTransform.anchoredPosition += dots[i] * _cell;
                }
                return;
            }
            var arrow = Board.ArrowDirection(cell);
            if (arrow.HasValue)
            {
                tile.color = TiltwellVisuals.ArrowFloor;
                float angle = arrow.Value switch { Tilt.Up => 0f, Tilt.Left => 90f, Tilt.Down => 180f, _ => 270f };
                if (iconFont != null)
                {
                    var go = new GameObject("Arrow", typeof(RectTransform), typeof(CanvasRenderer), typeof(TMPro.TextMeshProUGUI));
                    go.transform.SetParent(_layer, false);
                    var t = go.GetComponent<TMPro.TextMeshProUGUI>();
                    t.font = iconFont;
                    t.text = UIIcons.ArrowUp;
                    t.fontSize = _cell * 0.78f;
                    t.fontStyle = TMPro.FontStyles.Bold;
                    t.color = TiltwellVisuals.ArrowGlyph;
                    t.alignment = TMPro.TextAlignmentOptions.Center;
                    t.raycastTarget = false;
                    Place(t.rectTransform, pos, 0.9f);
                    t.rectTransform.localRotation = Quaternion.Euler(0, 0, angle);
                }
                else
                {
                    for (int side = -1; side <= 1; side += 2)
                    {
                        var bar = NewImage("Chevron", _layer, RoundedSprite, TiltwellVisuals.ArrowGlyph);
                        Place(bar.rectTransform, pos, 0.1f);
                        bar.rectTransform.sizeDelta = new Vector2(_cell * 0.1f, _cell * 0.36f);
                        var offset = Quaternion.Euler(0, 0, angle) * new Vector3(side * _cell * 0.1f, 0, 0);
                        bar.rectTransform.anchoredPosition += (Vector2)offset;
                        bar.rectTransform.localRotation = Quaternion.Euler(0, 0, angle - side * 40f);
                    }
                }
                return;
            }
            var pad = Board.PadColor(cell);
            if (pad != Paint.None)
            {
                // Diamond of paint, clearly different from round wells.
                var color = TiltwellVisuals.Of(pad);
                var under = NewImage("PadShade", _layer, RoundedSprite, Fade(color, 0.25f));
                Slice(under, _cell * 0.12f);
                Place(under.rectTransform, pos, 0.6f);
                under.rectTransform.localRotation = Quaternion.Euler(0, 0, 45f);
                var diamond = NewImage("Pad", _layer, RoundedSprite, color);
                Slice(diamond, _cell * 0.1f);
                Place(diamond.rectTransform, pos, 0.42f);
                diamond.rectTransform.localRotation = Quaternion.Euler(0, 0, 45f);
                diamond.gameObject.AddComponent<UIGradient>().Set(Color.white, new Color(0.82f, 0.82f, 0.88f));
            }
        }

        /// <summary>Snaps the view to a state (after undo/restart).</summary>
        public void Sync(PuzzleState state)
        {
            foreach (var d in _drops.Values) if (d != null) Destroy(Root(d).gameObject);
            _drops.Clear();
            for (int w = 0; w < _wellFills.Count; w++) SetWellFilled(w, !state.IsWellOpen(w), instant: true);
            foreach (var d in state.Droplets) _drops[d.Id] = CreateDrop(d.Id, d.Pos, TiltwellVisuals.Of(d.Color));
            if (_fx != null) _fx.SetAsLastSibling();
        }

        /// <summary>Drop = root (moves) with the skin's parts; returns the body (the tinted part).</summary>
        private Image CreateDrop(int id, int pos, Color color)
        {
            var root = NewRect("Drop" + id, _layer);
            Place(root, pos, 0.74f);
            return DropVisual.Create(root, _cell * 0.74f, color, Skin, new DropSprites(circle, rounded, roundedRadiusPx, star));
        }

        private static RectTransform Root(Image body) => (RectTransform)body.transform.parent;

        /// <param name="landed">Called once when every droplet has stopped sliding (before merges and wells resolve).</param>
        public async Task Animate(TiltResult result, System.Action<TiltResult> landed = null)
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
                    movers.Add((Root(img), CellCenter(s.From), CellCenter(s.To), duration));
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
                        // squash along the travel direction while moving
                        var dir = m.to - m.from;
                        float squash = k < 1f ? 0.1f : 0f;
                        m.rt.localScale = Mathf.Abs(dir.x) > Mathf.Abs(dir.y)
                            ? new Vector3(1f + squash, 1f - squash, 1f)
                            : new Vector3(1f - squash, 1f + squash, 1f);
                    }
                }
                foreach (var m in movers) if (m.rt != null) m.rt.localScale = Vector3.one;
                landed?.Invoke(result);

                // Phase 2: merges recolor, holes swallow, wells fill.
                var shrinking = new List<RectTransform>();
                var pulsing = new List<RectTransform>();
                foreach (var s in result.Steps)
                {
                    if (!_drops.TryGetValue(s.Id, out var img)) continue;
                    switch (s.Fate)
                    {
                        case DropletFate.Moved:
                            var painted = TiltwellVisuals.Of(s.Color);
                            if (img.color != painted)
                            {
                                img.color = painted;
                                pulsing.Add(Root(img));
                                Burst(Root(img).anchoredPosition, painted, 10);
                            }
                            break;
                        case DropletFate.Merged:
                            Destroy(Root(img).gameObject);
                            _drops.Remove(s.Id);
                            if (_drops.TryGetValue(s.Other, out var host))
                            {
                                host.color = TiltwellVisuals.Of(s.Color);
                                pulsing.Add(Root(host));
                                Burst(Root(host).anchoredPosition, host.color, 12);
                            }
                            break;
                        case DropletFate.FellInHole:
                        case DropletFate.Delivered:
                            shrinking.Add(Root(img));
                            _drops.Remove(s.Id);
                            if (s.Fate == DropletFate.Delivered)
                            {
                                SetWellFilled(s.Other, true, instant: false);
                                Burst(CellCenter(s.To), img.color, 18);
                            }
                            else Burst(CellCenter(s.To), new Color(0.3f, 0.33f, 0.5f), 6);
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
                    foreach (var rt in pulsing) if (rt != null) rt.localScale = Vector3.one * (1f + 0.28f * Mathf.Sin(k * Mathf.PI));
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
            c.a = filled ? 1f : 0.3f;
            _wellFills[w].color = c;
            _wellFills[w].rectTransform.localScale = Vector3.one * (filled ? 1.5f : 1f);
            var glow = _wellGlows[w];
            if (glow != null) glow.color = Fade(glow.color, filled ? 0.35f : 0f);
            if (filled && !instant) _pulseWells.Add(w);
        }

        private void UpdateWellPulse(int w, float k)
        {
            if (!_pulseWells.Contains(w)) return;
            _wellFills[w].rectTransform.localScale = Vector3.one * (1.5f + 0.3f * Mathf.Sin(k * Mathf.PI));
            if (k >= 1f) _pulseWells.Remove(w);
        }

        // ------------------------------------------------------------------ particles

        private void Burst(Vector2 at, Color color, int count)
        {
            if (_fx == null) return;
            for (int i = 0; i < count; i++)
            {
                var img = _particlePool.Count > 0 ? _particlePool.Pop() : NewImage("Spark", _fx, CircleSprite, Color.white);
                img.gameObject.SetActive(true);
                img.color = i % 3 == 0 ? Color.white : color;
                float size = _cell * Random.Range(0.08f, 0.17f);
                img.rectTransform.sizeDelta = Vector2.one * size;
                img.rectTransform.anchoredPosition = at;
                float a = (i / (float)count + Random.Range(-0.05f, 0.05f)) * Mathf.PI * 2f;
                _particles.Add(new Particle
                {
                    Rt = img.rectTransform, Img = img, Size = size,
                    Velocity = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * _cell * Random.Range(2.2f, 4.2f),
                    Life = Random.Range(0.35f, 0.55f),
                });
            }
        }

        private void Update()
        {
            if (_particles.Count == 0) return;
            float dt = Time.unscaledDeltaTime;
            for (int i = _particles.Count - 1; i >= 0; i--)
            {
                var p = _particles[i];
                p.Age += dt;
                if (p.Age >= p.Life || p.Rt == null)
                {
                    if (p.Img != null) { p.Img.gameObject.SetActive(false); _particlePool.Push(p.Img); }
                    _particles.RemoveAt(i);
                    continue;
                }
                p.Velocity *= 1f - 4f * dt;
                p.Rt.anchoredPosition += p.Velocity * dt;
                float k = 1f - p.Age / p.Life;
                p.Rt.sizeDelta = Vector2.one * p.Size * k;
                _particles[i] = p;
            }
        }

        private void ClearParticles()
        {
            _particles.Clear();
            _particlePool.Clear(); // pooled images die with the old board root
        }

        // ------------------------------------------------------------------ helpers

        private void Place(RectTransform rt, int pos, float scale)
        {
            rt.sizeDelta = Vector2.one * _cell * scale;
            rt.anchoredPosition = CellCenter(pos);
        }

        private void Slice(Image img, float radius)
        {
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = RoundedPx / Mathf.Max(1f, radius);
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        private static Color Fade(Color c, float a) => new Color(c.r, c.g, c.b, a);

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
