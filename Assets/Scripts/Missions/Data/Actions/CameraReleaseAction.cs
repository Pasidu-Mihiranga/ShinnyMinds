using System;
using System.Collections;

namespace ShinyMinds.Missions.Data
{
    [Serializable]
    public class CameraReleaseAction : CutsceneAction
    {
        public float blendSeconds = 0.8f;

        public override IEnumerator Execute(IMissionContext ctx)
        {
            if (ctx.Camera == null)
                yield break;

            yield return ctx.Camera.Release(blendSeconds);
        }
    }
}
