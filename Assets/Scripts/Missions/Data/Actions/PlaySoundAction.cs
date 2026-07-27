using System;
using System.Collections;
using UnityEngine;

namespace ShinyMinds.Missions.Data
{
    [Serializable]
    public class PlaySoundAction : CutsceneAction
    {
        public AudioClip clip;
        [Range(0f, 1f)] public float volume = 1f;

        [Tooltip("Play positioned at this marker. Leave empty for a flat 2D sound.")]
        public string atMarkerKey;

        [Tooltip("Block until the clip finishes.")]
        public bool waitForEnd = false;

        public override IEnumerator Execute(IMissionContext ctx)
        {
            if (clip == null)
            {
                // Expected while audio is still being sourced — warn, don't stall the mission.
                Debug.LogWarning("PlaySoundAction: no clip assigned.");
                yield break;
            }

            if (!string.IsNullOrEmpty(atMarkerKey))
            {
                Transform marker = RequireMarker(ctx, atMarkerKey, nameof(PlaySoundAction));
                if (marker != null)
                    AudioSource.PlayClipAtPoint(clip, marker.position, volume);
            }
            else if (ctx.Sfx != null)
            {
                ctx.Sfx.PlayOneShot(clip, volume);
            }

            if (waitForEnd)
                yield return new WaitForSeconds(clip.length);
        }
    }
}
