using System;
using ShinyMinds.Missions.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ShinyMinds.Missions.UI
{
    /// <summary>The result screen: badge, title, lesson, and Retry / Continue.</summary>
    public class MissionEndingCard : MonoBehaviour
    {
        [SerializeField] GameObject root;
        [SerializeField] Image badge;
        [SerializeField] TMP_Text title;
        [SerializeField] TMP_Text lesson;
        [SerializeField] Button retryButton;
        [SerializeField] Button continueButton;

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
            Hide();
        }

        void OnDestroy()
        {
            if (retryButton != null) retryButton.onClick.RemoveListener(Retry);
            if (continueButton != null) continueButton.onClick.RemoveListener(Continue);
        }

        public void Show(MissionEnding ending, Action retry, Action cont)
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
