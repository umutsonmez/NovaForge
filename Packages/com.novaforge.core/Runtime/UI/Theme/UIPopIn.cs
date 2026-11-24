using UnityEngine;

namespace NovaForge.UI
{
    /// <summary>Scales from 0 with an overshoot each time the object is enabled (cells, stars, rewards). Unscaled time.</summary>
    public sealed class UIPopIn : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float delay;
        [SerializeField, Min(0.05f)] private float duration = 0.35f;
        private Vector3 _base;
        private float _start;
        private bool _baseKnown;

        public float Delay { get => delay; set => delay = value; }

        private void OnEnable()
        {
            if (!_baseKnown) { _base = transform.localScale; _baseKnown = true; }
            _start = Time.unscaledTime + delay;
            transform.localScale = Vector3.zero;
        }

        private void OnDisable()
        {
            if (_baseKnown) transform.localScale = _base;
        }

        private void Update()
        {
            float t = (Time.unscaledTime - _start) / duration;
            if (t < 0f) return;
            if (t >= 1f) { transform.localScale = _base; enabled = false; return; }
            transform.localScale = _base * EaseOutBack(t);
        }

        /// <summary>Restart the animation (component re-enables itself).</summary>
        public void Play(float withDelay)
        {
            delay = withDelay;
            enabled = false;
            enabled = true;
        }

        public static float EaseOutBack(float x)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(x - 1f, 3) + c1 * Mathf.Pow(x - 1f, 2);
        }
    }
}
