using System;
using System.Collections;
using UnityEngine;

namespace ShinyMinds.Missions.Data
{
    [Serializable]
    public class SetObjectiveAction : CutsceneAction
    {
        [Tooltip("Objective shown in the HUD. Empty hides the HUD.")]
        [TextArea(1, 2)] public string text;

        public override IEnumerator Execute(IMissionContext ctx)
        {
            ctx.Ui?.SetObjective(text);
            yield break;
        }
    }
}
