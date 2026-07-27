using UnityEngine;

namespace ShinyMinds.Core
{
    /// <summary>
    /// Applies PlayerInputLock to the player rig. Put this on GIRL 1.
    ///
    /// Disabling PlayerController alone is not enough. It writes the animator
    /// parameters every Update, so a disabled controller freezes them at their last
    /// value and the character walks in place; and footstepaudio only calls Stop()
    /// in its else-branch, so disabling it mid-stride leaves the loop playing.
    /// Both of those are visible in the existing Groq NPC dialogue today.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerLockBinder : MonoBehaviour
    {
        [Header("Components to disable while locked")]
        [SerializeField] PlayerController playerController;
        [SerializeField] CameraController cameraController;
        [SerializeField] MapToggle mapToggle;
        [SerializeField] footstepaudio footsteps;

        [Header("Cleanup on lock")]
        [Tooltip("The looping footstep AudioSource. Stopped explicitly on lock.")]
        [SerializeField] AudioSource footstepSource;
        [SerializeField] Animator animator;

        [Header("Animator parameter names")]
        [SerializeField] string speedParam = "Speed";
        [SerializeField] string turnLeftParam = "TurnLeft";
        [SerializeField] string turnRightParam = "TurnRight";
        [SerializeField] string backwardParam = "Backward";

        void Reset()
        {
            playerController = GetComponent<PlayerController>();
            cameraController = GetComponentInChildren<CameraController>(true);
            footsteps = GetComponent<footstepaudio>();
            animator = GetComponent<Animator>();
            if (footsteps != null)
                footstepSource = footsteps.audioSource;
        }

        void OnEnable()
        {
            PlayerInputLock.GameplayLockChanged += Apply;
            Apply(PlayerInputLock.IsLocked);
        }

        void OnDisable()
        {
            PlayerInputLock.GameplayLockChanged -= Apply;
        }

        void Apply(bool locked)
        {
            if (playerController != null) playerController.enabled = !locked;
            if (cameraController != null) cameraController.enabled = !locked;
            if (mapToggle != null) mapToggle.enabled = !locked;
            if (footsteps != null) footsteps.enabled = !locked;

            if (!locked)
                return;

            // Kill the looping walk/run clip that footstepaudio can no longer stop.
            if (footstepSource != null)
                footstepSource.Stop();

            // Zero the locomotion parameters PlayerController can no longer write.
            if (animator == null)
                return;

            SetFloatIfPresent(speedParam, 0f);
            SetBoolIfPresent(turnLeftParam, false);
            SetBoolIfPresent(turnRightParam, false);
            SetBoolIfPresent(backwardParam, false);
        }

        void SetFloatIfPresent(string param, float value)
        {
            if (HasParam(param)) animator.SetFloat(param, value);
        }

        void SetBoolIfPresent(string param, bool value)
        {
            if (HasParam(param)) animator.SetBool(param, value);
        }

        bool HasParam(string param)
        {
            if (string.IsNullOrEmpty(param) || animator == null || animator.runtimeAnimatorController == null)
                return false;

            foreach (AnimatorControllerParameter p in animator.parameters)
                if (p.name == param) return true;

            return false;
        }
    }
}
