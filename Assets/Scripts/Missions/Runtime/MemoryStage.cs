using ShinyMinds.Missions.Data;
using UnityEngine;

namespace ShinyMinds.Missions.Runtime
{
    /// <summary>
    /// The little set that fills the memory bubble.
    ///
    /// A remembered conversation has to show the two people in it, and Aisha cannot
    /// play both herself and her own memory — she is the player, standing at the
    /// school gate while she remembers. So the memory is staged as a second, tiny
    /// set parked far below the city, with its own stand-ins and its own camera
    /// rendering into a RenderTexture that the UI shows inside the bubble. Nothing
    /// in the playable world moves, and the world camera never has to cut away.
    ///
    /// Put this component on an always-active object: it toggles <see cref="diorama"/>,
    /// the child that actually holds the camera and the stand-ins, and Unity would
    /// never call Awake here if the whole object started inactive.
    /// </summary>
    [DefaultExecutionOrder(-150)]   // after MissionStagingRoot (-200), before MissionRunner (-100)
    public class MemoryStage : MonoBehaviour
    {
        [Tooltip("Camera plus stand-ins. Switched off while no memory is on screen.")]
        [SerializeField] GameObject diorama;
        [SerializeField] Camera stageCamera;

        [Header("Stand-ins")]
        [Tooltip("Speaks the lines of a speaker whose memorySide is Left.")]
        [SerializeField] Animator leftActor;
        [Tooltip("Speaks the lines of a speaker whose memorySide is Right.")]
        [SerializeField] Animator rightActor;

        /// <summary>
        /// The stage is a scene object and MissionRunner ships inside a prefab, so the
        /// runner falls back to this when its own reference was not wired.
        /// </summary>
        public static MemoryStage Current { get; private set; }

        public bool IsOpen { get; private set; }

        void Awake()
        {
            Current = this;
            IsOpen = false;
            Apply(false);
        }

        void OnDestroy()
        {
            if (Current == this) Current = null;
        }

        /// <summary>Shows or hides the set. Idempotent.</summary>
        public void SetOpen(bool open)
        {
            if (IsOpen == open) return;

            IsOpen = open;
            Apply(open);
        }

        /// <summary>Drives the talking animation on whichever stand-in owns this line.</summary>
        public void SetSpeaking(MemorySide side)
        {
            SetTalking(leftActor, side == MemorySide.Left);
            SetTalking(rightActor, side == MemorySide.Right);
        }

        void Apply(bool open)
        {
            // Stop the mouths before the object goes away, or they resume mid-word the
            // next time the memory opens.
            if (!open) SetSpeaking(MemorySide.None);

            if (diorama != null && diorama.activeSelf != open)
                diorama.SetActive(open);

            // Nothing is rendered into the target texture while the memory is closed.
            if (stageCamera != null)
                stageCamera.enabled = open;
        }

        static void SetTalking(Animator animator, bool talking)
        {
            // An animator on a deactivated stand-in has no controller bound yet, and
            // SetBool on one logs a warning every time.
            if (animator == null || !animator.isActiveAndEnabled ||
                animator.runtimeAnimatorController == null)
                return;

            foreach (AnimatorControllerParameter p in animator.parameters)
            {
                if (p.name == "IsTalking")
                {
                    animator.SetBool("IsTalking", talking);
                    return;
                }
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Current = null;
    }
}
