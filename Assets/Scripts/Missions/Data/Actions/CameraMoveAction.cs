using System;
using System.Collections;
using UnityEngine;

namespace ShinyMinds.Missions.Data
{
    /// <summary>
    /// A dolly along the current view axis, with optional shake.
    ///
    /// A slow push-in during a line is the cheapest way to build pressure without
    /// anything overtly frightening happening on screen — which is exactly the register
    /// this mission needs.
    /// </summary>
    [Serializable]
    public class CameraMoveAction : CutsceneAction
    {
        [Tooltip("Metres to dolly. Positive pushes in, negative pulls back.")]
        public float pushInDistance = 0.8f;

        public float seconds = 2f;

        [Tooltip("Off (default) lets the dolly keep running under the lines that follow, " +
                 "which is almost always what you want — a cutscene node joins its " +
                 "parallel actions before it ends, so a blocking push stalls the very " +
                 "line it exists to underscore.")]
        public bool waitForEnd = false;

        [Header("Shake")]
        [Tooltip("Metres of positional jitter. Keep tiny — 0.02 is already noticeable.")]
        public float shakeAmplitude = 0f;
        public float shakeDuration = 0.4f;

        public override IEnumerator Execute(IMissionContext ctx)
        {
            if (ctx.Camera == null)
                yield break;

            if (shakeAmplitude > 0f)
                ctx.Camera.Shake(shakeAmplitude, shakeDuration);

            if (waitForEnd)
            {
                yield return ctx.Camera.PushIn(pushInDistance, seconds);
            }
            else
            {
                ctx.Camera.BeginPushIn(pushInDistance, seconds);
                yield break;
            }
        }
    }
}
