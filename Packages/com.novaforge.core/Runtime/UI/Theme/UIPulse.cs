using UnityEngine;

namespace NovaForge.UI
{
    /// <summary>Gentle looping scale "breath" to draw the eye (play button, current level). Unscaled time.</summary>
    public sealed class UIPulse : MonoBehaviour
    {
        [SerializeField, Range(0f, 0.3f)] private float amount = 0.05f;
        [SerializeField, Min(0.1f)] private float period = 1.4f;
        private Vector3 _base;

        private void Awake() => _base = transform.localScale;
        private void OnDisable() => transform.localScale = _base;

        private void Update()
        {
            float k = 1f + amount * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f / period));
            transform.localScale = _base * k;
        }
    }
}
