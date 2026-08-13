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
            // The menu is the only way in. Neither a chosen mission nor an open-world
            // request means SampleScene was opened directly, so hand the player back
            // rather than dropping them into a city with no session and no progress.
            // Deliberately done here and not in the sceneLoaded callback above: loading
            // a scene from inside Unity's own scene-load event is asking for trouble.
            if (!GameFlow.HasEntryPoint)
            {
                Debug.Log("[ShinyMinds] Gameplay entered without the menu - returning to it.");

                GameFlow.LoadMainMenu();

                return;
            }

            if (GameFlow.HasSelection)
            {
                Debug.Log(
                    $"[ShinyMinds] Playing \"{GameFlow.SelectedMissionTitle}\" " +
                    $"(code {GameFlow.SelectedMissionCode}, topic \"{GameFlow.SelectedMissionTopic}\").");
            }
            else
            {
                Debug.Log("[ShinyMinds] Roaming the city - no mission selected, MissionTrigger offers them in world.");
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
