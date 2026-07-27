using UnityEngine;

namespace ShinyMinds.Core
{
    /// <summary>
    /// The ONLY place gameplay "E" is read. Guarantees at most one consumer per
    /// frame and suppresses world interaction while a cutscene owns input.
    ///
    /// Priority is set by [DefaultExecutionOrder] on the callers:
    ///   -100 dialogue / mission advance, -50 NPC conversation, 0 doors.
    /// </summary>
    public static class InteractKey
    {
        public const KeyCode Advance = KeyCode.E;
        public const KeyCode Leave = KeyCode.F;

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

            if (!Input.GetKeyDown(Advance))
                return false;

            consumedFrame = Time.frameCount;
            return true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => consumedFrame = -1;
    }
}
