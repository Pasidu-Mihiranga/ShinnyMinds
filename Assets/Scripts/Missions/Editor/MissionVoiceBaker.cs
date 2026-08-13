using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ShinyMinds.Config;
using ShinyMinds.Missions.Data;
using ShinyMinds.Missions.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace ShinyMinds.Missions.EditorTools
{
    /// <summary>
    /// Generates every spoken line of a mission once and stores the audio in the project.
    ///
    /// Mission dialogue is fixed text, so generating it per playthrough spends API credit
    /// and adds a pause before each line for something that never changes. Baked clips
    /// ship with the game: no network at run time, no key on the player's machine, and
    /// the mission plays offline.
    ///
    /// Safe to re-run. A line whose clip already exists is skipped, so a bake after
    /// editing one sentence costs one request rather than the whole script.
    /// </summary>
    public static class MissionVoiceBaker
    {
        const string OutputRoot = "Assets/Resources/" + MissionVoiceBank.ResourceRoot;
        const string Model = "eleven_multilingual_v2";

        [MenuItem("ShinyMinds/Voice/Bake Mission Dialogue")]
        public static void BakeSelected()
        {
            MissionData mission = FindMission();

            if (mission == null)
            {
                EditorUtility.DisplayDialog(
                    "Bake Mission Dialogue",
                    "Select a MissionData asset in the Project window first.",
                    "OK");

                return;
            }

            Bake(mission, false);
        }

        [MenuItem("ShinyMinds/Voice/Rebake Mission Dialogue (overwrite)")]
        public static void RebakeSelected()
        {
            MissionData mission = FindMission();

            if (mission == null)
            {
                EditorUtility.DisplayDialog(
                    "Rebake Mission Dialogue",
                    "Select a MissionData asset in the Project window first.",
                    "OK");

                return;
            }

            bool go = EditorUtility.DisplayDialog(
                "Rebake Mission Dialogue",
                $"Regenerate every line of \"{mission.title}\"?\n\n" +
                "This calls ElevenLabs once per line and spends credit for audio you " +
                "already have. Use the plain Bake to fill in only what is missing.",
                "Rebake everything",
                "Cancel");

            if (go)
            {
                Bake(mission, true);
            }
        }

        /// <summary>The selected MissionData, or the only one in the project.</summary>
        static MissionData FindMission()
        {
            if (Selection.activeObject is MissionData selected)
            {
                return selected;
            }

            string[] found = AssetDatabase.FindAssets("t:MissionData");

            if (found.Length != 1)
            {
                return null;
            }

            return AssetDatabase.LoadAssetAtPath<MissionData>(
                AssetDatabase.GUIDToAssetPath(found[0]));
        }

        static void Bake(MissionData mission, bool overwrite)
        {
            string apiKey = GameConfig.ElevenLabsApiKey;

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                EditorUtility.DisplayDialog(
                    "Bake Mission Dialogue",
                    $"No {GameConfig.ElevenLabsApiKeyName} configured.\n\n" +
                    "Add it to .env in the repository root, then try again.",
                    "OK");

                return;
            }

            string folder = $"{OutputRoot}/{mission.missionId}";

            Directory.CreateDirectory(folder);

            List<MissionNode> spoken = SpokenNodes(mission);

            int written = 0;
            int skipped = 0;
            int failed = 0;

            try
            {
                for (int i = 0; i < spoken.Count; i++)
                {
                    MissionNode node = spoken[i];
                    string file = $"{folder}/{MissionVoiceBank.ClipName(node)}.mp3";

                    if (!overwrite && File.Exists(file))
                    {
                        skipped++;
                        continue;
                    }

                    bool cancelled = EditorUtility.DisplayCancelableProgressBar(
                        $"Baking \"{mission.title}\"",
                        $"{node.id}  ({i + 1} of {spoken.Count})",
                        (float)i / Mathf.Max(1, spoken.Count));

                    if (cancelled)
                    {
                        break;
                    }

                    string voiceId = VoiceFor(mission, node);

                    if (string.IsNullOrWhiteSpace(voiceId))
                    {
                        Debug.LogWarning($"[VoiceBaker] No voice for speaker '{node.speakerKey}' " +
                                         $"(node '{node.id}'). Set " +
                                         $"{GameConfig.VoiceIdNameFor(node.speakerKey)} in .env.");

                        failed++;
                        continue;
                    }

                    if (Generate(node.text, voiceId, apiKey, file))
                    {
                        written++;
                    }
                    else
                    {
                        failed++;
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            AssetDatabase.Refresh();

            Debug.Log($"[VoiceBaker] \"{mission.title}\": {written} generated, " +
                      $"{skipped} already present, {failed} failed. -> {folder}");
        }

        /// <summary>Nodes that put words on screen. Cutscenes, choices and endings have none.</summary>
        static List<MissionNode> SpokenNodes(MissionData mission)
        {
            var spoken = new List<MissionNode>();
            var seen = new HashSet<string>();

            foreach (MissionNode node in mission.nodes)
            {
                bool speaks = node.kind == MissionNodeKind.Line
                              || node.kind == MissionNodeKind.Thought
                              || node.kind == MissionNodeKind.Memory;

                if (!speaks || string.IsNullOrWhiteSpace(node.text))
                {
                    continue;
                }

                // Two nodes with the same id and text would write the same file twice.
                if (seen.Add(MissionVoiceBank.ClipName(node)))
                {
                    spoken.Add(node);
                }
            }

            return spoken;
        }

        /// <summary>
        /// Same casting rule the runner uses: the SpeakerProfile's own id wins, then
        /// ELEVENLABS_&lt;KEY&gt;_VOICE_ID, then the shared NPC voice.
        /// </summary>
        static string VoiceFor(MissionData mission, MissionNode node)
        {
            SpeakerProfile speaker = mission.GetSpeaker(node.speakerKey);

            return speaker != null && !string.IsNullOrWhiteSpace(speaker.elevenLabsVoiceId)
                ? speaker.elevenLabsVoiceId
                : GameConfig.VoiceIdForSpeaker(node.speakerKey);
        }

        /// <summary>
        /// One blocking request. The editor has no coroutines to yield from, so this spins
        /// until the request settles; the progress bar above is what keeps that visible.
        /// </summary>
        static bool Generate(string text, string voiceId, string apiKey, string file)
        {
            string json = new JObject(
                new JProperty("text", text),
                new JProperty("model_id", Model)
            ).ToString(Formatting.None);

            using (var request = new UnityWebRequest(
                       "https://api.elevenlabs.io/v1/text-to-speech/" + voiceId, "POST"))
            {
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("xi-api-key", apiKey);

                UnityWebRequestAsyncOperation op = request.SendWebRequest();

                while (!op.isDone)
                {
                    System.Threading.Thread.Sleep(20);
                }

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"[VoiceBaker] {request.error}\n{request.downloadHandler.text}");

                    return false;
                }

                File.WriteAllBytes(file, request.downloadHandler.data);

                return true;
            }
        }
    }
}
