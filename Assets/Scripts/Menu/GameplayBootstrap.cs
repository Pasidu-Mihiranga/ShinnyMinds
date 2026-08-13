using ShinyMinds.Progress;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShinyMinds.Menu
{
    /// <summary>
    /// Wires the gameplay scene to the menu and the progress tracker.
    ///
    /// This installs itself from a runtime hook rather than being dropped into
    /// SampleScene.unity. That scene is a 17k-line YAML file shared by everyone on the
    /// project, and adding a component to it would be another merge conflict every time
    /// this behaviour changes.
    /// </summary>
    public class GameplayBootstrap : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Install()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != GameFlow.GameplayScene)
            {
                return;
            }

            GameObject host = new GameObject("ShinyMinds.GameplayBootstrap");

            host.AddComponent<GameplayBootstrap>();
        }

        private void Start()
        {
            if (GameFlow.HasSelection)
            {
                Debug.Log(
                    $"[ShinyMinds] Playing \"{GameFlow.SelectedMissionTitle}\" " +
                    $"(code {GameFlow.SelectedMissionCode}, topic \"{GameFlow.SelectedMissionTopic}\").");
            }
            else
            {
                // Pressing Play directly on SampleScene is normal while building levels.
                Debug.Log("[ShinyMinds] Gameplay scene entered directly - no mission selected, progress is not recorded.");
            }
        }

        private void Update()
        {
            // Escape is the way back to the menu. Leaving mid-mission records the attempt
            // as abandoned rather than discarding it, so time played still reaches the
            // parent dashboard.
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                ReturnToMenu();
            }
        }

        /// <summary>Ends the current mission and returns to the main menu.</summary>
        public static void ReturnToMenu(bool missionCompleted = false)
        {
            GameProgressTracker tracker = GameProgressTracker.Instance;

            if (tracker.HasActiveMission)
            {
                tracker.CompleteMission(!missionCompleted, _ => GameFlow.LoadMainMenu());

                return;
            }

            GameFlow.LoadMainMenu();
        }
    }
}
