using System.Collections.Generic;
using UnityEngine;

namespace ShinyMinds.Missions.Data
{
    /// <summary>
    /// One mission, authored entirely as data. Adding a mission means adding an
    /// asset, not adding code.
    ///
    /// The story graph is a flat list of nodes addressed by string id rather than a
    /// polymorphic tree: ids survive renames and reordering, read well in a diff,
    /// and cannot be nulled by a class rename the way [SerializeReference] can.
    /// Only the cutscene action lists use [SerializeReference], where the variation
    /// really is behavioural.
    /// </summary>
    [CreateAssetMenu(menuName = "ShinyMinds/Mission Data", fileName = "Mission_New")]
    public class MissionData : ScriptableObject
    {
        [Header("Identity")]
        public string missionId = "mission_01_road_home";
        public string title = "The Road Home";
        [TextArea] public string objective = "Walk home from school.";

        [Header("Graph")]
        public string startNodeId = "s1_open";
        public List<MissionNode> nodes = new List<MissionNode>();

        [Header("Outcomes")]
        public List<MissionEnding> endings = new List<MissionEnding>();

        [Header("Cast")]
        public List<SpeakerProfile> speakers = new List<SpeakerProfile>();

        Dictionary<string, MissionNode> nodeLookup;
        Dictionary<string, MissionEnding> endingLookup;
        Dictionary<string, SpeakerProfile> speakerLookup;

        public MissionNode GetNode(string id)
        {
            if (nodeLookup == null) BuildLookups();
            if (string.IsNullOrEmpty(id)) return null;
            return nodeLookup.TryGetValue(id, out MissionNode n) ? n : null;
        }

        public MissionEnding GetEnding(string id)
        {
            if (endingLookup == null) BuildLookups();
            if (string.IsNullOrEmpty(id)) return null;
            return endingLookup.TryGetValue(id, out MissionEnding e) ? e : null;
        }

        public SpeakerProfile GetSpeaker(string key)
        {
            if (speakerLookup == null) BuildLookups();
            if (string.IsNullOrEmpty(key)) return null;
            return speakerLookup.TryGetValue(key, out SpeakerProfile s) ? s : null;
        }

        void BuildLookups()
        {
            nodeLookup = new Dictionary<string, MissionNode>(nodes.Count);
            endingLookup = new Dictionary<string, MissionEnding>(endings.Count);
            speakerLookup = new Dictionary<string, SpeakerProfile>(speakers.Count);

            foreach (MissionNode n in nodes)
                if (n != null && !string.IsNullOrEmpty(n.id)) nodeLookup[n.id] = n;

            foreach (MissionEnding e in endings)
                if (e != null && !string.IsNullOrEmpty(e.id)) endingLookup[e.id] = e;

            foreach (SpeakerProfile s in speakers)
                if (s != null && !string.IsNullOrEmpty(s.key)) speakerLookup[s.key] = s;
        }

        // Invalidate so edits made while in Play mode take effect immediately.
        void OnValidate()
        {
            nodeLookup = null;
            endingLookup = null;
            speakerLookup = null;
        }

        /// <summary>
        /// Reports duplicate ids and dangling links. Called by the runner on Begin()
        /// and available from the asset's context menu.
        /// </summary>
        [ContextMenu("Validate Graph")]
        public void ValidateGraph()
        {
            var seen = new HashSet<string>();
            int problems = 0;

            foreach (MissionNode n in nodes)
            {
                if (n == null) continue;

                if (string.IsNullOrEmpty(n.id))
                {
                    Debug.LogError($"[{missionId}] A node has an empty id.", this);
                    problems++;
                    continue;
                }

                if (!seen.Add(n.id))
                {
                    Debug.LogError($"[{missionId}] Duplicate node id '{n.id}'.", this);
                    problems++;
                }

                if (!string.IsNullOrEmpty(n.nextId) && GetNode(n.nextId) == null)
                {
                    Debug.LogError($"[{missionId}] Node '{n.id}' points to missing nextId '{n.nextId}'.", this);
                    problems++;
                }

                if (n.kind == MissionNodeKind.Choice)
                {
                    foreach (MissionChoice c in n.choices)
                    {
                        if (c != null && !string.IsNullOrEmpty(c.nextId) && GetNode(c.nextId) == null)
                        {
                            Debug.LogError($"[{missionId}] Choice '{c.label}' on '{n.id}' points to missing node '{c.nextId}'.", this);
                            problems++;
                        }
                    }
                }

                if (n.kind == MissionNodeKind.Ending && GetEnding(n.endingId) == null)
                {
                    Debug.LogError($"[{missionId}] Node '{n.id}' points to missing ending '{n.endingId}'.", this);
                    problems++;
                }

                if ((n.kind == MissionNodeKind.Line || n.kind == MissionNodeKind.Thought)
                    && !string.IsNullOrEmpty(n.speakerKey) && GetSpeaker(n.speakerKey) == null)
                {
                    Debug.LogWarning($"[{missionId}] Node '{n.id}' uses unknown speaker '{n.speakerKey}'.", this);
                }
            }

            if (GetNode(startNodeId) == null)
            {
                Debug.LogError($"[{missionId}] startNodeId '{startNodeId}' does not exist.", this);
                problems++;
            }

            if (problems == 0)
                Debug.Log($"[{missionId}] Graph OK — {nodes.Count} nodes, {endings.Count} endings.", this);
        }
    }
}
