using ShinyMinds.Missions.Data;
using UnityEngine;

namespace ShinyMinds.Missions.Runtime
{
    /// <summary>
    /// Where a mission's pre-generated dialogue audio lives, and how it is named.
    ///
    /// Mission dialogue is fixed text, so every line is generated once by the editor
    /// baker and shipped with the game. Players never call ElevenLabs: there is no
    /// per-line latency, no API spend per playthrough, and a mission plays offline.
    ///
    /// Shared by the baker and the runtime so the two can never disagree about a path.
    /// </summary>
    public static class MissionVoiceBank
    {
        /// <summary>Under Assets/Resources, so the clips load without any scene wiring.</summary>
        public const string ResourceRoot = "MissionVoice";

        /// <summary>
        /// Clip name for a node: its id plus a hash of the text it was generated from.
        ///
        /// The hash is what makes an edited line safe. Change the words and the name
        /// changes, so the runtime finds no clip and falls back to live TTS rather than
        /// confidently playing audio of the old sentence. Re-baking then produces the
        /// new name and the fallback stops being used.
        /// </summary>
        public static string ClipName(MissionNode node)
        {
            return node == null
                ? null
                : $"{node.id}_{HashText(node.text)}";
        }

        /// <summary>Resources path for a node's clip, without a file extension.</summary>
        public static string ResourcePath(string missionId, MissionNode node)
        {
            string clip = ClipName(node);

            return string.IsNullOrEmpty(missionId) || string.IsNullOrEmpty(clip)
                ? null
                : $"{ResourceRoot}/{missionId}/{clip}";
        }

        /// <summary>The baked clip for a node, or null when it has not been baked.</summary>
        public static AudioClip Load(string missionId, MissionNode node)
        {
            string path = ResourcePath(missionId, node);

            return string.IsNullOrEmpty(path)
                ? null
                : Resources.Load<AudioClip>(path);
        }

        /// <summary>
        /// FNV-1a. Deliberately not string.GetHashCode, which is seeded per process on
        /// modern runtimes — a name that changed between the bake and the build would
        /// miss every clip.
        /// </summary>
        public static string HashText(string text)
        {
            unchecked
            {
                const uint offset = 2166136261;
                const uint prime = 16777619;

                uint hash = offset;

                if (!string.IsNullOrEmpty(text))
                {
                    // Normalised so a line that only gained trailing whitespace, or had its
                    // newlines rewritten by a merge, does not silently invalidate its clip.
                    string normalised = text.Replace("\r\n", "\n").Trim();

                    for (int i = 0; i < normalised.Length; i++)
                    {
                        hash ^= normalised[i];
                        hash *= prime;
                    }
                }

                return hash.ToString("x8");
            }
        }
    }
}
