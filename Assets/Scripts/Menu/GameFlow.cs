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

        public static void SelectMission(string code, string topic, string title)
        {
            SelectedMissionCode = code;
            SelectedMissionTopic = topic;
            SelectedMissionTitle = title;
        }

        public static void ClearSelection()
        {
            SelectedMissionCode = null;
            SelectedMissionTopic = null;
            SelectedMissionTitle = null;
        }

        public static void LoadGameplay()
        {
            SceneManager.LoadScene(GameplayScene);
        }

        public static void LoadMainMenu()
        {
            SceneManager.LoadScene(MainMenuScene);
        }
    }
}
