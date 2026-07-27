using UnityEngine;

namespace ShinyMinds.Missions.Runtime
{
    /// <summary>
    /// A named position and facing for mission staging: where an actor walks to,
    /// or where a cutscene camera sits.
    ///
    /// Never name these objects "Waypoint …" — CarAI.waypoints is a serialized
    /// Transform[] and the hierarchy already returns ~325 hits for that string, so a
    /// mis-drag would silently send a car to a cutscene mark.
    /// </summary>
    [DisallowMultipleComponent]
    public class MissionMarker : MonoBehaviour
    {
        [SerializeField] string markerKey;

        [Header("Gizmo")]
        [SerializeField] float gizmoRadius = 0.4f;
        [SerializeField] Color gizmoColor = new Color(0.2f, 0.9f, 1f, 0.9f);

        bool registered;

        public string MarkerKey => markerKey;

        void Awake() => EnsureRegistered();
        void OnDestroy() => MissionSceneRegistry.UnregisterMarker(markerKey, transform);

        /// <summary>
        /// Also called by MissionStagingRoot, so markers parented under an inactive
        /// object still resolve. See MissionStagingRoot for why.
        /// </summary>
        public void EnsureRegistered()
        {
            if (registered) return;

            registered = true;
            MissionSceneRegistry.RegisterMarker(markerKey, transform);
        }

        void OnDrawGizmos()
        {
            Gizmos.color = gizmoColor;
            Gizmos.DrawWireSphere(transform.position, gizmoRadius);
            Gizmos.DrawLine(transform.position, transform.position + transform.forward * gizmoRadius * 3f);

#if UNITY_EDITOR
            string label = string.IsNullOrEmpty(markerKey) ? "(no key!)" : markerKey;
            UnityEditor.Handles.color = gizmoColor;
            UnityEditor.Handles.Label(transform.position + Vector3.up * (gizmoRadius + 0.2f), label);
#endif
        }
    }
}
