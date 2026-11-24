using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace NovaForge.UI
{
    /// <summary>
    /// Thick smooth line through points (Catmull-Rom), optionally dashed. Used for level-map paths.
    /// Points are in this RectTransform's local space.
    /// </summary>
    public sealed class UIPolyline : MaskableGraphic
    {
        [SerializeField] private List<Vector2> points = new List<Vector2>();
        [SerializeField, Min(1f)] private float thickness = 20f;
        [SerializeField, Range(1, 24)] private int smoothing = 10;
        [Tooltip("0 = solid. Otherwise dash length; the gap has the same length.")]
        [SerializeField, Min(0f)] private float dash;
        [Tooltip("Width of the soft transparent edge (canvas units).")]
        [SerializeField, Min(0f)] private float feather = 2f;

        public float Thickness { get => thickness; set { thickness = value; SetVerticesDirty(); } }
        public float Dash { get => dash; set { dash = value; SetVerticesDirty(); } }

        public void SetPoints(IEnumerable<Vector2> pts)
        {
            points.Clear();
            points.AddRange(pts);
            SetVerticesDirty();
        }

        private static readonly List<Vector2> Path = new List<Vector2>();

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (points.Count < 2) return;
            Path.Clear();
            for (int i = 0; i < points.Count - 1; i++)
            {
                var p0 = points[Mathf.Max(0, i - 1)];
                var p1 = points[i];
                var p2 = points[i + 1];
                var p3 = points[Mathf.Min(points.Count - 1, i + 2)];
                for (int s = 0; s < smoothing; s++) Path.Add(CatmullRom(p0, p1, p2, p3, s / (float)smoothing));
            }
            Path.Add(points[points.Count - 1]);

            // Which segments are drawn (dashes), then emit each run of drawn segments as one mitred strip
            // so joints have no gaps or overlaps.
            Drawn.Clear();
            float travelled = 0f;
            for (int i = 0; i < Path.Count - 1; i++)
            {
                float len = Vector2.Distance(Path[i], Path[i + 1]);
                bool on = dash <= 0f || Mathf.FloorToInt(travelled / dash) % 2 == 0;
                travelled += len;
                Drawn.Add(on && len > 0.001f);
            }
            for (int i = 0; i < Drawn.Count;)
            {
                if (!Drawn[i]) { i++; continue; }
                int j = i;
                while (j + 1 < Drawn.Count && Drawn[j + 1]) j++;
                Strip(vh, i, j + 1);
                i = j + 1;
            }
        }

        private static readonly List<bool> Drawn = new List<bool>();

        private void Strip(VertexHelper vh, int start, int end)
        {
            // 4 vertices per point: transparent fringe, solid core, solid core, transparent fringe (cheap anti-aliasing).
            float half = Mathf.Max(0.5f, thickness / 2f - feather / 2f);
            Color32 c = color;
            Color32 clear = c;
            clear.a = 0;
            int first = vh.currentVertCount;
            for (int k = start; k <= end; k++)
            {
                var next = k < end ? (Path[k + 1] - Path[k]).normalized : (Path[k] - Path[k - 1]).normalized;
                var prev = k > start ? (Path[k] - Path[k - 1]).normalized : next;
                var tangent = (prev + next).normalized;
                if (tangent.sqrMagnitude < 0.5f) tangent = next;
                var normal = new Vector2(-tangent.y, tangent.x);
                float scale = 1f / Mathf.Max(0.35f, Vector2.Dot(normal, new Vector2(-next.y, next.x)));
                var p = Path[k];
                vh.AddVert(p - normal * (half + feather) * scale, clear, Vector2.zero);
                vh.AddVert(p - normal * half * scale, c, Vector2.zero);
                vh.AddVert(p + normal * half * scale, c, Vector2.zero);
                vh.AddVert(p + normal * (half + feather) * scale, clear, Vector2.zero);
                if (k == start) continue;
                int a = first + (k - start - 1) * 4, b = a + 4;
                for (int q = 0; q < 3; q++)
                {
                    vh.AddTriangle(a + q, a + q + 1, b + q + 1);
                    vh.AddTriangle(a + q, b + q + 1, b + q);
                }
            }
        }

        private static Vector2 CatmullRom(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
        {
            float t2 = t * t, t3 = t2 * t;
            return 0.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }
    }
}
