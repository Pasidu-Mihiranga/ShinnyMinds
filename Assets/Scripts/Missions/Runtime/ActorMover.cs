using System.Collections;
using ShinyMinds.Missions.Data;
using UnityEngine;

namespace ShinyMinds.Missions.Runtime
{
    /// <summary>
    /// Walks an actor between hand-authored markers, driving the animator parameters
    /// the project already uses.
    ///
    /// Deliberately not NavMesh: none is baked, baking one over Worldcity.fbx is a
    /// long detour, and every path in a staged mission is short and straight.
    /// </summary>
    [DisallowMultipleComponent]
    public class ActorMover : MonoBehaviour, IActorMover
    {
        [Header("Speeds (metres/second at scale 1)")]
        [SerializeField] float walkSpeed = 1.4f;
        [SerializeField] float runSpeed = 3.5f;
        [SerializeField] float backSpeed = 0.9f;
        [SerializeField] float turnDegreesPerSecond = 360f;
        [SerializeField] float arriveDistance = 0.15f;

        [Header("Animator")]
        [SerializeField] Animator animator;
        [SerializeField] string speedParam = "Speed";
        [SerializeField] string backwardParam = "Backward";
        [Tooltip("Must match PlayerController's hardcoded animator values (0 / 2 / 6).")]
        [SerializeField] float walkAnimValue = 2f;
        [SerializeField] float runAnimValue = 6f;

        [Header("Ground")]
        [SerializeField] bool stickToGround = true;
        [Tooltip("How far above the feet the ground ray starts. Must clear the road mesh, " +
                 "but anything it starts inside can pull the actor up — see maxSnapStepUp.")]
        [SerializeField] float groundRayHeight = 2f;
        [Tooltip("How far BELOW the feet to look for ground. Scaled by the actor's lossyScale, " +
                 "so a scale-2 adult still reaches down as far as a scale-1 one.")]
        [SerializeField] float groundRayLength = 8f;
        [Tooltip("Largest upward correction the ground snap may apply, in metres at scale 1. " +
                 "A kerb, yes; a rooftop, no. See SnapDown for why this matters.")]
        [SerializeField] float maxSnapStepUp = 0.4f;
        [SerializeField] LayerMask groundMask = ~0;
        [Tooltip("Ground the character by its lowest rendered point instead of by the transform " +
                 "origin. Humanoid retargeting can seat the body above the origin, which no " +
                 "amount of raycasting corrects. See RenderedFootLift.")]
        [SerializeField] bool groundByRenderedFeet = true;

        [Header("Root motion")]
        [Tooltip("Let the animation drive travel instead of translating the transform. " +
                 "Feet plant properly and turns carry weight, at the cost of exact arrival " +
                 "timing. Requires looping locomotion clips with root motion baked in.")]
        [SerializeField] bool useRootMotion = false;
        [Tooltip("Give up and snap to the target after this long, so a clip with too little " +
                 "root translation cannot stall a cutscene. 0 disables.")]
        [SerializeField] float rootMotionTimeout = 20f;

        [Header("Optional")]
        [Tooltip("Assign for GIRL 1. A CharacterController ignores direct transform writes.")]
        [SerializeField] CharacterController characterController;
        [Tooltip("Downward push applied through the CharacterController so it stays grounded.")]
        [SerializeField] float ccGravity = 4f;

        bool rootMotionActive;
        bool cachedApplyRootMotion;

        // SnapDown runs every frame of a move, so its warnings are latched to one per
        // move — otherwise a single bad path buries the console.
        bool warnedNoGround;
        bool warnedClimb;

        Renderer[] renderers;
        float calibratedLift = float.PositiveInfinity;
        float calibrationStartTime = -1f;
        float calibrationEndTime = -1f;
        float calibratedScale;          // lossyScale.y the lift above was measured at

        // Long enough for the Animator to settle into idle and for the bounds to describe a
        // real pose rather than the bind pose, short enough to close before an emote plays.
        const float CalibrationSeconds = 1f;

        // Dead time before sampling starts, so the first frames — where the Animator has not
        // run yet and the mesh is still in its bind pose — cannot poison the measurement.
        const float CalibrationDelay = 0.2f;

        public bool IsMoving { get; private set; }
        public bool UseRootMotion => useRootMotion;

