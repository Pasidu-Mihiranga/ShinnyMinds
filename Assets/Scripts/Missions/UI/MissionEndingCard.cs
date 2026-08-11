using System;
using ShinyMinds.Core.Save;
using ShinyMinds.Missions.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ShinyMinds.Missions.UI
{
    /// <summary>
    /// The summary screen: badge, outcome title, the stars earned, the lesson, how the run
    /// compares to previous attempts, and Try Again / Continue.
    ///
    /// The progress half is read from SaveService rather than passed in, because MissionRunner
    /// has already called RecordEnding by the time this shows — so attempts includes this run
    /// and bestStars is up to date.
    /// </summary>
    public class MissionEndingCard : MonoBehaviour
    {
        [SerializeField] GameObject root;
        [SerializeField] Image badge;
        [SerializeField] TMP_Text title;
        [SerializeField] TMP_Text lesson;
        [SerializeField] Button retryButton;
        [SerializeField] Button continueButton;

        [Header("Score")]
        [Tooltip("One Image per star, left to right. Tinted, not swapped — see Show().")]
        [SerializeField] Image[] stars = new Image[0];
        [SerializeField] Color starEarned = new Color(0.98f, 0.74f, 0.16f);
        [SerializeField] Color starEmpty = new Color(0.85f, 0.86f, 0.89f);

        [Header("Progress")]
        [SerializeField] TMP_Text attemptText;
        [SerializeField] TMP_Text bestText;

        [Header("Title colours by quality")]
        [SerializeField] Color unsafeColor = new Color(0.90f, 0.25f, 0.30f);
        [SerializeField] Color safeColor = new Color(0.20f, 0.70f, 0.45f);
        [SerializeField] Color bestColor = new Color(0.25f, 0.60f, 0.95f);

        Action onRetry;
        Action onContinue;

        void Awake()
        {
            if (retryButton != null) retryButton.onClick.AddListener(Retry);
            if (continueButton != null) continueButton.onClick.AddListener(Continue);

            // NO Hide() HERE. This component sits on the card's root, and that root ships
            // INACTIVE — so Awake does not run at load. It runs the moment something activates
            // the object, and the only thing that ever does is the LAST LINE of Show(). Hiding
            // here therefore switched the summary off in the same frame it appeared and nulled
            // onRetry/onContinue with it, leaving MissionRunner.ShowEnding spinning on
            // `while (!done)`: no summary, no Try Again, and the player locked on a black screen.
            //
            // Nothing is needed in its place. The prefab stores this root inactive, and
            // MissionUIView.Awake() calls HideAll() at load, which hides the card from outside.
        }

        void OnDestroy()
        {
            if (retryButton != null) retryButton.onClick.RemoveListener(Retry);
            if (continueButton != null) continueButton.onClick.RemoveListener(Continue);
        }

        public void Show(MissionEnding ending, string missionId, Action retry, Action cont)
        {
            if (ending == null) return;

            onRetry = retry;
            onContinue = cont;

            if (title != null)
            {
                title.text = ending.title;
                title.color = ColorFor(ending.quality);
            }

            if (lesson != null)
                lesson.text = ending.lesson;

            ShowStars(ending.stars);
            ShowProgress(missionId);

            // A Sprite, not an emoji — TMP's default atlas has no emoji glyphs.
            if (badge != null)
            {
                badge.sprite = ending.badge;
                badge.enabled = ending.badge != null;
            }

            if (retryButton != null)
            {
                retryButton.gameObject.SetActive(ending.allowRetry);
                retryButton.interactable = true;
            }

            if (continueButton != null)
            {
                continueButton.gameObject.SetActive(ending.allowContinue);
                continueButton.interactable = true;
            }

            if (root != null) root.SetActive(true);
        }

        public void Hide()
        {
            onRetry = null;
            onContinue = null;
            if (root != null) root.SetActive(false);
        }

        /// <summary>
        /// Tints rather than swaps sprites, so the row is one generated star used three times and
        /// an empty star still holds its place in the layout.
        /// </summary>
        void ShowStars(int earned)
        {
            for (int i = 0; i < stars.Length; i++)
                if (stars[i] != null)
                    stars[i].color = i < earned ? starEarned : starEmpty;
        }

        void ShowProgress(string missionId)
        {
            if (attemptText == null && bestText == null)
                return;

            MissionProgress p = string.IsNullOrEmpty(missionId)
                ? null
                : SaveService.GetProgress(missionId);

            if (attemptText != null)
            {
                attemptText.text = p == null || p.attempts <= 0
                    ? string.Empty
                    : p.attempts == 1 ? "First attempt" : $"Attempt {p.attempts}";
            }

            if (bestText == null)
                return;

            int total = Mathf.Max(1, stars.Length);

            bestText.text = p == null ? string.Empty
                : p.completed ? $"Mission complete — best {p.bestStars} of {total}"
                : $"Best so far {p.bestStars} of {total}";
        }

        void Retry()
        {
            Action a = onRetry;
            onRetry = null;
            onContinue = null;
            SetInteractable(false);
            a?.Invoke();
        }

        void Continue()
        {
            Action a = onContinue;
            onRetry = null;
            onContinue = null;
            SetInteractable(false);
            a?.Invoke();
        }

        void SetInteractable(bool value)
        {
            if (retryButton != null) retryButton.interactable = value;
            if (continueButton != null) continueButton.interactable = value;
        }

        Color ColorFor(EndingQuality quality)
        {
            switch (quality)
            {
                case EndingQuality.Unsafe: return unsafeColor;
                case EndingQuality.Best: return bestColor;
                default: return safeColor;
            }
        }
    }
}
