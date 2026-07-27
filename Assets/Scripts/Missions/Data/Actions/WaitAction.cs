using System;
using System.Collections;
using UnityEngine;

namespace ShinyMinds.Missions.Data
{
    [Serializable]
    public class WaitAction : CutsceneAction
    {
        public float seconds = 1f;

        public override IEnumerator Execute(IMissionContext ctx)
        {
            if (seconds > 0f)
                yield return new WaitForSeconds(seconds);
        }
    }
}
