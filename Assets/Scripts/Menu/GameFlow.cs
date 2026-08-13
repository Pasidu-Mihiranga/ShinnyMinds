using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShinyMinds.Menu
{
    /// <summary>
    /// Carries the player's menu choice into the gameplay scene.
    ///
    /// Scene loads destroy everything not marked DontDestroyOnLoad, so the selected
    /// mission is held in static fields that the gameplay scene reads on start.
    /// </summary>
    public static class GameFlow
    {
        public const string MainMenuScene = "MainMenu";
        public const string GameplayScene = "SampleScene";

        /// <summary>Mission code the player chose, e.g. "road_crossing".</summary>
        public static string SelectedMissionCode { get; private set; }

        /// <summary>
        /// NPC topic for the chosen mission. GroqDialogue components read this so the
        /// conversation matches the mission, instead of each NPC hard-coding a topic.
        /// </summary>
        public static string SelectedMissionTopic { get; private set; }

        public static string SelectedMissionTitle { get; private set; }

        public static bool HasSelection => !string.IsNullOrEmpty(SelectedMissionCode);

        /// <summary>
        /// Set when the player asked for the open city rather than a named mission.
        /// GameplayBootstrap sends an entry with neither back to the menu, so roaming
        /// has to announce itself instead of looking like a stray scene load.
        /// </summary>
        public static bool OpenWorldRequested { get; private set; }

        /// <summary>True when the player reached gameplay through the menu.</summary>
        public static bool HasEntryPoint => HasSelection || OpenWorldRequested;

        /// <summary>
        /// Node the player asked to resume at, and the local mission id it belongs to.
        /// Both null for a fresh start.
        ///
        /// The id is carried alongside the node because the city can hold more than one
        /// mission zone, and each has to be able to tell whether the checkpoint waiting
        /// here is one of its own.
        /// </summary>
        public static string ResumeMissionId { get; private set; }

        public static string ResumeNodeId { get; private set; }

        /// <param name="resumeMissionId">Local mission id to resume, or null to start fresh.</param>
        /// <param name="resumeNodeId">Checkpoint node within that mission.</param>
        public static void SelectMission(string code, string topic, string title,
                                         string resumeMissionId = null, string resumeNodeId = null)
        {
            SelectedMissionCode = code;
            SelectedMissionTopic = topic;
            SelectedMissionTitle = title;
            OpenWorldRequested = false;

            ResumeMissionId = resumeMissionId;
            ResumeNodeId = resumeNodeId;
        }

        /// <summary>
        /// True when a resume was requested for this specific mission. Reading it is
        /// destructive: the checkpoint is a one-shot instruction to skip the offer and
        /// pick up where the player left off, and leaving it set would re-resume every
        /// time they walked back into the zone for the rest of the session.
        /// </summary>
        public static bool ConsumeResume(string missionId, out string nodeId)
        {
            nodeId = null;

            if (string.IsNullOrEmpty(missionId)
                || missionId != ResumeMissionId
                || string.IsNullOrEmpty(ResumeNodeId))
            {
                return false;
            }

            nodeId = ResumeNodeId;

            ResumeMissionId = null;
            ResumeNodeId = null;

            return true;
        }

        public static void ClearSelection()
        {
            SelectedMissionCode = null;
            SelectedMissionTopic = null;
            SelectedMissionTitle = null;
            OpenWorldRequested = false;

            ResumeMissionId = null;
            ResumeNodeId = null;
        }

        /// <summary>
        /// Enters the city with no mission chosen. The scene deliberately leaves
        /// autoStartMission empty, so MissionTrigger offers each mission as the player
        /// reaches it and nothing seizes control on arrival.
        /// </summary>
        public static void EnterOpenWorld()
        {
            ClearSelection();

            OpenWorldRequested = true;

            LoadGameplay();
        }

        public static void LoadGameplay()
        {
            SceneManager.LoadScene(GameplayScene);
        }

        public static void LoadMainMenu()
        {
            SceneManager.LoadScene(MainMenuScene);
        }

        // "Enter Play Mode Options -> Disable Domain Reload" keeps statics between
        // sessions. A stale OpenWorldRequested would let SampleScene run without the
        // menu, which is exactly what the redirect in GameplayBootstrap prevents.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            ClearSelection();
        }
    }
}
