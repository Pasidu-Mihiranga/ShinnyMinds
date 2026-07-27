using System;
using System.Collections;
using TMPro;
using UnityEngine;

namespace ShinyMinds.Missions.UI
{
    /// <summary>
    /// Reveals a line character by character. Uses TMP's maxVisibleCharacters rather
    /// than rebuilding the string, so the layout never reflows mid-reveal.
    /// </summary>
    public class TypewriterText : MonoBehaviour
    {
        [SerializeField] TMP_Text target;

        void Reset() => target = GetComponent<TMP_Text>();

        public void ShowInstant(string text)
        {
            if (target == null) return;

            target.text = text;
            target.maxVisibleCharacters = int.MaxValue;
        }

        /// <param name="skipRequested">
        /// Polled each frame. Returning true completes the line immediately — the first
        /// E press finishes the text rather than advancing to the next node.
        /// </param>
        public IEnumerator Play(string text, float charsPerSecond, Func<bool> skipRequested)
        {
            if (target == null)
                yield break;

            target.text = text;
            target.ForceMeshUpdate();

            int total = target.textInfo.characterCount;

            if (charsPerSecond <= 0f || total == 0)
            {
                target.maxVisibleCharacters = int.MaxValue;
                yield break;
            }

            target.maxVisibleCharacters = 0;

            float revealed = 0f;
            while (revealed < total)
            {
                if (skipRequested != null && skipRequested())
                    break;

                revealed += charsPerSecond * Time.deltaTime;
                target.maxVisibleCharacters = Mathf.Min(total, Mathf.FloorToInt(revealed));
                yield return null;
            }

            target.maxVisibleCharacters = int.MaxValue;
        }
    }
}
