using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NovaForge.Core.Ads;
using NovaForge.Core.Economy;
using NovaForge.Core.Foundation;
using NovaForge.Core.Localization;
using NovaForge.Core.UI;
using NovaForge.UI;
using NovaForge.Unity.Audio;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if TILTWELL_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace NovaForge.Tiltwell.Unity
{
    /// <summary>Swipe detection through the EventSystem (works with touch and mouse, both input backends).</summary>
    public sealed class SwipeInput : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [Tooltip("Minimum swipe distance in screen-height fractions.")]
        [SerializeField, Range(0.01f, 0.2f)] private float threshold = 0.04f;
        public event Action<Tilt> Swiped;
        private Vector2 _start;
        private bool _fired;

        public void OnBeginDrag(PointerEventData e) { _start = e.position; _fired = false; }

        public void OnDrag(PointerEventData e)
        {
            if (_fired) return;
            var d = e.position - _start;
            if (d.magnitude < threshold * Screen.height) return;
            _fired = true; // one tilt per swipe, fired as soon as the intent is clear (feels responsive)
            Swiped?.Invoke(Mathf.Abs(d.x) > Mathf.Abs(d.y) ? (d.x > 0 ? Tilt.Right : Tilt.Left) : (d.y > 0 ? Tilt.Up : Tilt.Down));
        }

        public void OnEndDrag(PointerEventData e) => _fired = false;
    }
}
