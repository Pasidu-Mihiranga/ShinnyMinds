using ShinyMinds.Missions.Data;
using TMPro;
using UnityEngine;

namespace ShinyMinds.Missions.UI
{
    /// <summary>
    /// One speech balloon inside the memory bubble, sitting over the stand-in who is
    /// speaking. There are two, one per <see cref="MemorySide"/>.
    ///
    /// A finished balloon is dimmed rather than hidden, so once the second line lands
    /// the pair reads as a conversation Aisha is replaying rather than two captions
    /// that happened to share a frame.
    /// </summary>
    public class MemoryBubble : MonoBehaviour
    {
        [SerializeField] GameObject root;
        [SerializeField] CanvasGroup group;
        [Tooltip("Optional, and left unassigned by the builder: the balloon's tail already " +
                 "says who is speaking, and the line reads better with the whole balloon.")]
        [SerializeField] TMP_Text nameLabel;
        [SerializeField] TMP_Text lineText;
        [SerializeField] TypewriterText typewriter;

        [Tooltip("Alpha of a balloon whose line has already been read.")]
        [SerializeField] float dimmedAlpha = 0.45f;

        public TypewriterText Typewriter => typewriter;

        public void Show(SpeakerProfile speaker, string text)
        {
            if (root != null) root.SetActive(true);
            if (group != null) group.alpha = 1f;

            if (nameLabel != null)
            {
                string name = speaker != null ? speaker.displayName : string.Empty;
                nameLabel.text = name;
                nameLabel.gameObject.SetActive(!string.IsNullOrEmpty(name));
                if (speaker != null) nameLabel.color = speaker.nameColor;
            }

            if (lineText != null) lineText.text = text;
        }

        /// <summary>Keeps the line on screen, faded back, while the other side speaks.</summary>
        public void Dim()
        {
            if (root == null || !root.activeSelf) return;
            if (group != null) group.alpha = dimmedAlpha;
        }

        public void Hide()
        {
            if (root != null) root.SetActive(false);
        }
    }
}
