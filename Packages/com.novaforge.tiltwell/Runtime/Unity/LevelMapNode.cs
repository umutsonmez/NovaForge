using NovaForge.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NovaForge.Tiltwell.Unity
{
    public enum LevelNodeState
    {
        Locked,
        Current,
        Completed,
    }

    /// <summary>One stop on the level map: a round raised button with number, stars, lock and "you are here" marker.</summary>
    [RequireComponent(typeof(Button))]
    public sealed class LevelMapNode : MonoBehaviour
    {
        [SerializeField] private TMP_Text label;
        [SerializeField] private Image edge;
        [SerializeField] private UIGradient face;
        [SerializeField] private GameObject lockIcon;
        [SerializeField] private Image[] stars = new Image[0];
        [SerializeField] private GameObject currentRing;
        [SerializeField] private GameObject marker;
        [SerializeField] private Color lockedTop = new Color32(0xE4, 0xE8, 0xF1, 0xFF);
        [SerializeField] private Color lockedBottom = new Color32(0xC9, 0xCF, 0xDD, 0xFF);
        [SerializeField] private Color lockedEdge = new Color32(0xA9, 0xB1, 0xC4, 0xFF);
        [SerializeField] private Color starOn = new Color32(0xFF, 0xC8, 0x3A, 0xFF);
        [SerializeField] private Color starOff = new Color(1f, 1f, 1f, 0.75f);

        public Button Button => GetComponent<Button>();

        public void Set(int number, LevelNodeState state, int starCount, Color top, Color bottom, Color edgeColor)
        {
            bool locked = state == LevelNodeState.Locked;
            label.text = number.ToString();
            label.gameObject.SetActive(!locked);
            if (lockIcon != null) lockIcon.SetActive(locked);
            face.Set(locked ? lockedTop : top, locked ? lockedBottom : bottom);
            edge.color = locked ? lockedEdge : edgeColor;
            for (int i = 0; i < stars.Length; i++)
            {
                if (stars[i] == null) continue;
                stars[i].gameObject.SetActive(state == LevelNodeState.Completed);
                stars[i].color = i < starCount ? starOn : starOff;
            }
            if (currentRing != null) currentRing.SetActive(state == LevelNodeState.Current);
            if (marker != null) marker.SetActive(state == LevelNodeState.Current);
            Button.interactable = !locked;
        }
    }
}
