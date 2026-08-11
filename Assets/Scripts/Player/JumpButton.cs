using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Fires a jump on touch-down.
///
/// Deliberately not a UnityEngine.UI.Button: Button.onClick fires on pointer UP and only
/// if the finger is still inside the rect, which reads as a dropped input on a platformer
/// control. Jump has to answer the moment the finger lands.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class JumpButton : MonoBehaviour, IPointerDownHandler
{
    public void OnPointerDown(PointerEventData eventData)
    {
        if (MobileInput.Instance != null)
            MobileInput.Instance.QueueJump();
    }
}
