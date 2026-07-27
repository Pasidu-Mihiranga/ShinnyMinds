using System;
using System.Collections;
using System.Collections.Generic;
using ShinyMinds.Missions.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ShinyMinds.Missions.UI
{
    /// <summary>
    /// The one shared mission UI. Pure view — it never reads or mutates MissionData,
    /// it only renders what the runner hands it.
    ///
    /// Lives on a Screen Space Overlay canvas with Sort Order 100 so it always draws
    /// above the two per-NPC Groq canvases (which sit at 0).
    /// </summary>
    public class MissionUIView : MonoBehaviour, IMissionUi
    {
        [Header("Dialogue")]
        [SerializeField] GameObject dialoguePanel;
        [SerializeField] TMP_Text speakerLabel;
        [SerializeField] TMP_Text lineText;
        [SerializeField] TypewriterText lineTypewriter;
        [SerializeField] TMP_Text continuePrompt;

        [Header("Thought bubble")]
        [SerializeField] GameObject thoughtPanel;
        [SerializeField] TMP_Text thoughtText;
        [SerializeField] TypewriterText thoughtTypewriter;

        [Header("Choices")]
        [SerializeField] GameObject choicePanel;
        [SerializeField] TMP_Text choicePrompt;
        [SerializeField] List<MissionChoiceButton> choiceButtons = new List<MissionChoiceButton>();

        [Header("Ending")]
        [SerializeField] MissionEndingCard endingCard;

        [Header("Objective")]
        [SerializeField] ObjectiveHud objectiveHud;

        [Header("Screen effects")]
        [SerializeField] Image fadeOverlay;
        [SerializeField] RectTransform letterboxTop;
        [SerializeField] RectTransform letterboxBottom;
        [SerializeField] float letterboxHeight = 110f;

        bool thoughtActive;
        Coroutine letterboxRoutine;

        void Awake() => HideAll();

        // ------------------------------------------------------------------ lines

        public void ShowLine(bool isThought, SpeakerProfile speaker, string text)
        {
            thoughtActive = isThought;

            if (isThought)
            {
                SetActive(dialoguePanel, false);
                SetActive(thoughtPanel, true);
                if (thoughtText != null) thoughtText.text = text;
            }
            else
            {
                SetActive(thoughtPanel, false);
                SetActive(dialoguePanel, true);

                if (speakerLabel != null)
                {
                    string name = speaker != null ? speaker.displayName : string.Empty;
                    speakerLabel.text = name;
                    speakerLabel.gameObject.SetActive(!string.IsNullOrEmpty(name));
                    if (speaker != null) speakerLabel.color = speaker.nameColor;
                }

                if (lineText != null) lineText.text = text;
            }

            ShowContinuePrompt(string.Empty);
        }

        public IEnumerator PlayTypewriter(string text, float charsPerSecond, Func<bool> skipRequested)
        {
            TypewriterText writer = thoughtActive ? thoughtTypewriter : lineTypewriter;

            if (writer == null)
                yield break;

            yield return writer.Play(text, charsPerSecond, skipRequested);
        }

        public void ShowContinuePrompt(string text)
        {
            if (continuePrompt == null) return;

            continuePrompt.text = text;
            continuePrompt.gameObject.SetActive(!string.IsNullOrEmpty(text));
        }

        public void HideLine()
        {
            SetActive(dialoguePanel, false);
            SetActive(thoughtPanel, false);
            thoughtActive = false;
        }

        // ---------------------------------------------------------------- choices

        public void ShowChoices(string prompt, IList<MissionChoice> choices, Action<int> onPicked)
        {
            if (choicePrompt != null) choicePrompt.text = prompt;

            int count = choices != null ? choices.Count : 0;

            for (int i = 0; i < choiceButtons.Count; i++)
            {
                MissionChoiceButton button = choiceButtons[i];
                if (button == null) continue;

                if (i < count)
                    button.Bind(i, choices[i].label, onPicked);
                else
                    button.gameObject.SetActive(false);
            }

            if (count > choiceButtons.Count)
                Debug.LogWarning($"MissionUIView: {count} choices but only {choiceButtons.Count} buttons in the prefab.", this);

            SetActive(choicePanel, true);
        }

        public void HideChoices() => SetActive(choicePanel, false);

        // ---------------------------------------------------------------- endings

        public void ShowEnding(MissionEnding ending, Action onRetry, Action onContinue)
        {
            HideLine();
            HideChoices();
            endingCard?.Show(ending, onRetry, onContinue);
        }

        public void HideEnding() => endingCard?.Hide();

        // ------------------------------------------------------------- IMissionUi

        public void SetObjective(string text) => objectiveHud?.Set(text);

        public IEnumerator Fade(bool toBlack, float seconds, float holdSeconds = 0f)
        {
            if (fadeOverlay == null)
                yield break;

            fadeOverlay.gameObject.SetActive(true);

            float from = fadeOverlay.color.a;
            float to = toBlack ? 1f : 0f;

            if (seconds > 0f)
            {
                float t = 0f;
                while (t < 1f)
                {
                    t += Time.deltaTime / seconds;
                    SetFadeAlpha(Mathf.Lerp(from, to, Mathf.Clamp01(t)));
                    yield return null;
                }
            }

            SetFadeAlpha(to);

            if (holdSeconds > 0f)
                yield return new WaitForSeconds(holdSeconds);

            // Leave the object active while black so it keeps covering the screen.
            if (!toBlack)
                fadeOverlay.gameObject.SetActive(false);
        }

        public IEnumerator SetLetterbox(bool on, float seconds)
        {
            if (letterboxTop == null || letterboxBottom == null)
                yield break;

            if (letterboxRoutine != null)
            {
                StopCoroutine(letterboxRoutine);
                letterboxRoutine = null;
            }

            float from = letterboxTop.sizeDelta.y;
            float to = on ? letterboxHeight : 0f;

            if (Mathf.Approximately(from, to))
                yield break;

            letterboxTop.gameObject.SetActive(true);
            letterboxBottom.gameObject.SetActive(true);

            if (seconds > 0f)
            {
                float t = 0f;
                while (t < 1f)
                {
                    t += Time.deltaTime / seconds;
                    SetBarHeight(Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t))));
                    yield return null;
                }
            }

            SetBarHeight(to);

            if (!on)
            {
                letterboxTop.gameObject.SetActive(false);
                letterboxBottom.gameObject.SetActive(false);
            }
        }

        // ----------------------------------------------------------------- teardown

        public void HideAll()
        {
            HideLine();
            HideChoices();
            HideEnding();
            ShowContinuePrompt(string.Empty);
            SetObjective(string.Empty);

            SetBarHeight(0f);
            if (letterboxTop != null) letterboxTop.gameObject.SetActive(false);
            if (letterboxBottom != null) letterboxBottom.gameObject.SetActive(false);

            if (fadeOverlay != null)
            {
                SetFadeAlpha(0f);
                fadeOverlay.gameObject.SetActive(false);
            }
        }

        // ------------------------------------------------------------------ helpers

        void SetFadeAlpha(float a)
        {
            Color c = fadeOverlay.color;
            c.a = a;
            fadeOverlay.color = c;
        }

        void SetBarHeight(float height)
        {
            if (letterboxTop != null)
                letterboxTop.sizeDelta = new Vector2(letterboxTop.sizeDelta.x, height);

            if (letterboxBottom != null)
                letterboxBottom.sizeDelta = new Vector2(letterboxBottom.sizeDelta.x, height);
        }

        static void SetActive(GameObject go, bool value)
        {
            if (go != null && go.activeSelf != value)
                go.SetActive(value);
        }
    }
}
