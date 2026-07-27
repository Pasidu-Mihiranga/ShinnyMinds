using TMPro;
using UnityEngine;

namespace ShinyMinds.Missions.UI
{
    /// <summary>Small persistent "what am I doing" label, top-left.</summary>
    public class ObjectiveHud : MonoBehaviour
    {
        [SerializeField] GameObject root;
        [SerializeField] TMP_Text text;

        void Reset()
        {
            root = gameObject;
            text = GetComponentInChildren<TMP_Text>();
        }

        public void Set(string objective)
        {
            bool show = !string.IsNullOrWhiteSpace(objective);

            if (text != null && show)
                text.text = objective;

            if (root != null)
                root.SetActive(show);
        }
    }
}
