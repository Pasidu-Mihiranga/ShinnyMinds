using UnityEngine;

namespace ShinyMinds.Core
{
    /// <summary>
    /// The ONLY place the gameplay advance input is read — "E" on a keyboard, a tap on
    /// the play area on a touch device. Guarantees at most one consumer per frame and
    /// suppresses world interaction while a cutscene owns input.
    ///
    /// Priority is set by [DefaultExecutionOrder] on the callers:
    ///   -100 dialogue / mission advance, -50 NPC conversation, 0 doors.
    /// </summary>
    public static class InteractKey
    {
        public const KeyCode Advance = KeyCode.E;
        public const KeyCode Leave = KeyCode.F;

        /// <summary>
        /// What to tell the player to do: "Press E" on a keyboard, "Touch" on a phone.
        /// Every on-screen prompt builds its text from this so the two never disagree.
        /// </summary>
        public static string AdvanceLabel => TouchInput.AdvanceLabel;

        static int consumedFrame = -1;

        /// <summary>
        /// For world interactables (doors, NPC proximity zones).
        /// Blocked entirely while gameplay is locked.
        /// </summary>
        public static bool TryConsumeWorld()
        {
            if (PlayerInputLock.IsLocked)
                return false;

            return TryConsumeUI();
        }

        /// <summary>
        /// For dialogue UIs that already own the lock. Not blocked by the lock,
        /// but still exclusive within the frame.
        /// </summary>
        public static bool TryConsumeUI()
        {
            if (consumedFrame == Time.frameCount)
                return false;

            // The key still works on a touch device — a Bluetooth keyboard, or a phone
            // build being tested in the editor — so this is an "either" and not a switch.
            if (!Input.GetKeyDown(Advance) && !TouchInput.TapPressedThisFrame())
                return false;

            consumedFrame = Time.frameCount;
            return true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => consumedFrame = -1;
    }
}
