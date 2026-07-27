using ShinyMinds.Missions.Data;
using UnityEngine;

namespace ShinyMinds.Missions.Runtime
{
    /// <summary>
    /// Makes a scene character addressable by mission data, and remembers its spawn
    /// state so a retry can put the world back exactly as it started.
    /// Put one on GIRL 1, Teacher, Mother and the Stranger.
    /// </summary>
    [DisallowMultipleComponent]
    public class MissionActor : MonoBehaviour, IMissionActor
    {
        [SerializeField] string actorKey;
        [SerializeField] Animator animator;
        [SerializeField] ActorMover mover;
        [SerializeField] CharacterController characterController;

        Vector3 spawnPosition;
        Quaternion spawnRotation;
        bool spawnActive;
        bool initialised;

        public string ActorKey => actorKey;
        public Transform Transform => transform;
        public GameObject GameObject => gameObject;
        public Animator Animator => animator;
        public IActorMover Mover => mover;

        void Reset()
        {
            animator = GetComponent<Animator>();
            mover = GetComponent<ActorMover>();
            characterController = GetComponent<CharacterController>();
        }

        void Awake() => EnsureInitialised();

        void OnDestroy() => MissionSceneRegistry.Unregister(this);

        /// <summary>
        /// Captures spawn state and registers. Called from Awake for actors that start
        /// active, and from MissionStagingRoot for those that start inactive — Unity
        /// never runs Awake on a component of an inactive GameObject, so the Stranger
        /// and Mother would otherwise never register and every action targeting them
        /// would silently do nothing.
        /// </summary>
        public void EnsureInitialised()
        {
            if (initialised)
                return;

            initialised = true;

            if (animator == null) animator = GetComponent<Animator>();
            if (mover == null) mover = GetComponent<ActorMover>();
            if (characterController == null) characterController = GetComponent<CharacterController>();

            CaptureSpawnState();
            MissionSceneRegistry.Register(this);
        }

        public void CaptureSpawnState()
        {
            spawnPosition = transform.position;
            spawnRotation = transform.rotation;
            spawnActive = gameObject.activeSelf;
        }

        public void TeleportTo(Vector3 position, Quaternion rotation, bool snapToGround)
        {
            // A CharacterController silently reverts direct transform writes, so it has
            // to be switched off around the move.
            bool hadController = characterController != null && characterController.enabled;
            if (hadController) characterController.enabled = false;

            transform.SetPositionAndRotation(position, rotation);

            // PlayerController only snaps to ground in Start(), so a jump-cut needs this.
            if (snapToGround && mover != null)
                mover.SnapDown();

            if (hadController) characterController.enabled = true;
        }

        public void ResetToSpawn()
        {
            mover?.Stop();

            TeleportTo(spawnPosition, spawnRotation, false);

            // Clear any latched emote trigger (Fear / Sad / Laugh) left mid-cutscene.
            if (animator != null && animator.runtimeAnimatorController != null)
            {
                animator.Rebind();
                animator.Update(0f);
            }

            if (gameObject.activeSelf != spawnActive)
                gameObject.SetActive(spawnActive);
        }
    }
}
