// STATUS: compiles in Unity 6000.3.29f1 (verified 2026-11-23). Runtime behaviour not yet covered by PlayMode tests.
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using NovaForge.Core.Data;
using NovaForge.Core.Foundation;
using NovaForge.Core.Purchasing;
using UnityEngine;
using UnityEngine.Networking;

namespace NovaForge.Unity
{
    /// <summary>Fits a RectTransform into Screen.safeArea (notches, rounded corners, home indicator).</summary>
    [RequireComponent(typeof(RectTransform))]
    [ExecuteAlways]
    public sealed class SafeAreaFitter : MonoBehaviour
    {
        private RectTransform _rect;
        private Rect _lastSafeArea;
        private Vector2Int _lastScreen;

        private void OnEnable()
        {
            _rect = (RectTransform)transform;
            Apply();
        }

        private void Update()
        {
            if (Screen.safeArea != _lastSafeArea || Screen.width != _lastScreen.x || Screen.height != _lastScreen.y) Apply();
        }

        private void Apply()
        {
            var safe = Screen.safeArea;
            _lastSafeArea = safe;
            _lastScreen = new Vector2Int(Screen.width, Screen.height);
            if (Screen.width <= 0 || Screen.height <= 0) return;
            var min = safe.position;
            var max = safe.position + safe.size;
            min.x /= Screen.width; min.y /= Screen.height;
            max.x /= Screen.width; max.y /= Screen.height;
            _rect.anchorMin = min;
            _rect.anchorMax = max;
            _rect.offsetMin = Vector2.zero;
            _rect.offsetMax = Vector2.zero;
        }
    }
}
