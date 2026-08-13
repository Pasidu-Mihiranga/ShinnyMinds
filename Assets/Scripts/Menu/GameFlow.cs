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

        public static void SelectMission(string code, string topic, string title)
        {
            SelectedMissionCode = code;
            SelectedMissionTopic = topic;
            SelectedMissionTitle = title;
            OpenWorldRequested = false;
        }

        public static void ClearSelection()
        {
            SelectedMissionCode = null;
            SelectedMissionTopic = null;
            SelectedMissionTitle = null;
            OpenWorldRequested = false;
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
