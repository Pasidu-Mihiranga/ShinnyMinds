using System;
using System.Collections;
using UnityEngine;

namespace ShinyMinds.Missions.Data
{
    [Serializable]
    public class TeleportActorAction : CutsceneAction
    {
        public string actorKey;
        public string markerKey;

        [Tooltip("Raycast down after moving so the feet sit on the ground. " +
                 "PlayerController only snaps to ground in Start(), so jump-cuts need this.")]
        public bool snapToGround = true;

        public override IEnumerator Execute(IMissionContext ctx)
        {
            IMissionActor actor = RequireActor(ctx, actorKey, nameof(TeleportActorAction));
            Transform marker = RequireMarker(ctx, markerKey, nameof(TeleportActorAction));

            if (actor == null || marker == null)
                yield break;

            actor.TeleportTo(marker.position, marker.rotation, snapToGround);
            yield break;
        }
    }
}