        /// <summary>
        /// Mixamo clips are authored for a roughly 1-unit rig with root motion off, but
        /// GIRL 1 is scale 5 and Teacher/Mother are scale 2. Without scaling the world
        /// speed to match, the legs cycle at full stride while the actor inches forward.
        /// </summary>
        float ScaleFactor => Mathf.Max(0.01f, Mathf.Abs(transform.lossyScale.y));

        void Reset()
        {
            animator = GetComponent<Animator>();
            characterController = GetComponent<CharacterController>();
        }

        /// <summary>
        /// Keeps the actor planted while standing still, not just while walking.
        ///
        /// The retargeting lift RenderedFootLift corrects only exists once the Animator has
        /// evaluated, so this has to be LateUpdate — in Update the bounds still describe
        /// last frame's pose. It also covers the long stretches where a mission actor is
        /// idling through dialogue and no MoveTo coroutine is running to re-ground them.
        ///
        /// Actors driven by a CharacterController (the player) are left alone; their own
        /// controller owns their vertical position.
        /// </summary>
        void LateUpdate()
        {
            if (!stickToGround) return;
            if (characterController != null && characterController.enabled) return;

            SnapDown();
        }

        public IEnumerator MoveTo(Vector3 target, bool run, bool backwards = false)
        {
            IsMoving = true;
            warnedNoGround = false;
            warnedClimb = false;

            float baseSpeed = backwards ? backSpeed : (run ? runSpeed : walkSpeed);
            float speed = baseSpeed * ScaleFactor;

            SetAnim(run ? runAnimValue : walkAnimValue, backwards);
            BeginRootMotion();

            float sqrArrive = arriveDistance * arriveDistance;
            float elapsed = 0f;

            while (true)
            {
                Vector3 here = transform.position;
                Vector3 flatTarget = new Vector3(target.x, here.y, target.z);
                Vector3 toTarget = flatTarget - here;

                if (toTarget.sqrMagnitude <= sqrArrive)
                    break;

                Vector3 dir = toTarget.normalized;

                // Face along travel, or away from it when stepping backwards.
                Vector3 faceDir = backwards ? -dir : dir;
                if (faceDir.sqrMagnitude > 0.0001f)
                {
                    transform.rotation = Quaternion.RotateTowards(
                        transform.rotation,
                        Quaternion.LookRotation(faceDir, Vector3.up),
                        turnDegreesPerSecond * Time.deltaTime);
                }

                // Under root motion the clip supplies the translation; we only steer.
                // See OnAnimatorMove.
                if (!rootMotionActive)
                {
                    Vector3 delta = dir * speed * Time.deltaTime;

                    // Never overshoot on a long frame.
                    if (delta.sqrMagnitude > toTarget.sqrMagnitude)
                        delta = toTarget;

                    if (characterController != null && characterController.enabled)
                    {
                        characterController.Move(delta + Vector3.down * ccGravity * Time.deltaTime);
                    }
                    else
                    {
                        transform.position += delta;
                        if (stickToGround) SnapDown();
                    }
                }

                elapsed += Time.deltaTime;
                if (rootMotionActive && rootMotionTimeout > 0f && elapsed >= rootMotionTimeout)
                {
                    Debug.LogWarning($"ActorMover on '{name}': root motion did not reach the target in " +
                                     $"{rootMotionTimeout}s. Snapping. Check the clip actually has baked " +
                                     "root translation.", this);
                    SnapTo(flatTarget);
                    break;
                }

                yield return null;
            }

            EndRootMotion();
            SetAnim(0f, false);
            IsMoving = false;
        }

        // ------------------------------------------------------------- root motion

        void BeginRootMotion()
        {
            if (!useRootMotion || animator == null || rootMotionActive)
                return;

            // PlayerController forces applyRootMotion off in Start(), so it has to be
            // turned on for the cutscene and put back afterwards.
            cachedApplyRootMotion = animator.applyRootMotion;
            animator.applyRootMotion = true;
            rootMotionActive = true;
        }

        void EndRootMotion()
        {
            if (!rootMotionActive || animator == null)
                return;

            animator.applyRootMotion = cachedApplyRootMotion;
            rootMotionActive = false;
        }

