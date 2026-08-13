using ShinyMinds.Core;
using ShinyMinds.Menu;
using ShinyMinds.Missions.Data;
using ShinyMinds.Missions.UI;
using UnityEngine;

namespace ShinyMinds.Missions.Runtime
{
    /// <summary>
    /// Offers a mission when the player comes close, and starts it only if they accept.
    ///
    /// The city is open world: walking near the school should not seize control. So this shows
    /// a banner naming the mission and waits for the same advance input everything else uses —
    /// "Press E" on a keyboard, a tap on a phone. Walk away and the offer simply withdraws.
    ///
    /// Re-armed on exit rather than fired once, so a player who finishes a mission and chooses
    /// to keep roaming can come back and play it again. <see cref="rearmSeconds"/> stops the
    /// zone re-offering the instant a mission ends while they are still standing in it.
    /// </summary>
    // Before NPCInteraction (-50) and DoorController (0): if a mission is on offer here, the
    // accept press belongs to the mission rather than to a door on the same doorstep.
    [DefaultExecutionOrder(-60)]
    [RequireComponent(typeof(Collider))]
    public class MissionTrigger : MonoBehaviour
    {
        [SerializeField] MissionData mission;
        [SerializeField] MissionRunner runner;
        [Tooltip("Draws the banner. Left empty, the zone finds the one in the scene.")]
        [SerializeField] MissionUIView ui;

        [Header("Behaviour")]
        [Tooltip("Skip the offer and start the moment the player walks in. The old behaviour, " +
                 "kept for a mission that is meant to ambush.")]
        [SerializeField] bool startImmediately = false;
        [Tooltip("Offer the mission only once per session, even if it is never played.")]
        [SerializeField] bool once = false;
        [Tooltip("Quiet time after a mission ends before this zone will offer again, so the " +
                 "summary's Continue does not drop the player straight back into the offer.")]
        [SerializeField] float rearmSeconds = 1.5f;

        bool playerInside;
        bool offering;
        bool consumed;
        bool wasRunning;
        float quietUntil;

        MissionUIView Ui => ui != null ? ui : (ui = FindAnyObjectByType<MissionUIView>());

        void Reset()
        {
            Collider c = GetComponent<Collider>();
            if (c != null) c.isTrigger = true;
        }

        void OnDisable() => Withdraw();

        void Start()
        {
            // Continue on the main menu resumes the mission outright. Waiting for the
            // player to walk back to the offer zone and accept would mean replaying the
            // walk to it, which is the part they already did.
            if (runner != null && mission != null && !runner.IsRunning
                && GameFlow.ResumeMissionId == mission.missionId)
            {
                runner.Begin(mission);
            }
        }

        void Update()
        {
            if (runner == null || mission == null)
                return;

            // The frame a mission stops running, start the quiet time. Without it the banner
            // reappears under the player's finger as the summary closes.
            if (wasRunning && !runner.IsRunning)
                quietUntil = Time.time + rearmSeconds;

            wasRunning = runner.IsRunning;

            if (!CanOffer())
            {
                Withdraw();
                return;
            }

            if (startImmediately)
            {
                Accept();
                return;
            }

            if (!offering)
            {
                offering = true;
                Ui?.ShowBanner(mission.title, mission.objective,
                               $"{InteractKey.AdvanceLabel} to begin");
            }

            // TryConsumeWorld, not TryConsumeUI: this is a world interaction and must be blocked
            // while a cutscene or a Groq conversation owns input.
            if (InteractKey.TryConsumeWorld())
                Accept();
        }

        bool CanOffer()
        {
            return playerInside
                   && !consumed
                   && !runner.IsRunning
                   && !PlayerInputLock.IsLocked
                   && Time.time >= quietUntil;
        }

        void Accept()
        {
            Withdraw();

            if (once)
                consumed = true;

            runner.Begin(mission);
        }

        void Withdraw()
        {
            if (!offering)
                return;

            offering = false;
            Ui?.HideBanner();
        }

        void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Player"))
                playerInside = true;
        }

        void OnTriggerExit(Collider other)
        {
            if (!other.CompareTag("Player"))
                return;

            playerInside = false;
            Withdraw();
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.35f, 0.85f, 0.45f, 0.35f);

            var sphere = GetComponent<SphereCollider>();
            if (sphere != null)
            {
                Gizmos.DrawWireSphere(transform.TransformPoint(sphere.center),
                                      sphere.radius * Mathf.Abs(transform.lossyScale.x));
            }
        }
    }
}
