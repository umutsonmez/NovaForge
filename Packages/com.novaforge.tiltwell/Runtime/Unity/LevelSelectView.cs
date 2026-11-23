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
    public static class TiltwellViewIds
    {
        public const string Levels = "levels";
    }

    /// <summary>Level select grid (view id "levels"). Cells are cloned from a template button.</summary>
    public sealed class LevelSelectView : UIView
    {
        [SerializeField] private Button cellTemplate;
        [SerializeField] private Button backButton;
        private readonly List<GameObject> _cells = new List<GameObject>();

        protected override void Awake()
        {
            base.Awake();
            cellTemplate.gameObject.SetActive(false);
            backButton.onClick.AddListener(Close);
        }

        protected override void OnShowing(object args)
        {
            foreach (var c in _cells) Destroy(c);
            _cells.Clear();
            var progression = Core.Progression;
            for (int i = 0; i < progression.Catalog.Count; i++)
            {
                var id = progression.Catalog.GetLevelId(i);
                var cell = Instantiate(cellTemplate, cellTemplate.transform.parent, false);
                cell.gameObject.SetActive(true);
                _cells.Add(cell.gameObject);
                bool unlocked = progression.IsUnlocked(i);
                int stars = progression.Get(id)?.BestStars ?? 0;
                var label = cell.GetComponentInChildren<TMP_Text>();
                label.text = unlocked ? (i + 1).ToString() : $"<alpha=#55>{i + 1}";
                // Star images named Star1..Star3 (fonts may lack the ★ glyph, so no text stars).
                for (int s = 1; s <= 3; s++)
                {
                    var star = cell.transform.Find("Stars/Star" + s);
                    if (star != null) star.gameObject.SetActive(unlocked && s <= stars);
                }
                cell.interactable = unlocked;
                cell.onClick.AddListener(() => Navigation.ResetToAsync(ViewIds.Gameplay, id).Forget());
            }
        }
    }
}
