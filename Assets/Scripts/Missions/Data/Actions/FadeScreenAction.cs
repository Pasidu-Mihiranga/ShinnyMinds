using System;
using System.Collections;
using UnityEngine;

namespace ShinyMinds.Missions.Data
{
    [Serializable]
    public class FadeScreenAction : CutsceneAction
    {
        [Tooltip("True fades to black, false fades back in.")]
        public bool toBlack = true;
        public float seconds = 1f;

        [Tooltip("Hold at full black after the fade completes.")]
        public float holdSeconds = 0f;

        public override IEnumerator Execute(IMissionContext ctx)
        {
            if (ctx.Ui == null)
                yield break;

            yield return ctx.Ui.Fade(toBlack, seconds, holdSeconds);
        }
    }
}
