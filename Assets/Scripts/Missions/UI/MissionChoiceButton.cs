using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ShinyMinds.Missions.UI
{
    /// <summary>One clickable option in the decision panel.</summary>
    [RequireComponent(typeof(Button))]
    public class MissionChoiceButton : MonoBehaviour
    {
        [SerializeField] Button button;
        [SerializeField] TMP_Text label;
        [Tooltip("Optional A / B / C prefix shown to the left of the label.")]
        [SerializeField] TMP_Text letter;

        int index = -1;
        Action<int> onPicked;

        void Reset()
        {
            button = GetComponent<Button>();
            label = GetComponentInChildren<TMP_Text>();
        }

        void Awake()
        {
            if (button == null) button = GetComponent<Button>();
            button.onClick.AddListener(Pick);
        }

        void OnDestroy()
        {
            if (button != null) button.onClick.RemoveListener(Pick);
        }

        public void Bind(int choiceIndex, string text, Action<int> callback)
        {
            index = choiceIndex;
            onPicked = callback;

            if (label != null) label.text = text;
            if (letter != null) letter.text = ((char)('A' + choiceIndex)).ToString();

            if (button != null) button.interactable = true;
            gameObject.SetActive(true);
        }

        public void Pick()
        {
            if (index < 0 || onPicked == null)
                return;

            // Latch so a double-click cannot resolve two branches.
            Action<int> callback = onPicked;
            onPicked = null;

            if (button != null) button.interactable = false;

            callback(index);
        }
    }
}
