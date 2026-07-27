using System;
using System.Collections;
using UnityEngine;

namespace ShinyMinds.Missions.Data
{
    /// <summary>
    /// Records a boolean on the running mission. Unused by Mission 01, but future
    /// missions need somewhere to remember "the player already picked up the key".
    /// Flags are cleared on retry and when a mission starts.
    /// </summary>
    [Serializable]
    public class SetFlagAction : CutsceneAction
    {
        public string flagKey;
        public bool value = true;

        public override IEnumerator Execute(IMissionContext ctx)
        {
            ctx.SetFlag(flagKey, value);
            yield break;
        }
    }
}
