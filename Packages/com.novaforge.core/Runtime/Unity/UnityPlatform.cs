// STATUS: written against stable UnityEngine APIs but NOT YET COMPILED in a Unity Editor
// (no Unity installation was available when this was authored). Verify on first Unity import.
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
    /// <summary>Routes Core logs to the Unity console (and therefore to device logcat / Xcode console).</summary>
    public sealed class UnityLogSink : ILogSink
    {
        public void Write(in LogEntry e)
        {
            var text = $"[{e.Tag}] {e.Message}";
            switch (e.Level)
            {
                case Core.Foundation.LogLevel.Error:
                    if (e.Exception != null) { Debug.LogError(text); Debug.LogException(e.Exception); }
                    else Debug.LogError(text);
                    break;
                case Core.Foundation.LogLevel.Warning: Debug.LogWarning(text); break;
                default: Debug.Log(text); break;
            }
        }
    }

    public static class UnityStorage
    {
        /// <summary>Per-game save directory: {persistentDataPath}/novaforge/{gameId}. Keeps games strictly separated.</summary>
        public static FileSaveStorage ForGame(string gameId)
            => new FileSaveStorage(Path.Combine(Application.persistentDataPath, "novaforge", gameId));
    }

    /// <summary>IHttpTransport over UnityWebRequest. Must be called from the main thread.</summary>
    public sealed class UnityHttpTransport : IHttpTransport
    {
        public Task<HttpResponse> PostJsonAsync(string url, string jsonBody, TimeSpan timeout)
        {
            var tcs = new TaskCompletionSource<HttpResponse>();
            var request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST)
            {
                uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(jsonBody ?? string.Empty)),
                downloadHandler = new DownloadHandlerBuffer(),
                timeout = Math.Max(1, (int)Math.Ceiling(timeout.TotalSeconds)),
            };
            request.SetRequestHeader("Content-Type", "application/json");
            var op = request.SendWebRequest();
            op.completed += _ =>
            {
                try
                {
                    bool networkError = request.result == UnityWebRequest.Result.ConnectionError;
                    tcs.TrySetResult(new HttpResponse
                    {
                        NetworkError = networkError,
                        StatusCode = (int)request.responseCode,
                        Body = request.downloadHandler != null ? request.downloadHandler.text : null,
                    });
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
                finally
                {
                    request.Dispose();
                }
            };
            return tcs.Task;
        }
    }

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
