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
        [SerializeField] float groundRayHeight = 2f;
        [SerializeField] float groundRayLength = 8f;
        [SerializeField] LayerMask groundMask = ~0;

        [Header("Optional")]
        [Tooltip("Assign for GIRL 1. A CharacterController ignores direct transform writes.")]
        [SerializeField] CharacterController characterController;
        [Tooltip("Downward push applied through the CharacterController so it stays grounded.")]
        [SerializeField] float ccGravity = 4f;

        public bool IsMoving { get; private set; }

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

        public IEnumerator MoveTo(Vector3 target, bool run, bool backwards = false)
        {
            IsMoving = true;

            float baseSpeed = backwards ? backSpeed : (run ? runSpeed : walkSpeed);
            float speed = baseSpeed * ScaleFactor;

            SetAnim(run ? runAnimValue : walkAnimValue, backwards);

            float sqrArrive = arriveDistance * arriveDistance;

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

                yield return null;
            }

            SetAnim(0f, false);
            IsMoving = false;
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
            SetAnim(0f, false);
            SetTurn(false, false);
            IsMoving = false;
        }

        public void SnapDown()
        {
            Vector3 origin = transform.position + Vector3.up * groundRayHeight;

            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit,
                                groundRayLength, groundMask, QueryTriggerInteraction.Ignore))
            {
                Vector3 p = transform.position;
                p.y = hit.point.y;
                transform.position = p;
            }
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
