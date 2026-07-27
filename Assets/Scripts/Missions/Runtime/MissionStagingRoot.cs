using UnityEngine;

namespace ShinyMinds.Missions.Runtime
{
    /// <summary>
    /// Registers mission actors and markers that start out INACTIVE.
    ///
    /// Unity never calls Awake on a component of an inactive GameObject, so an actor
    /// that a mission activates part-way through (the Stranger, the Mother) would
    /// never register itself and every action targeting it would silently no-op.
    /// This walks the hierarchy including inactive children and initialises them.
    ///
    /// Put one on the mission's staging root (e.g. Mission01_Staging), which must
    /// itself be active.
    /// </summary>
    [DefaultExecutionOrder(-200)]   // before MissionRunner (-100) auto-starts
    public class MissionStagingRoot : MonoBehaviour
    {
        [Tooltip("Also scan actors/markers outside this hierarchy that start inactive. " +
                 "Slower — only needed if staging is split across the scene.")]
        [SerializeField] bool scanWholeScene = false;

        void Awake()
        {
            if (scanWholeScene)
            {
                foreach (MissionActor actor in FindObjectsByType<MissionActor>(FindObjectsInactive.Include))
                    actor.EnsureInitialised();

                foreach (MissionMarker marker in FindObjectsByType<MissionMarker>(FindObjectsInactive.Include))
                    marker.EnsureRegistered();

                return;
            }

            foreach (MissionActor actor in GetComponentsInChildren<MissionActor>(true))
                actor.EnsureInitialised();

            foreach (MissionMarker marker in GetComponentsInChildren<MissionMarker>(true))
                marker.EnsureRegistered();
        }
    }
}
