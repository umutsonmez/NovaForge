using System;
using System.Collections.Generic;
using System.Text;
using NovaForge.Core;
using NovaForge.Core.Ads;
using NovaForge.Core.Economy;
using NovaForge.Core.Foundation;
using NovaForge.Core.Localization;
using NovaForge.Core.Meta;
using NovaForge.Core.Progression;
using NovaForge.Core.Shop;
using NovaForge.Core.UI;
using NovaForge.Unity;
using NovaForge.Unity.Audio;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NovaForge.UI
{
    // ====================================================================== daily reward

    /// <summary>One day cell of the daily reward calendar.</summary>
    public sealed class DailyRewardDayCell : MonoBehaviour
    {
        public TMP_Text dayLabel;
        public TMP_Text rewardLabel;
        [Tooltip("Shown for already claimed days.")]
        public GameObject claimedMark;
        [Tooltip("Shown for the day that the next claim grants.")]
        public GameObject currentHighlight;
    }
}
