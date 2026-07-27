using System;
using System.Collections;
using UnityEngine;

namespace ShinyMinds.Missions.Data
{
    /// <summary>
    /// Blocks until the player walks into range of a marker. This is how a free-roam
    /// segment ends without needing a trigger collider in the scene.
    /// </summary>
    [Serializable]
    public class WaitForPlayerAtMarkerAction : CutsceneAction
    {
        public string markerKey;
        public float radius = 3f;

        [Tooltip("Ignore height when measuring. Leave on — the city has kerbs and slopes.")]
        public bool ignoreY = true;

        [Tooltip("Safety valve in seconds. 0 = wait forever. Prevents a soft-lock if " +
                 "the player cannot reach the marker.")]
        public float timeoutSeconds = 0f;

        public override IEnumerator Execute(IMissionContext ctx)
        {
            Transform marker = RequireMarker(ctx, markerKey, nameof(WaitForPlayerAtMarkerAction));
            Transform player = ctx.Player;

            if (marker == null || player == null)
                yield break;

            float elapsed = 0f;
            float sqrRadius = radius * radius;

            while (true)
            {
                Vector3 delta = player.position - marker.position;
                if (ignoreY) delta.y = 0f;

                if (delta.sqrMagnitude <= sqrRadius)
                    yield break;

                if (timeoutSeconds > 0f)
                {
                    elapsed += Time.deltaTime;
                    if (elapsed >= timeoutSeconds)
                    {
                        Debug.LogWarning($"WaitForPlayerAtMarkerAction: timed out waiting for the player at '{markerKey}'.");
                        yield break;
                    }
                }

                yield return null;
            }
        }
    }
}
