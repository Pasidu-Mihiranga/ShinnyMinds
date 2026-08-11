using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShinyMinds.Missions.Data
{
    public enum MissionNodeKind
    {
        /// <summary>Someone speaks. Shown in the dialogue panel.</summary>
        Line,
        /// <summary>Internal monologue. Shown in the thought bubble panel.</summary>
        Thought,
        /// <summary>Runs an ordered list of CutsceneActions.</summary>
        Cutscene,
        /// <summary>The player picks an option with the mouse.</summary>
        Choice,
        /// <summary>Shows the ending card, then retries or returns to free roam.</summary>
        Ending,
        /// <summary>
        /// A line the speaker is remembering, not saying now. Shown inside the memory
        /// bubble, acted out by the stand-ins on the memory stage. Consecutive Memory
        /// nodes share one bubble; it opens on the first and closes after the last.
        ///
        /// Appended rather than slotted next to Thought on purpose — the enum is
        /// serialized by value, and inserting would renumber every mission asset.
        /// </summary>
        Memory
    }

    [Serializable]
    public class MissionNode
    {
        [Tooltip("Unique within this mission. Referenced by nextId and by choices.")]
        public string id;

        public MissionNodeKind kind = MissionNodeKind.Line;

        [Header("Line / Thought / Memory")]
        [Tooltip("Key into MissionData.speakers.")]
        public string speakerKey;
        [TextArea(2, 5)] public string text;
        [Tooltip("0 = wait for E. Greater than 0 = auto-advance after N seconds.")]
        public float autoAdvanceSeconds = 0f;
        [Tooltip("Speak this line with ElevenLabs TTS. Off by default — the TTS path " +
                 "builds its JSON by string concatenation and races on a single file.")]
        public bool speakAloud = false;

        [Header("Cutscene")]
        [SerializeReference] public List<CutsceneAction> actions = new List<CutsceneAction>();

        [Header("Choice")]
        [TextArea(1, 2)] public string prompt;
        public List<MissionChoice> choices = new List<MissionChoice>();

        [Header("Ending")]
        public string endingId;

        [Header("Flow")]
        [Tooltip("Next node. Empty = the mission stops here. Ignored for Choice nodes " +
                 "unless no choice was picked.")]
        public string nextId;
    }
}
