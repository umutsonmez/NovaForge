using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace NovaForge.UI
{
    /// <summary>Vertical two-color gradient for uGUI Images (multiplies the vertex color, so the Image color still tints).</summary>
    [RequireComponent(typeof(Graphic))]
    public sealed class UIGradient : BaseMeshEffect
    {
        [SerializeField] private Color top = Color.white;
        [SerializeField] private Color bottom = Color.white;

        public Color Top { get => top; set { top = value; Refresh(); } }
        public Color Bottom { get => bottom; set { bottom = value; Refresh(); } }

        public void Set(Color topColor, Color bottomColor)
        {
            top = topColor;
            bottom = bottomColor;
            Refresh();
        }

        private void Refresh()
        {
            if (graphic != null) graphic.SetVerticesDirty();
        }

        private static readonly List<UIVertex> Vertices = new List<UIVertex>();

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || vh.currentVertCount == 0) return;
            Vertices.Clear();
            vh.GetUIVertexStream(Vertices);
            float min = float.MaxValue, max = float.MinValue;
            foreach (var v in Vertices)
            {
                if (v.position.y < min) min = v.position.y;
                if (v.position.y > max) max = v.position.y;
            }
            float range = Mathf.Max(0.0001f, max - min);
            for (int i = 0; i < Vertices.Count; i++)
            {
                var v = Vertices[i];
                var g = Color.Lerp(bottom, top, (v.position.y - min) / range);
                v.color = (Color32)((Color)v.color * g);
                Vertices[i] = v;
            }
            vh.Clear();
            vh.AddUIVertexTriangleStream(Vertices);
        }
    }
}
