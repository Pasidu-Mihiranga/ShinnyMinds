using System;
using System.Collections;

namespace ShinyMinds.Missions.Data
{
    [Serializable]
    public class LetterboxAction : CutsceneAction
    {
        public bool on = true;
        public float seconds = 0.4f;

        public override IEnumerator Execute(IMissionContext ctx)
        {
            if (ctx.Ui == null)
                yield break;

            yield return ctx.Ui.SetLetterbox(on, seconds);
        }
    }
}
