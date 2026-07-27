using System;
using System.Collections;
using UnityEngine;

namespace ShinyMinds.Missions.Data
{
    [Serializable]
    public class MoveActorAction : CutsceneAction
    {
        public string actorKey;
        public string markerKey;
        public bool run = false;

        [Tooltip("Walk backwards while still facing the original direction. " +
                 "Used for Aisha's step back in Path B.")]
        public bool walkBackwards = false;

        [Tooltip("Turn to face this actor on arrival. Leave empty to face the marker's forward.")]
        public string faceActorKeyOnArrive;

        public float faceSeconds = 0.35f;

        public override IEnumerator Execute(IMissionContext ctx)
        {
            IMissionActor actor = RequireActor(ctx, actorKey, nameof(MoveActorAction));
            Transform marker = RequireMarker(ctx, markerKey, nameof(MoveActorAction));

            if (actor == null || marker == null || actor.Mover == null)
                yield break;

            yield return actor.Mover.MoveTo(marker.position, run, walkBackwards);

            if (!string.IsNullOrEmpty(faceActorKeyOnArrive))
            {
                IMissionActor target = ctx.GetActor(faceActorKeyOnArrive);
                if (target != null)
                    yield return actor.Mover.FaceTowards(target.Transform.position, faceSeconds);
            }
            else
            {
                yield return actor.Mover.FaceDirection(marker.forward, faceSeconds);
            }
        }
    }
}
