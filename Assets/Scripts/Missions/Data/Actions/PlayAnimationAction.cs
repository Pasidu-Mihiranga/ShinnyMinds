using System;
using System.Collections;
using UnityEngine;

namespace ShinyMinds.Missions.Data
{
    /// <summary>
    /// Plays a named animator state on an actor and optionally waits for it to finish.
    ///
    /// AnimatorParamAction fires a trigger and hopes the controller is wired to react.
    /// This targets a state directly, so a cutscene beat can be authored as an animation
    /// clip rather than assembled from parameter flips.
    /// </summary>
    [Serializable]
    public class PlayAnimationAction : CutsceneAction
    {
        public string actorKey;

        [Tooltip("Animator state name, or Layer.State for a non-base layer.")]
        public string stateName;

        public int layer = 0;

        [Tooltip("Blend time into the state. 0 snaps.")]
        public float crossFadeSeconds = 0.2f;

        [Tooltip("Block until the clip reaches the end. Off for looping states.")]
        public bool waitForEnd = true;

        [Tooltip("Stop waiting after this long, so a bad state name cannot soft-lock the " +
                 "mission. 0 disables the safety valve.")]
        public float timeoutSeconds = 12f;

        public override IEnumerator Execute(IMissionContext ctx)
        {
            IMissionActor actor = RequireActor(ctx, actorKey, nameof(PlayAnimationAction));
            Animator animator = actor?.Animator;

            if (animator == null || animator.runtimeAnimatorController == null)
            {
                Debug.LogWarning($"PlayAnimationAction: actor '{actorKey}' has no animator controller.");
                yield break;
            }

            if (string.IsNullOrEmpty(stateName))
            {
                Debug.LogWarning("PlayAnimationAction: no state name.");
                yield break;
            }

            int hash = Animator.StringToHash(stateName);

            if (!animator.HasState(layer, hash))
            {
                Debug.LogWarning($"PlayAnimationAction: '{actorKey}' animator has no state '{stateName}' on layer {layer}.");
                yield break;
            }

            if (crossFadeSeconds > 0f)
                animator.CrossFadeInFixedTime(hash, crossFadeSeconds, layer);
            else
                animator.Play(hash, layer, 0f);

            if (!waitForEnd)
                yield break;

            float elapsed = 0f;

            // Wait for the state to actually become current before timing its end,
            // otherwise a crossfade makes us read the previous state's normalizedTime.
            while (animator.GetCurrentAnimatorStateInfo(layer).shortNameHash != hash)
            {
                elapsed += Time.deltaTime;
                if (TimedOut(elapsed)) yield break;
                yield return null;
            }

            while (true)
            {
                AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(layer);

                if (info.shortNameHash != hash) yield break;      // something interrupted it
                if (info.loop) yield break;                       // never ends on its own
                if (info.normalizedTime >= 1f) yield break;

                elapsed += Time.deltaTime;
                if (TimedOut(elapsed)) yield break;

                yield return null;
            }
        }

        bool TimedOut(float elapsed)
        {
            if (timeoutSeconds <= 0f || elapsed < timeoutSeconds)
                return false;

            Debug.LogWarning($"PlayAnimationAction: timed out waiting for '{stateName}' on '{actorKey}'.");
            return true;
        }
    }
}
