using System;
using System.Collections;
using UnityEngine;

namespace ShinyMinds.Missions.Data
{
    [Serializable]
    public class CameraShotAction : CutsceneAction
    {
        [Tooltip("Marker whose position and rotation define the shot.")]
        public string markerKey;

        [Tooltip("0 = cut. Greater than 0 = blend from the current pose.")]
        public float blendSeconds = 1f;

        [Tooltip("Keep the shot aimed at this actor, so it tracks them while they walk.")]
        public string lookAtActorKey;

        public bool letterbox = true;

        public override IEnumerator Execute(IMissionContext ctx)
        {
            Transform marker = RequireMarker(ctx, markerKey, nameof(CameraShotAction));
            if (marker == null || ctx.Camera == null)
                yield break;

            Transform lookAt = null;
            if (!string.IsNullOrEmpty(lookAtActorKey))
            {
                IMissionActor target = ctx.GetActor(lookAtActorKey);
                if (target != null) lookAt = target.Transform;
            }

            yield return ctx.Camera.ShotTo(marker, blendSeconds, lookAt, letterbox);
        }
    }
}
