using System;
using System.Collections;
using UnityEngine;

namespace ShinyMinds.Missions.Data
{
    [Serializable]
    public class FaceAction : CutsceneAction
    {
        public string actorKey;

        [Tooltip("Face this actor. Takes priority over targetMarkerKey.")]
        public string targetActorKey;
        public string targetMarkerKey;

        public float seconds = 0.5f;
        [Tooltip("Snap instantly instead of turning.")]
        public bool instant = false;

        public override IEnumerator Execute(IMissionContext ctx)
        {
            IMissionActor actor = RequireActor(ctx, actorKey, nameof(FaceAction));
            if (actor == null || actor.Mover == null)
                yield break;

            Vector3 targetPoint;

            if (!string.IsNullOrEmpty(targetActorKey))
            {
                IMissionActor target = RequireActor(ctx, targetActorKey, nameof(FaceAction));
                if (target == null) yield break;
                targetPoint = target.Transform.position;
            }
            else
            {
                Transform marker = RequireMarker(ctx, targetMarkerKey, nameof(FaceAction));
                if (marker == null) yield break;
                targetPoint = marker.position;
            }

            yield return actor.Mover.FaceTowards(targetPoint, instant ? 0f : seconds);
        }
    }
}
