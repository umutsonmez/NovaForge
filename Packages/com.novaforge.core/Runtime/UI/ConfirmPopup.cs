using System;
using System.Collections.Generic;
using NovaForge.Core.Config;
using NovaForge.Core.Foundation;
using NovaForge.Core.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NovaForge.UI
{
    /// <summary>Arguments for the standard confirm popup. Text fields are localization keys.</summary>
    public sealed class ConfirmRequest
    {
        public string TitleKey;
        public string MessageKey;
        public object[] MessageArgs;
        public string ConfirmKey = "common.ok";
        /// <summary>null = single-button (info) popup.</summary>
        public string CancelKey = "common.cancel";
        internal Action<bool> Completed;
    }

    /// <summary>Standard yes/no popup (view id "confirm"). Use <see cref="UIManager.ConfirmAsync"/>.</summary>
    public sealed class ConfirmPopup : UIView
    {
        [SerializeField] private TMP_Text title;
        [SerializeField] private TMP_Text message;
        [SerializeField] private Button confirmButton;
        [SerializeField] private TMP_Text confirmLabel;
        [SerializeField] private Button cancelButton;
        [SerializeField] private TMP_Text cancelLabel;

        private ConfirmRequest _request;

        protected override void Awake()
        {
            base.Awake();
            confirmButton.onClick.AddListener(() => Answer(true));
            if (cancelButton != null) cancelButton.onClick.AddListener(() => Answer(false));
        }

        protected override void OnShowing(object args)
        {
            _request = args as ConfirmRequest ?? new ConfirmRequest();
            var loc = Core?.Localization;
            string T(string key) => string.IsNullOrEmpty(key) ? "" : loc != null ? loc.Get(key) : key;
            if (title != null) title.text = T(_request.TitleKey);
            if (message != null)
                message.text = _request.MessageArgs != null && loc != null ? loc.Format(_request.MessageKey, _request.MessageArgs) : T(_request.MessageKey);
            if (confirmLabel != null) confirmLabel.text = T(_request.ConfirmKey);
            if (cancelButton != null) cancelButton.gameObject.SetActive(_request.CancelKey != null);
            if (cancelLabel != null) cancelLabel.text = T(_request.CancelKey);
        }

        // Closed by back button or by navigation (screen change): counts as "no".
        protected override void OnHiding() => Complete(false);

        private void Answer(bool result)
        {
            Complete(result);
            Close();
        }

        private void Complete(bool result)
        {
            var r = _request;
            _request = null;
            r?.Completed?.Invoke(result);
        }
    }
}
