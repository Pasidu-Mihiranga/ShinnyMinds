using System;
using System.Collections;
using ShinyMinds.Core;
using UnityEngine;

namespace ShinyMinds.Missions.Data
{
    [Serializable]
    public class SetPlayerControlAction : CutsceneAction
    {
        [Tooltip("True hands control back to the player, false freezes them for a cutscene.")]
        public bool allowControl = false;

        public override IEnumerator Execute(IMissionContext ctx)
        {
            // The context object (the MissionRunner) is the lock owner, so a Line node
            // acquiring the same lock later is refcounted rather than conflicting.
            if (allowControl)
                PlayerInputLock.Release(ctx);
            else
                PlayerInputLock.Acquire(ctx);

            yield break;
        }
    }
}
