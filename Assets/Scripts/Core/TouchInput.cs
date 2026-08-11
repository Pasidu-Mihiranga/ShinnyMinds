using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ShinyMinds.Core
{
    /// <summary>
    /// Answers the two questions every prompt and every "advance" read needs on a phone:
    /// is this a touch device, and did a finger just land somewhere that counts as the
    /// E press.
    ///
    /// A tap anywhere on the play area advances rather than one small hit box: while a
    /// line is on screen PlayerInputLock has already hidden the stick and the buttons, so
    /// a thumb landing anywhere can only mean "next". Taps on a control that handles its
    /// own pointer are still ignored, which is what keeps a joystick drag, a Jump press or
    /// a choice button from also counting as an advance.
    /// </summary>
    public static class TouchInput
    {
        public const string TouchLabel = "Touch";
        public const string KeyLabel = "Press E";

        static bool simulated;

        /// <summary>
        /// True on phones and tablets. <see cref="MobileInput"/> can force it on so the
        /// touch prompts and tap-to-advance are testable in the Game view.
        /// </summary>
        public static bool Active =>
            simulated || Application.isMobilePlatform || SystemInfo.deviceType == DeviceType.Handheld;

        /// <summary>What the on-screen prompts should call the advance input.</summary>
        public static string AdvanceLabel => Active ? TouchLabel : KeyLabel;

        public static void SetSimulated(bool value) => simulated = value;

        /// <summary>
        /// True for the one frame a finger lands on the play area. Touch-down rather than
        /// lift: a dialogue that waits for the finger to leave the glass reads as late.
        /// </summary>
        public static bool TapPressedThisFrame()
        {
            if (!Active)
                return false;

            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch touch = Input.GetTouch(i);

                if (touch.phase == TouchPhase.Began && !IsOverWidget(touch.position))
                    return true;
            }

            // A desktop test run has no touches at all, so the simulation reads the mouse.
            return simulated && Input.touchCount == 0 &&
                   Input.GetMouseButtonDown(0) && !IsOverWidget(Input.mousePosition);
        }

        static readonly List<RaycastResult> hits = new List<RaycastResult>();

        /// <summary>
        /// Whether this screen point is over a control that handles its own pointer — the
        /// stick, Jump, Run, a choice button, an ending card button. Plain panels, labels
        /// and speech balloons are not widgets: tapping the dialogue box itself is exactly
        /// how a player expects to advance it.
        /// </summary>
        static bool IsOverWidget(Vector2 screenPosition)
        {
            EventSystem events = EventSystem.current;

            if (events == null)
                return false;

            // Our own raycast rather than IsPointerOverGameObject(fingerId): the scene's
            // EventSystem runs the Input System module, whose pointer ids are not the
            // legacy finger ids read above, so the two would not line up.
            var pointer = new PointerEventData(events) { position = screenPosition };

            hits.Clear();
            events.RaycastAll(pointer, hits);

            for (int i = 0; i < hits.Count; i++)
            {
                if (HandlesPointer(hits[i].gameObject))
                    return true;
            }

            return false;
        }

        static bool HandlesPointer(GameObject go)
        {
            if (go == null)
                return false;

            // Parents too: the tap may land on a button's label or on the stick's handle,
            // neither of which carries the handler itself.
            MonoBehaviour[] components = go.GetComponentsInParent<MonoBehaviour>(true);

            for (int i = 0; i < components.Length; i++)
            {
                MonoBehaviour c = components[i];

                if (c is IPointerDownHandler || c is IPointerClickHandler || c is IDragHandler)
                    return true;
            }

            return false;
        }

        // "Enter Play Mode Options -> Disable Domain Reload" keeps statics between sessions,
        // so a simulation switched on for one test run must not leak into the next.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => simulated = false;
    }
}
