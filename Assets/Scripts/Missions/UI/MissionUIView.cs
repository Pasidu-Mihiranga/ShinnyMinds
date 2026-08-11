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
        [Tooltip("The subtitle bar. Only used for speakers with no body in the scene — " +
                 "the narrator — since everyone else gets a speech balloon over their head.")]
        [SerializeField] GameObject dialoguePanel;
        [SerializeField] TMP_Text speakerLabel;
        [SerializeField] TMP_Text lineText;
        [SerializeField] TypewriterText lineTypewriter;
        [SerializeField] TMP_Text continuePrompt;

        [Header("Speech balloon")]
        [SerializeField] WorldSpeechBubble worldBubble;
        [SerializeField] TMP_Text worldContinuePrompt;

        [Header("Thought bubble")]
        [SerializeField] GameObject thoughtPanel;
        [SerializeField] TMP_Text thoughtText;
        [SerializeField] TypewriterText thoughtTypewriter;

        [Header("Memory bubble")]
        [Tooltip("The whole remembered-scene overlay: dimmer, bubble, tail dots.")]
        [SerializeField] GameObject memoryPanel;
        [SerializeField] CanvasGroup memoryGroup;
        [Tooltip("Scaled up slightly as the memory opens, so it swells into view.")]
        [SerializeField] RectTransform memoryBubble;
        [Tooltip("Sizes the oval and keeps it, and its trail of dots, next to whoever is " +
                 "remembering or thinking.")]
        [SerializeField] MemoryBubbleAnchor memoryAnchor;
        [Tooltip("The masked oval showing the memory stage. Switched off for a thought, " +
                 "which has no stage behind it — only the paper and one balloon.")]
        [SerializeField] GameObject memoryStageView;
        [SerializeField] MemoryBubble memoryLeft;
        [SerializeField] MemoryBubble memoryRight;
        [Tooltip("One balloon centred in the bubble, for a thought: nobody is on stage, so " +
                 "there is no side for it to belong to.")]
        [SerializeField] MemoryBubble memoryThought;
        [Tooltip("The dialogue panel's own prompt is hidden with that panel, so the " +
                 "memory bubble carries a second one.")]
        [SerializeField] TMP_Text memoryContinuePrompt;
        [SerializeField] float memoryFadeSeconds = 0.45f;

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
        // 0 disables the cinematic bars: SetLetterbox then animates 0 -> 0 and early-outs,
        // so LetterboxAction and letterboxed shots become no-ops. Set to 110 to bring them back.
        [SerializeField] float letterboxHeight = 0f;

        /// <summary>Which panel the line currently on screen is being typed into.</summary>
        enum LineMode { Dialogue, Thought, Memory, World }

        LineMode mode = LineMode.Dialogue;
        MemoryBubble activeMemoryBubble;
        Coroutine letterboxRoutine;

        void Awake() => HideAll();

        // ------------------------------------------------------------------ lines

        public void ShowLine(bool isThought, SpeakerProfile speaker, string text)
            => ShowLine(isThought, speaker, text, null, null);

        /// <param name="speakerBody">
        /// The speaker's transform in the scene, if they have one. Given it and a camera,
        /// the line goes into a balloon over their head instead of the subtitle bar.
        /// </param>
        public void ShowLine(bool isThought, SpeakerProfile speaker, string text,
                             Transform speakerBody, Camera worldCamera)
        {
            bool overhead = !isThought && worldBubble != null
                            && speakerBody != null && worldCamera != null;

            mode = isThought ? LineMode.Thought
                 : overhead ? LineMode.World
                 : LineMode.Dialogue;

            if (overhead)
            {
                SetActive(dialoguePanel, false);
                SetActive(thoughtPanel, false);
                worldBubble.Show(text, speakerBody, worldCamera);
            }
            else if (isThought)
            {
                SetActive(dialoguePanel, false);
                SetActive(thoughtPanel, true);
                worldBubble?.Hide();
                if (thoughtText != null) thoughtText.text = text;
            }
            else
            {
                worldBubble?.Hide();
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
            TypewriterText writer;

            switch (mode)
            {
                case LineMode.Thought: writer = thoughtTypewriter; break;
                case LineMode.Memory: writer = activeMemoryBubble != null ? activeMemoryBubble.Typewriter : null; break;
                case LineMode.World: writer = worldBubble != null ? worldBubble.Typewriter : null; break;
                default: writer = lineTypewriter; break;
            }

            if (writer == null)
                yield break;

            yield return writer.Play(text, charsPerSecond, skipRequested);
        }

        /// <summary>
        /// Each panel carries its own prompt, so only the one belonging to whatever is on
        /// screen is set and the other two are cleared.
        /// </summary>
        public void ShowContinuePrompt(string text)
        {
            SetPrompt(continuePrompt, mode == LineMode.Dialogue || mode == LineMode.Thought ? text : string.Empty);
            SetPrompt(memoryContinuePrompt, mode == LineMode.Memory ? text : string.Empty);
            SetPrompt(worldContinuePrompt, mode == LineMode.World ? text : string.Empty);
        }

        static void SetPrompt(TMP_Text prompt, string text)
        {
            if (prompt == null) return;

            prompt.text = text;
            prompt.gameObject.SetActive(!string.IsNullOrEmpty(text));
        }

        public void HideLine()
        {
            // A remembered line stays up, faded, until the memory itself closes — the
            // two lines are one exchange, and blanking the balloon between them would
            // read as two unrelated captions.
            if (mode == LineMode.Memory)
            {
                activeMemoryBubble?.Dim();
                return;
            }

            SetActive(dialoguePanel, false);
            SetActive(thoughtPanel, false);
            worldBubble?.Hide();
            mode = LineMode.Dialogue;
        }

        // ----------------------------------------------------------------- memory

        public bool HasMemoryPanel => memoryPanel != null;

        /// <summary>True once the prefab carries the centred balloon a thought needs.</summary>
        public bool HasMemoryThought => memoryPanel != null && memoryThought != null;

        /// <summary>
        /// Swells the memory bubble into view over the dimmed world. The stage behind
        /// the render texture should already be switched on, or the bubble opens onto
        /// a blank frame.
        /// </summary>
        /// <param name="withStage">
        /// False for a thought, which has nobody to show. Decided here rather than when the
        /// line arrives, because the bubble has to be showing the right thing — and be the
        /// right size — before it swells, otherwise the opening frames flash the last
        /// memory's picture or resize under the player mid-fade.
        /// </param>
        /// <param name="owner">Whose memory or thought this is. The oval sits beside them.</param>
        public IEnumerator OpenMemory(bool withStage, Transform owner, Camera worldCamera)
        {
            if (memoryPanel == null)
                yield break;

            memoryLeft?.Hide();
            memoryRight?.Hide();
            memoryThought?.Hide();
            activeMemoryBubble = null;

            SetActive(memoryStageView, withStage);

            SetActive(memoryPanel, true);
            memoryAnchor?.Attach(owner, worldCamera, withStage);

            yield return FadeMemory(0f, 1f, 0.86f, 1f);
        }

        public IEnumerator CloseMemory()
        {
            if (memoryPanel == null || !memoryPanel.activeSelf)
                yield break;

            yield return FadeMemory(1f, 0f, 1f, 0.94f);

            HideMemoryImmediate();
        }

        /// <summary>
        /// Puts a remembered line in the balloon over the stand-in who is speaking, and
        /// fades the other balloon back if it is still holding the previous line.
        /// </summary>
        public void ShowMemoryLine(SpeakerProfile speaker, MemorySide side, string text)
        {
            // A prefab built before the memory box exists still has to play the line.
            if (memoryPanel == null)
            {
                ShowLine(false, speaker, text);
                return;
            }

            mode = LineMode.Memory;

            // A thought may have left the picture switched off in this same bubble.
            SetActive(memoryStageView, true);
            memoryThought?.Hide();

            MemoryBubble speaking = side == MemorySide.Right ? memoryRight : memoryLeft;
            MemoryBubble other = side == MemorySide.Right ? memoryLeft : memoryRight;

            other?.Dim();

            activeMemoryBubble = speaking;
            speaking?.Show(speaker, text);

            ShowContinuePrompt(string.Empty);
        }

        /// <summary>
        /// A thought, staged in the memory bubble: the same dimmed world, the same warm oval
        /// and trail of dots back to the girl, but empty of people. What Aisha is thinking is
        /// not a scene she can watch, so the picture is switched off and the line gets the
        /// paper to itself in one centred balloon.
        /// </summary>
        public void ShowMemoryThought(string text)
        {
            // A prefab built before the thought balloon exists still has to play the line.
            if (memoryPanel == null || memoryThought == null)
            {
                ShowLine(true, null, text);
                return;
            }

            mode = LineMode.Memory;

            SetActive(memoryStageView, false);
            memoryLeft?.Hide();
            memoryRight?.Hide();

            activeMemoryBubble = memoryThought;
            memoryThought.Show(null, text);

            ShowContinuePrompt(string.Empty);
        }

        void HideMemoryImmediate()
        {
            memoryAnchor?.Detach();
            memoryLeft?.Hide();
            memoryRight?.Hide();
            memoryThought?.Hide();
            activeMemoryBubble = null;

            // Back to the authored state, so the next memory does not inherit a thought's
            // empty bubble.
            SetActive(memoryStageView, true);

            if (memoryGroup != null) memoryGroup.alpha = 1f;
            if (memoryBubble != null) memoryBubble.localScale = Vector3.one;

            SetActive(memoryPanel, false);

            if (mode == LineMode.Memory) mode = LineMode.Dialogue;
        }

        IEnumerator FadeMemory(float fromAlpha, float toAlpha, float fromScale, float toScale)
        {
            if (memoryFadeSeconds > 0f)
            {
                float t = 0f;
                while (t < 1f)
                {
                    t += Time.deltaTime / memoryFadeSeconds;
                    float e = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));

                    if (memoryGroup != null) memoryGroup.alpha = Mathf.Lerp(fromAlpha, toAlpha, e);
                    if (memoryBubble != null)
                        memoryBubble.localScale = Vector3.one * Mathf.Lerp(fromScale, toScale, e);

                    yield return null;
                }
            }

            if (memoryGroup != null) memoryGroup.alpha = toAlpha;
            if (memoryBubble != null) memoryBubble.localScale = Vector3.one * toScale;
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
            HideMemoryImmediate();      // an ending card must never land on a dimmed memory
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
            HideMemoryImmediate();      // before HideLine, which leaves a memory line up
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
