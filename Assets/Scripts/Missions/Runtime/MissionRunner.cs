using System.Collections;
using System.Collections.Generic;
using ShinyMinds.Core;
using ShinyMinds.Core.Save;
using ShinyMinds.Missions.Data;
using ShinyMinds.Missions.UI;
using UnityEngine;

namespace ShinyMinds.Missions.Runtime
{
    /// <summary>
    /// Walks a MissionData graph and stages it.
    ///
    /// Coroutines rather than Timeline: `yield return someIEnumerator` composes nested
    /// actions for free, it is the pattern already used elsewhere in this project, and
    /// a new mission is a new asset with no new code. Timeline would need a .playable
    /// per branch with its own scene bindings.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class MissionRunner : MonoBehaviour, IMissionContext
    {
        [Header("Mission")]
        [SerializeField] MissionData autoStartMission;
        [Tooltip("Give PlayerController.Start() time to finish SnapToGround() before " +
                 "the first cutscene teleports anyone.")]
        [SerializeField] float autoStartDelay = 0.5f;

        [Header("Scene references")]
        [SerializeField] MissionUIView ui;
        [SerializeField] MissionCameraDirector cameraDirector;
        [SerializeField] AudioSource sfx;
        [SerializeField] Transform playerTransform;

        [Header("Presentation")]
        [SerializeField] float typewriterCharsPerSecond = 45f;
        [SerializeField] string continuePromptText = "Press E";

        MissionData mission;
        Coroutine loop;
        int pickedChoice = -1;
        string pendingRestartId;
        readonly Dictionary<string, bool> flags = new Dictionary<string, bool>();

        public bool IsRunning => loop != null;

        // ------------------------------------------------------------ IMissionContext

        public IMissionActor GetActor(string key) => MissionSceneRegistry.GetActor(key);
        public Transform GetMarker(string key) => MissionSceneRegistry.GetMarker(key);
        public IMissionUi Ui => ui;
        public IMissionCamera Camera => cameraDirector;
        public AudioSource Sfx => sfx;
        public Transform Player => playerTransform;

        public void SetFlag(string key, bool value)
        {
            if (!string.IsNullOrEmpty(key)) flags[key] = value;
        }

        public bool GetFlag(string key)
            => !string.IsNullOrEmpty(key) && flags.TryGetValue(key, out bool v) && v;

        // ------------------------------------------------------------------ lifecycle

        IEnumerator Start()
        {
            if (autoStartMission == null)
                yield break;

            yield return new WaitForSeconds(autoStartDelay);
            Begin(autoStartMission);
        }

        public void Begin(MissionData data)
        {
            if (data == null || loop != null)
                return;

            mission = data;
            mission.ValidateGraph();

            flags.Clear();
            pendingRestartId = null;
            loop = StartCoroutine(RunFrom(mission.startNodeId));
        }

        /// <summary>Aborts the mission and hands the player back a clean world.</summary>
        public void Abort()
        {
            if (loop != null)
            {
                StopAllCoroutines();
                loop = null;
            }

            ResetWorld();
        }

        void OnDisable()
        {
            // Never leave the player frozen because this object went away mid-cutscene.
            if (loop != null)
            {
                loop = null;
                PlayerInputLock.ResetAll();
            }
        }

        // ------------------------------------------------------------------- the loop

        IEnumerator RunFrom(string startNodeId)
        {
            string current = startNodeId;

            while (!string.IsNullOrEmpty(current))
            {
                MissionNode node = mission.GetNode(current);

                if (node == null)
                {
                    Debug.LogError($"[{mission.missionId}] Unknown node id '{current}'. Stopping.", this);
                    break;
                }

                switch (node.kind)
                {
                    case MissionNodeKind.Line:
                    case MissionNodeKind.Thought:
                        yield return ShowLine(node);
                        current = node.nextId;
                        break;

                    case MissionNodeKind.Cutscene:
                        yield return RunActions(node.actions);
                        current = node.nextId;
                        break;

                    case MissionNodeKind.Choice:
                        yield return ShowChoice(node);
                        current = (pickedChoice >= 0 && pickedChoice < node.choices.Count)
                            ? node.choices[pickedChoice].nextId
                            : node.nextId;
                        break;

                    case MissionNodeKind.Ending:
                        yield return ShowEnding(mission.GetEnding(node.endingId));

                        // Retry sets pendingRestartId and the loop simply continues from
                        // the top, so we never stop a coroutine from inside itself.
                        current = pendingRestartId;
                        pendingRestartId = null;
                        break;
                }
            }

            loop = null;
        }

        /// <summary>
        /// Sequential by default. Actions marked runInParallel start immediately and are
        /// joined before the next sequential action, and again at the end of the list.
        /// </summary>
        IEnumerator RunActions(List<CutsceneAction> actions)
        {
            if (actions == null)
                yield break;

            var pending = new List<Coroutine>();

            foreach (CutsceneAction action in actions)
            {
                if (action == null) continue;

                if (action.runInParallel)
                {
                    pending.Add(StartCoroutine(action.Execute(this)));
                }
                else
                {
                    for (int i = 0; i < pending.Count; i++)
                        yield return pending[i];

                    pending.Clear();
                    yield return action.Execute(this);
                }
            }

            for (int i = 0; i < pending.Count; i++)
                yield return pending[i];
        }

        IEnumerator ShowLine(MissionNode node)
        {
            SpeakerProfile speaker = mission.GetSpeaker(node.speakerKey);
            bool isThought = node.kind == MissionNodeKind.Thought;

            PlayerInputLock.Acquire(this);
            SetTalking(speaker, true);
            ui.ShowLine(isThought, speaker, node.text);

            // The first E press completes the typewriter rather than advancing.
            bool skipped = false;
            yield return ui.PlayTypewriter(
                node.text,
                typewriterCharsPerSecond,
                () => { skipped = skipped || InteractKey.TryConsumeUI(); return skipped; });

            if (node.autoAdvanceSeconds > 0f)
            {
                yield return new WaitForSeconds(node.autoAdvanceSeconds);
            }
            else
            {
                ui.ShowContinuePrompt(continuePromptText);

                // Don't let the skip press also count as the advance press.
                if (skipped) yield return null;

                while (!InteractKey.TryConsumeUI())
                    yield return null;
            }

            SetTalking(speaker, false);
            ui.HideLine();
        }

        IEnumerator ShowChoice(MissionNode node)
        {
            pickedChoice = -1;

            PlayerInputLock.Acquire(this);
            PlayerInputLock.PushCursorFree(this);   // cursor appears; camera is already frozen

            ui.ShowChoices(node.prompt, node.choices, i => pickedChoice = i);

            while (pickedChoice < 0)
                yield return null;

            ui.HideChoices();
            PlayerInputLock.PopCursorFree(this);    // cursor re-locks and hides
        }

        IEnumerator ShowEnding(MissionEnding ending)
        {
            if (ending == null)
                yield break;

            PlayerInputLock.Acquire(this);
            PlayerInputLock.PushCursorFree(this);

            if (ending.stinger != null && sfx != null)
                sfx.PlayOneShot(ending.stinger);

            SaveService.RecordEnding(mission.missionId, ending);

            bool retry = false;
            bool done = false;

            ui.ShowEnding(ending,
                onRetry: () => { retry = true; done = true; },
                onContinue: () => { done = true; });

            while (!done)
                yield return null;

            ui.HideEnding();
            PlayerInputLock.PopCursorFree(this);

            if (retry)
            {
                ResetWorld();
                pendingRestartId = mission.startNodeId;
            }
            else
            {
                PlayerInputLock.Release(this);
                yield return cameraDirector.Release(0.6f);
            }
        }

        // ------------------------------------------------------------------- helpers

        /// <summary>
        /// Puts every registered actor back where it started. An in-place reset rather
        /// than a scene reload: the scene is 3 MB with the whole city mesh, the project
        /// has no scene-loading code at all, and PlayerController's Start() does
        /// raycast-dependent ground snapping that we don't want re-running.
        /// </summary>
        void ResetWorld()
        {
            foreach (IMissionActor actor in MissionSceneRegistry.AllActors())
            {
                actor.Mover?.Stop();
                actor.ResetToSpawn();
            }

            cameraDirector?.HardRelease();
            ui?.HideAll();
            PlayerInputLock.ResetAll();
            flags.Clear();
            pickedChoice = -1;
        }

        void SetTalking(SpeakerProfile speaker, bool talking)
        {
            if (speaker == null || string.IsNullOrEmpty(speaker.actorKey))
                return;

            IMissionActor actor = GetActor(speaker.actorKey);
            Animator animator = actor?.Animator;

            if (animator == null || animator.runtimeAnimatorController == null)
                return;

            foreach (AnimatorControllerParameter p in animator.parameters)
            {
                if (p.name == "IsTalking")
                {
                    animator.SetBool("IsTalking", talking);
                    return;
                }
            }
        }
    }
}
