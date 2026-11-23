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
    // ====================================================================== shop

    /// <summary>One shop row/card. Bound by ShopView; layout is up to the game.</summary>
    public sealed class ShopItemView : MonoBehaviour
    {
        public TMP_Text titleLabel;
        public TMP_Text contentLabel;
        public TMP_Text priceLabel;
        public Button buyButton;
        [Tooltip("Shown instead of the buy button once the purchase limit is reached.")]
        public GameObject ownedMark;
    }
}
