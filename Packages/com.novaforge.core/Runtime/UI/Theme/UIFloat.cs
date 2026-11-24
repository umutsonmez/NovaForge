using UnityEngine;

namespace NovaForge.UI
{
    /// <summary>Slow vertical bobbing (decorations, map marker). Phase is randomised so groups don't move in lockstep.</summary>
    public sealed class UIFloat : MonoBehaviour
    {
        [SerializeField] private float distance = 14f;
        [SerializeField, Min(0.1f)] private float period = 2.6f;
        [SerializeField] private float rotation;
        private RectTransform _rt;
        private Vector2 _base;
        private float _phase;

        private void Awake()
        {
            _rt = (RectTransform)transform;
            _base = _rt.anchoredPosition;
            _phase = Random.value * Mathf.PI * 2f;
        }

        private void OnDisable()
        {
            if (_rt != null) _rt.anchoredPosition = _base;
        }

        private void Update()
        {
            float s = Mathf.Sin(_phase + Time.unscaledTime * Mathf.PI * 2f / period);
            _rt.anchoredPosition = _base + new Vector2(0f, s * distance);
            if (rotation != 0f) _rt.localRotation = Quaternion.Euler(0, 0, s * rotation);
        }
    }
}
