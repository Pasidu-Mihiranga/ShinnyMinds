using ShinyMinds.Core;
using ShinyMinds.Missions.Data;
using UnityEngine;

namespace ShinyMinds.Missions.Runtime
{
    /// <summary>
    /// Starts a mission when the player walks in. Mission 01 auto-starts instead, but
    /// every mission after it will be entered this way.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class MissionTrigger : MonoBehaviour
    {
        [SerializeField] MissionData mission;
        [SerializeField] MissionRunner runner;
        [SerializeField] bool once = true;

        bool fired;

        void Reset()
        {
            Collider c = GetComponent<Collider>();
            if (c != null) c.isTrigger = true;
        }

        void OnTriggerEnter(Collider other)
        {
            if (fired && once) return;
            if (!other.CompareTag("Player")) return;

            // A Groq NPC conversation or another cutscene already owns the player.
            if (PlayerInputLock.IsLocked) return;

            if (runner == null || mission == null)
            {
                Debug.LogWarning("MissionTrigger: runner or mission not assigned.", this);
                return;
            }

            if (runner.IsRunning) return;

            fired = true;
            runner.Begin(mission);
        }
    }
}
