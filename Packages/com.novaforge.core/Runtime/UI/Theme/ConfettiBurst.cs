using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace NovaForge.UI
{
    /// <summary>
    /// Lightweight uGUI confetti: pooled Images thrown up from this RectTransform's center, falling with gravity.
    /// No particle system needed, so it renders inside the canvas (correct sorting with popups).
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class ConfettiBurst : MonoBehaviour
    {
        [SerializeField] private Sprite sprite;
        [SerializeField] private Color[] colors =
        {
            new Color32(0xFF, 0x5F, 0x6D, 0xFF), new Color32(0xFF, 0xC3, 0x47, 0xFF), new Color32(0x4F, 0x8C, 0xFF, 0xFF),
            new Color32(0x3F, 0xC9, 0x7A, 0xFF), new Color32(0xA0, 0x6B, 0xFF, 0xFF),
        };
        [SerializeField, Range(4, 200)] private int count = 70;
        [SerializeField] private float speed = 1500f;
        [SerializeField] private float gravity = 2600f;
        [SerializeField, Min(0.2f)] private float lifetime = 2.2f;

        private struct Piece
        {
            public RectTransform Rt;
            public Image Img;
            public Vector2 Velocity;
            public float Spin, Age, Flutter;
        }

        private readonly List<Piece> _pieces = new List<Piece>();
        private readonly Stack<Image> _pool = new Stack<Image>();

        public void Burst()
        {
            for (int i = 0; i < count; i++)
            {
                var img = _pool.Count > 0 ? _pool.Pop() : Create();
                img.gameObject.SetActive(true);
                img.color = colors[Random.Range(0, colors.Length)];
                var rt = img.rectTransform;
                rt.anchoredPosition = Vector2.zero;
                rt.sizeDelta = new Vector2(Random.Range(14f, 24f), Random.Range(24f, 40f));
                rt.localRotation = Quaternion.Euler(0, 0, Random.Range(0f, 360f));
                float angle = Random.Range(55f, 125f) * Mathf.Deg2Rad;
                _pieces.Add(new Piece
                {
                    Rt = rt, Img = img,
                    Velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed * Random.Range(0.5f, 1.1f),
                    Spin = Random.Range(-540f, 540f),
                    Flutter = Random.Range(0f, 10f),
                });
            }
            enabled = true;
        }

        private Image Create()
        {
            var go = new GameObject("Confetti", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(transform, false);
            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.raycastTarget = false;
            return img;
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            for (int i = _pieces.Count - 1; i >= 0; i--)
            {
                var p = _pieces[i];
                p.Age += dt;
                p.Velocity.y -= gravity * dt;
                p.Velocity.x *= 1f - 0.9f * dt;
                if (p.Velocity.y < -500f) p.Velocity.y = -500f; // paper drag
                p.Rt.anchoredPosition += (p.Velocity + new Vector2(Mathf.Sin(p.Age * 6f + p.Flutter) * 120f, 0f)) * dt;
                p.Rt.localRotation = Quaternion.Euler(0, 0, p.Rt.localEulerAngles.z + p.Spin * dt);
                float fade = Mathf.Clamp01((lifetime - p.Age) / 0.4f);
                var c = p.Img.color;
                c.a = fade;
                p.Img.color = c;
                if (p.Age >= lifetime)
                {
                    p.Img.gameObject.SetActive(false);
                    _pool.Push(p.Img);
                    _pieces.RemoveAt(i);
                    continue;
                }
                _pieces[i] = p;
            }
            if (_pieces.Count == 0) enabled = false;
        }

        private void OnDisable()
        {
            foreach (var p in _pieces)
            {
                if (p.Img == null) continue;
                p.Img.gameObject.SetActive(false);
                _pool.Push(p.Img);
            }
            _pieces.Clear();
        }
    }
}
