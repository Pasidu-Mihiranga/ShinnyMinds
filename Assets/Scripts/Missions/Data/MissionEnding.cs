using System;
using UnityEngine;

namespace ShinyMinds.Missions.Data
{
    public enum EndingQuality { Unsafe, Safe, Best }

    [Serializable]
    public class MissionEnding
    {
        public string id;
        public EndingQuality quality = EndingQuality.Safe;
        public string title;
        [TextArea(2, 6)] public string lesson;

        [Tooltip("Badge image. Use a Sprite, not an emoji character — TMP's default " +
                 "LiberationSans atlas renders emoji as empty boxes.")]
        public Sprite badge;

        public AudioClip stinger;

        [Range(0, 3)] public int stars = 0;

        [Tooltip("Reaching this ending marks the whole mission complete.")]
        public bool completesMission = false;

        public bool allowRetry = true;
        [Tooltip("Offer a Continue button that returns the player to free roam.")]
        public bool allowContinue = false;
    }
}
