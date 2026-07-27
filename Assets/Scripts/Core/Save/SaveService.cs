using System;
using System.IO;
using Newtonsoft.Json;
using ShinyMinds.Missions.Data;
using UnityEngine;

namespace ShinyMinds.Core.Save
{
    /// <summary>
    /// Per-child mission progress, written as JSON to persistentDataPath.
    ///
    /// Newtonsoft is already a project dependency (GroqDialogue uses it) and
    /// ElevenLabsTTS already writes to persistentDataPath, so this adds no new
    /// packages or platform assumptions. The shape is deliberately simple because
    /// the parent dashboard will eventually read it.
    /// </summary>
    public static class SaveService
    {
        const string FileName = "shinyminds_save.json";

        static SaveFile cached;

        public static string FilePath => Path.Combine(Application.persistentDataPath, FileName);

        public static SaveFile Load()
        {
            if (cached != null)
                return cached;

            try
            {
                if (File.Exists(FilePath))
                {
                    string json = File.ReadAllText(FilePath);
                    cached = JsonConvert.DeserializeObject<SaveFile>(json) ?? new SaveFile();
                }
                else
                {
                    cached = new SaveFile();
                }
            }
            catch (Exception ex)
            {
                // A corrupt save must never block play for a child.
                Debug.LogError($"SaveService: could not read '{FilePath}'. Starting fresh. {ex.Message}");
                cached = new SaveFile();
            }

            return cached;
        }

        public static void Save()
        {
            if (cached == null)
                return;

            try
            {
                File.WriteAllText(FilePath, JsonConvert.SerializeObject(cached, Formatting.Indented));
            }
            catch (Exception ex)
            {
                Debug.LogError($"SaveService: could not write '{FilePath}'. {ex.Message}");
            }
        }

        public static MissionProgress GetProgress(string missionId)
        {
            if (string.IsNullOrEmpty(missionId))
                return null;

            SaveFile file = Load();

            foreach (MissionProgress p in file.missions)
                if (p.missionId == missionId) return p;

            var created = new MissionProgress { missionId = missionId };
            file.missions.Add(created);
            return created;
        }

        /// <summary>
        /// Records an attempt. Attempts always increment; stars and bestEndingId only
        /// improve, so replaying and picking a worse branch never erases a good result.
        /// </summary>
        public static void RecordEnding(string missionId, MissionEnding ending)
        {
            if (string.IsNullOrEmpty(missionId) || ending == null)
                return;

            MissionProgress p = GetProgress(missionId);

            p.attempts++;
            p.lastPlayedUtc = DateTime.UtcNow.ToString("o");
            p.completed |= ending.completesMission;

            if (ending.stars > p.bestStars || string.IsNullOrEmpty(p.bestEndingId))
            {
                p.bestStars = Mathf.Max(p.bestStars, ending.stars);
                p.bestEndingId = ending.id;
            }

            Save();
        }

        /// <summary>Test helper. Drops the in-memory copy so the next Load re-reads disk.</summary>
        public static void ClearCache() => cached = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => cached = null;
    }
}