        void OnAnimatorMove()
        {
            if (!rootMotionActive || animator == null)
                return;

            Vector3 delta = animator.deltaPosition;

            if (characterController != null && characterController.enabled)
            {
                characterController.Move(delta + Vector3.down * ccGravity * Time.deltaTime);
            }
            else
            {
                transform.position += delta;
                if (stickToGround) SnapDown();
            }

            // Rotation stays under MoveTo's control so the actor still steers toward the
            // marker; the clip only contributes translation.
        }

        void SnapTo(Vector3 position)
        {
            bool hadController = characterController != null && characterController.enabled;
            if (hadController) characterController.enabled = false;

            transform.position = position;
            if (stickToGround) SnapDown();

            if (hadController) characterController.enabled = true;
        }

        public IEnumerator FaceTowards(Vector3 worldPoint, float seconds)
        {
            Vector3 d = worldPoint - transform.position;
            d.y = 0f;

            if (d.sqrMagnitude < 0.0001f)
                yield break;

            yield return FaceDirection(d, seconds);
        }

        public IEnumerator FaceDirection(Vector3 direction, float seconds)
        {
            direction.y = 0f;

            if (direction.sqrMagnitude < 0.0001f)
                yield break;

            Quaternion from = transform.rotation;
            Quaternion to = Quaternion.LookRotation(direction.normalized, Vector3.up);

            if (seconds <= 0f)
            {
                transform.rotation = to;
                yield break;
            }

            // Drive the existing turn bools so the Left/Right Turn clips play.
            bool right = Vector3.SignedAngle(transform.forward, direction, Vector3.up) > 0f;
            SetTurn(!right, right);

            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / seconds;
                transform.rotation = Quaternion.Slerp(from, to, Mathf.Clamp01(t));
                yield return null;
            }

            transform.rotation = to;
            SetTurn(false, false);
        }

        /// <summary>
        /// Returns to idle. Note the MoveTo/Face coroutines are driven by MissionRunner,
        /// not by this component, so stopping them is the runner's job — this only
        /// clears the animator state left behind.
        /// </summary>
        public void Stop()
        {
            EndRootMotion();
            SetAnim(0f, false);
            SetTurn(false, false);
            IsMoving = false;
        }

        /// <summary>
        /// How far the character's lowest rendered point sits above its transform origin.
        ///
        /// Unity's Humanoid retargeting seats the body using the Avatar's proportions, and
        /// for Mixamo rigs — whose FBX root is exported at hip height — the result can sit
        /// well above the GameObject origin. It scales with lossyScale, so the scale-2
        /// Stranger floats about twice as far as a scale-1 actor. Disabling the Animator
        /// appears to fix it only because the bones fall back to the bind pose, where the
        /// origin really is at the feet.
        ///
        /// So grounding the origin is not the same as grounding the character. Measuring the
        /// rendered bounds corrects it whatever the residual turns out to be, without having
        /// to chase it through Avatar and clip import settings.
        /// </summary>
        float RenderedFootLift()
        {
            if (!groundByRenderedFeet) return 0f;

            if (renderers == null || renderers.Length == 0)
                renderers = GetComponentsInChildren<Renderer>(true);

            float lowest = float.PositiveInfinity;
            foreach (Renderer r in renderers)
                if (r != null && r.enabled)
                    lowest = Mathf.Min(lowest, r.bounds.min.y);

            if (float.IsPositiveInfinity(lowest))
                return 0f;

            // The lift is a WORLD-space distance, so it is only valid for the scale it was
            // measured at — halve the actor and the real lift halves with it. Without this,
            // rescaling an actor keeps applying the old figure forever and leaves it
            // floating (if shrunk) or sunk into the pavement (if enlarged).
            float currentScale = ScaleFactor;

            if (!Mathf.Approximately(currentScale, calibratedScale))
            {
                calibratedScale = currentScale;
                RecalibrateFootLift();
            }

            float live = lowest - transform.position.y;

            // Take the SMALLEST lift seen during a short settling window, not this frame's.
            // The retargeting residual is constant; the clip's own vertical motion is not.
            // Correcting by the minimum removes the residual and leaves the animation free
            // to lift the feet — correct by the live value instead and a run cycle's
            // airborne phase is flattened into a glide. The window then closes so a later
            // emote (Sad sits the character down) cannot poison the reference.
            if (calibrationStartTime < 0f)
            {
                calibrationStartTime = Time.time + CalibrationDelay;
                calibrationEndTime = calibrationStartTime + CalibrationSeconds;
            }

            // Do not sample until the Animator has actually produced a pose. On the frame an
            // actor is switched on, the bounds still describe the BIND pose — where the origin
            // really is at the feet, so the lift reads as ~0. Mathf.Min below would latch that
            // for the whole session, SnapDown would then ground the ORIGIN rather than the
            // feet, and the actor floats by the entire retargeting residual. Track the live
            // value meanwhile, so the delay itself never leaves them mis-grounded.
            if (Time.time < calibrationStartTime)
                return live;

            if (Time.time <= calibrationEndTime)
                calibratedLift = Mathf.Min(calibratedLift, live);

            return float.IsPositiveInfinity(calibratedLift) ? live : calibratedLift;
        }

