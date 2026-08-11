using System;
using UnityEngine;

namespace ShinyMinds.Missions.Data
{
    /// <summary>
    /// Which stand-in on the memory stage a remembered line belongs to, which is also
    /// the side of the memory bubble its speech balloon appears on.
    /// </summary>
    public enum MemorySide
    {
        None,
        Left,
        Right
    }

    [Serializable]
    public class SpeakerProfile
    {
        [Tooltip("Referenced by MissionNode.speakerKey. e.g. aisha, stranger, teacher, mother, narrator.")]
        public string key;

        [Tooltip("Shown above the line. Leave empty for a narrator.")]
        public string displayName;

        public Color nameColor = Color.white;

        [Tooltip("Scene actor to animate while this speaker talks. Leave empty for a " +
                 "narrator or a remembered voice with no body in the world.")]
        public string actorKey;

        [Tooltip("Only used when a node opts into speakAloud.")]
        public string elevenLabsVoiceId;

        [Tooltip("Only read for Memory nodes: which stand-in on the memory stage speaks, " +
                 "and which side of the memory bubble the balloon appears on.")]
        public MemorySide memorySide = MemorySide.None;
    }
}
