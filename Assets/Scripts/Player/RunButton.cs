using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Hold to run.
///
/// The release path needs both handlers. OnPointerUp covers the normal case (the
/// EventSystem sends it to the pressed object even if the finger has drifted off it),
/// and OnDisable covers the case where the controls are switched off mid-press by a
/// cutscene — without it the player sprints for the rest of the session.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class RunButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    public void OnPointerDown(PointerEventData eventData) => Set(true);

    public void OnPointerUp(PointerEventData eventData) => Set(false);

    void OnDisable() => Set(false);

    static void Set(bool value)
    {
        if (MobileInput.Instance != null)
            MobileInput.Instance.SetRun(value);
    }
}