        /// <summary>
        /// Reopens the settling window, e.g. after swapping an actor's rig at runtime.
        /// </summary>
        public void RecalibrateFootLift()
        {
            calibratedLift = float.PositiveInfinity;
            calibrationStartTime = -1f;
            calibrationEndTime = -1f;
        }

        /// <summary>
        /// Plants the actor's feet on the ground below them.
        ///
        /// The ray has to start above the feet or it would begin inside the road mesh
        /// and miss — but that also lets it hit a rooftop, awning or car roof the actor
        /// is passing under and snap him UP onto it. MoveTo only steers in XZ, and most
        /// mission actors have no CharacterController (so no gravity), which means one
        /// bad upward snap strands an actor in the sky for the rest of the cutscene.
        /// Hence: drops of any size are accepted, climbs beyond a kerb are refused.
        ///
        /// If this warns, the usual cause is a marker left at its auto-generated
        /// position while the rest of the staging was dragged elsewhere, sending the
        /// actor on a long walk through the middle of the city.
        /// </summary>
        public void SnapDown()
        {
            float scale = ScaleFactor;
            float above = groundRayHeight * scale;
            float below = groundRayLength * scale;

            Vector3 feet = transform.position;

            // Cast from where the character actually LOOKS like it is, not from its origin.
            float lift = RenderedFootLift();
            Vector3 soles = new Vector3(feet.x, feet.y + lift, feet.z);

            if (!Physics.Raycast(soles + Vector3.up * above, Vector3.down, out RaycastHit hit,
                                 above + below, groundMask, QueryTriggerInteraction.Ignore))
            {
                if (!warnedNoGround)
                {
                    warnedNoGround = true;
                    Debug.LogWarning($"ActorMover on '{name}': no ground within {below:0.#}m below " +
                                     $"{feet}. Leaving the actor at y={feet.y:0.##}. Check the target " +
                                     "marker actually sits over the street.", this);
                }
                return;
            }

            float climb = hit.point.y - soles.y;
            if (climb > maxSnapStepUp * scale)
            {
                if (!warnedClimb)
                {
                    warnedClimb = true;
                    Debug.LogWarning($"ActorMover on '{name}': ground ray hit '{hit.collider.name}' " +
                                     $"{climb:0.##}m above the feet — refusing to climb onto it. The " +
                                     "actor is walking through geometry; move the markers so the path " +
                                     "stays on the street.", this);
                }
                return;
            }

            // Place the ORIGIN so the rendered soles land on the hit point.
            feet.y = hit.point.y - lift;
            transform.position = feet;
        }

        void SetAnim(float speed, bool backwards)
        {
            if (animator == null) return;

            if (HasParam(speedParam)) animator.SetFloat(speedParam, speed);
            if (HasParam(backwardParam)) animator.SetBool(backwardParam, backwards);
        }

        void SetTurn(bool left, bool right)
        {
            if (animator == null) return;

            if (HasParam("TurnLeft")) animator.SetBool("TurnLeft", left);
            if (HasParam("TurnRight")) animator.SetBool("TurnRight", right);
        }

        bool HasParam(string name)
        {
            if (string.IsNullOrEmpty(name) || animator == null || animator.runtimeAnimatorController == null)
                return false;

            foreach (AnimatorControllerParameter p in animator.parameters)
                if (p.name == name) return true;

            return false;
        }
    }
}
