using System;
using System.Collections;
using UnityEngine;

namespace ShinyMinds.Missions.Data
{
    [Serializable]
    public class SetActorActiveAction : CutsceneAction
    {
        public string actorKey;
        public bool active = true;

        public override IEnumerator Execute(IMissionContext ctx)
        {
            IMissionActor actor = RequireActor(ctx, actorKey, nameof(SetActorActiveAction));

            if (actor != null && actor.GameObject != null)
                actor.GameObject.SetActive(active);

            yield break;
        }
    }
}
