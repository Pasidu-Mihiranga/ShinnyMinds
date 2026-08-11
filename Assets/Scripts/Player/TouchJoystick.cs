using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Analog on-screen stick. X turns the player, Y drives forward/back.
///
/// Replaces the Joystick Pack's Joystick.cs, which converted screen points by hand:
///     input = (pointer - centre) / (background.sizeDelta / 2 * canvas.scaleFactor)
/// That only holds for a Screen Space Overlay canvas whose background uses a literal
/// sizeDelta. It drifts under Screen Space Camera, a rotated or nested-scaled canvas,
/// and reads infinity when the background is anchor-stretched (sizeDelta 0).
/// RectTransformUtility does the same job correctly in every case.
/// </summary>
[RequireComponent(typeof(RectTransform))]
[DisallowMultipleComponent]
public class TouchJoystick : MonoBehaviour,
    IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    public enum StickMode
    {
        /// <summary>Base stays put. The finger must land on it.</summary>
        Fixed,
        /// <summary>Base jumps to wherever the finger lands inside this rect.</summary>
        Floating,
    }

    [Header("Parts")]
    [Tooltip("The circular base. Defaults to this object's own RectTransform.")]
    [SerializeField] RectTransform background;
    [Tooltip("The thumb that follows the finger. Must be a child of the background.")]
    [SerializeField] RectTransform handle;

    [Header("Feel")]
    [SerializeField] StickMode mode = StickMode.Fixed;

    [Tooltip("Tilt below this fraction of the radius reads as zero, so a resting thumb " +
             "does not creep the player forward.")]
    [Range(0f, 0.9f)]
    [SerializeField] float deadZone = 0.15f;

    [Tooltip("How far the thumb may travel, as a fraction of the base radius. Keep it at " +
             "1 minus (handle radius / base radius) and the thumb stops flush with the rim.")]
    [Range(0.1f, 1f)]
    [SerializeField] float handleRange = 0.6f;

    /// <summary>-1 (left) .. +1 (right), already past the dead zone.</summary>
    public float Horizontal => input.x;

    /// <summary>-1 (back) .. +1 (forward), already past the dead zone.</summary>
    public float Vertical => input.y;

    public Vector2 Direction => input;
    public bool IsHeld => activePointer != NoPointer;

    // PointerEventData ids are >= 0 for touches and negative for mouse buttons, so the
    // "nobody is holding this" sentinel has to sit outside both ranges.
    const int NoPointer = int.MinValue;

    Vector2 input;
    int activePointer = NoPointer;
    Vector2 restPosition;

    void Awake()
    {
        if (background == null)
            background = (RectTransform)transform;

        restPosition = background.anchoredPosition;
        CentreHandle();
        Release();
    }

    void Reset()
    {
        background = (RectTransform)transform;

        if (transform.childCount > 0)
            handle = transform.GetChild(0) as RectTransform;
    }

    // A stick that is switched off mid-drag never receives OnPointerUp, so without this
    // the last tilt stays latched and the player walks forever. Mission cutscenes hide
    // these controls exactly that way.
    void OnDisable() => Release();

    // ------------------------------------------------------------------ pointer

    public void OnPointerDown(PointerEventData eventData)
    {
        // First finger down owns the stick. A second finger landing on it (usually a
        // stray palm touch while the other thumb is on Jump) must not steal control.
        if (activePointer != NoPointer)
            return;

        activePointer = eventData.pointerId;

        if (mode == StickMode.Floating)
            MoveBaseUnder(eventData);

        UpdateInput(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (eventData.pointerId != activePointer)
            return;

        UpdateInput(eventData);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData.pointerId != activePointer)
            return;

        Release();
    }

    // ------------------------------------------------------------------ maths

    void UpdateInput(PointerEventData eventData)
    {
        if (background == null)
            return;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                background, eventData.position, eventData.pressEventCamera, out Vector2 local))
            return;

        // ScreenPointToLocalPointInRectangle returns a point relative to the pivot;
        // rect.center is where the middle sits in that same space.
        Vector2 fromCentre = local - background.rect.center;
        Vector2 radius = background.rect.size * 0.5f;

        Vector2 raw = new Vector2(
            radius.x > 0.001f ? fromCentre.x / radius.x : 0f,
            radius.y > 0.001f ? fromCentre.y / radius.y : 0f);

        raw = Vector2.ClampMagnitude(raw, 1f);

        float magnitude = raw.magnitude;

        if (magnitude <= deadZone)
        {
            input = Vector2.zero;
        }
        else
        {
            // Ramp from 0 at the dead-zone edge rather than jumping straight to
            // `deadZone`, so the first bit of tilt is a gentle walk and not a lurch.
            float scaled = Mathf.InverseLerp(deadZone, 1f, magnitude);
            input = (raw / magnitude) * scaled;
        }

        // The thumb tracks the finger, not the dead-zoned value — a thumb that refuses
        // to move until you cross the dead zone reads as an unresponsive control.
        if (handle != null)
            handle.anchoredPosition = raw * radius * handleRange;
    }

    void MoveBaseUnder(PointerEventData eventData)
    {
        RectTransform area = (RectTransform)transform;

        if (background == area || background.parent == null)
            return;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                area, eventData.position, eventData.pressEventCamera, out Vector2 local))
            background.anchoredPosition = local - area.rect.center + restPosition;
    }

    void Release()
    {
        input = Vector2.zero;
        activePointer = NoPointer;

        if (handle != null)
            handle.anchoredPosition = Vector2.zero;

        if (background != null && mode == StickMode.Floating)
            background.anchoredPosition = restPosition;
    }

    void CentreHandle()
    {
        if (handle == null)
            return;

        // anchoredPosition is only meaningful as "offset from the base centre" when the
        // handle is centre-anchored. Authoring mistakes here are invisible until you drag.
        Vector2 centre = new Vector2(0.5f, 0.5f);
        handle.anchorMin = centre;
        handle.anchorMax = centre;
        handle.pivot = centre;
        handle.anchoredPosition = Vector2.zero;
    }
}
