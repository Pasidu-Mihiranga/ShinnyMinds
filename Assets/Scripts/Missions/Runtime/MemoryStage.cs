using System.Collections;
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

        [Header("Grounding")]
        [Tooltip("The y INSIDE the diorama that the stand-ins' feet should stand on.")]
        [SerializeField] float floorLocalY = 0f;
        [Tooltip("How long to keep measuring after the memory opens. The retargeting lift only " +
                 "exists once the Animator has posed the rig, so it cannot be read on frame one.")]
        [SerializeField] float settleSeconds = 0.6f;

        Coroutine grounding;

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

            if (grounding != null)
            {
                StopCoroutine(grounding);
                grounding = null;
            }

            if (open && isActiveAndEnabled)
                grounding = StartCoroutine(GroundStandIns());
        }

        /// <summary>
        /// Plants the stand-ins' feet on the diorama floor.
        ///
        /// They are a Transform and an Animator and nothing else — no ActorMover, so none of its
        /// grounding applies to them. Unity's Humanoid retargeting seats the body using the
        /// Avatar's proportions, and for a Mixamo rig, whose FBX root is exported at hip height,
        /// the rendered body can sit well above the GameObject origin. The residual scales with
        /// the actor, so the scale-2 Mother floated about twice as far as the scale-5 girl — and
        /// with a sky backdrop behind the diorama, a floating stand-in reads as hanging in mid-air.
        ///
        /// Measured over a short window rather than on the frame the memory opens: until the
        /// Animator has evaluated, the bounds still describe the BIND pose, where the origin
        /// really is at the feet and the lift reads as zero. The SMALLEST lift seen wins, because
        /// the retargeting residual is constant while the clip's own vertical motion is not —
        /// correcting by the live value every frame would flatten the animation into a glide.
        /// </summary>
        IEnumerator GroundStandIns()
        {
            Transform[] standIns =
            {
                leftActor != null ? leftActor.transform : null,
                rightActor != null ? rightActor.transform : null,
            };

            var lifts = new float[standIns.Length];
            for (int i = 0; i < lifts.Length; i++) lifts[i] = float.PositiveInfinity;

            float until = Time.time + Mathf.Max(0.1f, settleSeconds);

            while (Time.time < until)
            {
                for (int i = 0; i < standIns.Length; i++)
                {
                    Transform t = standIns[i];
                    if (t == null) continue;

                    float lowest = LowestRenderedY(t);
                    if (float.IsInfinity(lowest)) continue;

                    // How far the feet sit above the origin, this frame.
                    lifts[i] = Mathf.Min(lifts[i], lowest - t.position.y);

                    if (float.IsInfinity(lifts[i])) continue;

                    Vector3 p = t.position;
                    p.y = FloorWorldY() - lifts[i];
                    t.position = p;
                }

                yield return null;
            }

            grounding = null;
        }

        float FloorWorldY()
        {
            Transform frame = diorama != null ? diorama.transform : transform;
            return frame.TransformPoint(new Vector3(0f, floorLocalY, 0f)).y;
        }

        static float LowestRenderedY(Transform t)
        {
            float lowest = float.PositiveInfinity;

            foreach (Renderer r in t.GetComponentsInChildren<Renderer>(true))
                if (r != null && r.enabled)
                    lowest = Mathf.Min(lowest, r.bounds.min.y);

            return lowest;
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
