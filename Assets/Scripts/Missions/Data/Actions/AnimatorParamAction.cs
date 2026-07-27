using System;
using System.Collections;
using UnityEngine;

namespace ShinyMinds.Missions.Data
{
    [Serializable]
    public class AnimatorParamAction : CutsceneAction
    {
        public enum ParamKind { Trigger, Bool, Float }

        public string actorKey;
        public string paramName = "Fear";
        public ParamKind kind = ParamKind.Trigger;
        public bool boolValue;
        public float floatValue;

        [Tooltip("Hold before continuing so the emote is readable.")]
        public float holdSeconds = 0f;

        public override IEnumerator Execute(IMissionContext ctx)
        {
            IMissionActor actor = RequireActor(ctx, actorKey, nameof(AnimatorParamAction));
            Animator animator = actor?.Animator;

            if (animator == null)
            {
                Debug.LogWarning($"AnimatorParamAction: actor '{actorKey}' has no Animator.");
            }
            else if (!HasParam(animator, paramName))
            {
                Debug.LogWarning($"AnimatorParamAction: '{actorKey}' has no parameter '{paramName}'.");
            }
            else
            {
                switch (kind)
                {
                    case ParamKind.Trigger: animator.SetTrigger(paramName); break;
                    case ParamKind.Bool: animator.SetBool(paramName, boolValue); break;
                    case ParamKind.Float: animator.SetFloat(paramName, floatValue); break;
                }
            }

            if (holdSeconds > 0f)
                yield return new WaitForSeconds(holdSeconds);
        }

        static bool HasParam(Animator animator, string name)
        {
            if (string.IsNullOrEmpty(name) || animator.runtimeAnimatorController == null)
                return false;

            foreach (AnimatorControllerParameter p in animator.parameters)
                if (p.name == name) return true;

            return false;
        }
    }
}
