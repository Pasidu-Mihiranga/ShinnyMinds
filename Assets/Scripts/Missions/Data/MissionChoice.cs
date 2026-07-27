using System;
using UnityEngine;

namespace ShinyMinds.Missions.Data
{
    /// <summary>
    /// How safe an option is. Recorded for progress reporting only — it never gates
    /// anything, so a child is free to explore every branch.
    /// </summary>
    public enum MissionChoiceTone { Unsafe, Neutral, Safe, Safest }

    [Serializable]
    public class MissionChoice
    {
        [TextArea(1, 2)] public string label;
        [Tooltip("Node to jump to when this option is picked.")]
        public string nextId;
        public MissionChoiceTone tone = MissionChoiceTone.Neutral;
    }
}
