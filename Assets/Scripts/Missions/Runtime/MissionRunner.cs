using System.Collections;
using System.Collections.Generic;
using ShinyMinds.Config;
using ShinyMinds.Core;
using ShinyMinds.Core.Save;
using ShinyMinds.Menu;
using ShinyMinds.Missions.Data;
using ShinyMinds.Progress;
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
        [Tooltip("Optional. Left empty, the runner uses whichever MemoryStage is in the " +
                 "scene, since this component ships inside a prefab.")]
        [SerializeField] MemoryStage memoryStage;
        [Tooltip("Optional. Speaks the mission's lines. Left empty, the runner finds one in " +
                 "the scene; with none at all, missions play silently as before.")]
        [SerializeField] ElevenLabsTTS voice;
        [Tooltip("Voice every line, rather than only nodes whose speakAloud is ticked. " +
                 "speakAloud predates the mission being fully voiced, and it lives in the " +
                 "mission asset — leaving this off means re-running the mission builder " +
                 "before any sound is heard. Untick to honour the per-node flag instead.")]
        [SerializeField] bool voiceEveryLine = true;

        [Header("Presentation")]
        [SerializeField] float typewriterCharsPerSecond = 45f;
        [SerializeField] string continuePromptText = "Press E";
        [Tooltip("Shown instead on phones and tablets, where a tap anywhere on the play " +
                 "area advances the line and there is no E to press.")]
        [SerializeField] string touchContinuePromptText = "Touch";

        MissionData mission;
        Coroutine loop;
        int pickedChoice = -1;
        string pendingRestartId;
        bool memoryOpen;
        bool warnedNoMemory;
        bool warnedNoVoice;
        AudioSource voiceAudio;
        readonly Dictionary<string, bool> flags = new Dictionary<string, bool>();
        readonly HashSet<string> warnedMissingSpeaker = new HashSet<string>();

        public bool IsRunning => loop != null;

        MemoryStage Stage => memoryStage != null ? memoryStage : MemoryStage.Current;

        /// <summary>
        /// Read per line rather than cached: a device can gain a keyboard mid-session, and
        /// the editor's touch simulation is a checkbox someone flips between play sessions.
        /// </summary>
        string ContinuePrompt =>
            TouchInput.Active && !string.IsNullOrEmpty(touchContinuePromptText)
                ? touchContinuePromptText
                : continuePromptText;

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

            // Continue on the main menu leaves a checkpoint for this mission. Reading it
            // clears it, so a retry later in the same session starts from the top.
            string startNode = mission.startNodeId;

            if (GameFlow.ConsumeResume(mission.missionId, out string resumeNode))
            {
                if (mission.GetNode(resumeNode) != null)
                {
                    startNode = resumeNode;
                }
                else
                {
                    // The mission was edited since the checkpoint was written.
                    Debug.LogWarning($"[{mission.missionId}] Checkpoint node '{resumeNode}' no " +
                                     "longer exists. Starting from the beginning.", this);
                }
            }

            loop = StartCoroutine(RunFrom(startNode));
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
            // Never leave the player frozen — or a memory hanging on screen — because
            // this object went away mid-cutscene.
            if (loop != null)
            {
                loop = null;
                memoryOpen = false;
                Stage?.SetOpen(false);
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

                // Where Continue will pick this attempt up. Recorded per node rather than
                // at scene beats: the graph is the only thing that knows where the player
                // actually is, and re-entering the city cannot reconstruct it.
                GameProgressTracker.Instance.RecordCheckpoint(current);

                switch (node.kind)
                {
                    case MissionNodeKind.Line:
                    case MissionNodeKind.Thought:
                    case MissionNodeKind.Memory:
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

            // Without a stage to act it out, a remembered line is still a line: play it
            // in the dialogue bar rather than opening an empty bubble.
            bool isMemory = node.kind == MissionNodeKind.Memory && Stage != null && ui.HasMemoryPanel;

            // A thought belongs in the same bubble, minus the stage. It is Aisha's own
            // voice, so there is nobody to act it out and nothing to render inside the
            // oval — but it is no more happening-now than a memory is, and the bubble is
            // what says so.
            bool isThoughtBubble = isThought && ui.HasMemoryThought;

            MemorySide side = isMemory && speaker != null ? speaker.memorySide : MemorySide.None;

            if (node.kind == MissionNodeKind.Memory && !isMemory)
                WarnMemoryUnavailable(node);

            PlayerInputLock.Acquire(this);

            if (isMemory || isThoughtBubble)
            {
                // Whose bubble this is. A thought speaker has a body — it is Aisha thinking —
                // while a memory speaker's actorKey is deliberately empty, because nobody in
                // the playable world is speaking: the girl standing there remembering it owns
                // the bubble.
                yield return OpenMemory(isMemory, SpeakerBody(speaker) ?? playerTransform);

                if (isMemory)
                {
                    Stage.SetSpeaking(side);
                    ui.ShowMemoryLine(speaker, side, node.text);
                }
                else
                {
                    ui.ShowMemoryThought(node.text);
                }
            }
            else
            {
                SetTalking(speaker, true);

                // With a body in the scene the line goes in a balloon over their head;
                // without one — the narrator — it falls back to the subtitle bar.
                Transform body = SpeakerBody(speaker);
                ui.ShowLine(isThought, speaker, node.text, body,
                            body != null ? cameraDirector?.ActiveCamera : null);
            }

            // Started with the line, not after the typewriter: the audio and the text
            // should arrive together, and the request takes a moment to come back.
            SpeakLine(node, speaker);

            // The first press (or tap) completes the typewriter rather than advancing.
            bool skipped = false;
            yield return ui.PlayTypewriter(
                node.text,
                typewriterCharsPerSecond,
                () => { skipped = skipped || InteractKey.TryConsumeUI(); return skipped; });

            // Before either advance path: a line that is still being spoken is not
            // finished, however long its timer was or how fast the player presses.
            yield return WaitForSpeech();

            if (node.autoAdvanceSeconds > 0f)
            {
                yield return new WaitForSeconds(node.autoAdvanceSeconds);
            }
            else
            {
                ui.ShowContinuePrompt(ContinuePrompt);

                // Don't let the skip press also count as the advance press.
                if (skipped) yield return null;

                while (!InteractKey.TryConsumeUI())
                    yield return null;
            }

            if (isMemory || isThoughtBubble)
            {
                if (isMemory) Stage.SetSpeaking(MemorySide.None);

                ui.HideLine();      // leaves the balloon up, dimmed

                // Consecutive nodes of the same kind share one bubble, so it only closes
                // once the remembered exchange — or the train of thought — is over. A
                // change of kind swaps the picture, so that one has to close and reopen.
                if (mission.GetNode(node.nextId)?.kind != node.kind)
                    yield return CloseMemory();
            }
            else
            {
                SetTalking(speaker, false);
                ui.HideLine();
            }
        }

        // ------------------------------------------------------------------- memory

        /// <summary>
        /// Says out loud which half of the setup is missing. Degrading to a plain line is
        /// the right behaviour, but doing it silently looks exactly like "the memory bubble
        /// does not work" — and the fix is always one un-run menu item.
        /// </summary>
        void WarnMemoryUnavailable(MissionNode node)
        {
            if (warnedNoMemory) return;
            warnedNoMemory = true;

            string missing = Stage == null
                ? "there is no MemoryStage in the scene (run 'ShinyMinds/Setup/7. Build Memory Stage')"
                : "MissionUI.prefab has no memory panel (run 'ShinyMinds/Setup/1. Build Mission UI Prefab')";

            Debug.LogWarning($"[{mission.missionId}] Memory node '{node.id}' is playing as an " +
                             $"ordinary line because {missing}.", this);
        }

        /// <param name="withStage">
        /// False for a thought: no stand-ins, so the little set stays switched off and its
        /// camera never renders a frame.
        /// </param>
        /// <param name="owner">Who the bubble hangs over and its trail of dots points at.</param>
        IEnumerator OpenMemory(bool withStage, Transform owner)
        {
            if (memoryOpen)
                yield break;

            memoryOpen = true;

            if (withStage)
                Stage?.SetOpen(true);   // the set must be rendering before the bubble fades up

            yield return ui.OpenMemory(withStage, owner, cameraDirector?.ActiveCamera);
        }

        IEnumerator CloseMemory()
        {
            if (!memoryOpen)
                yield break;

            memoryOpen = false;
            yield return ui.CloseMemory();
            Stage?.SetOpen(false);
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

            // After RecordEnding above, so the summary's "attempt N" counts this run.
            ui.ShowEnding(ending, mission.missionId,
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
                // Back to the open world. The objective has to go with the mission — left up, it
                // would hang in the corner telling the player to walk home for the rest of the
                // session — and the camera has to come back to the follow rig.
                ui.SetObjective(string.Empty);

                // The camera comes back BEFORE the input lock lifts, not after. The
                // cutscene camera renders by switching the main camera off, and Camera.main
                // only ever returns an enabled camera — so for the length of this blend
                // there is no main camera. Lifting the lock first let CameraController read
                // look input against it and throw a NullReferenceException every frame of
                // the blend, leaving the rig frozen just as the player got control back.
                yield return cameraDirector.Release(0.6f);

                PlayerInputLock.Release(this);

                // And the screen has to come BACK from black. Path A walks the pair off and fades
                // out (`Fade(true, 2.0f)`) with nothing that ever fades in again: only the retry
                // path cleared the overlay, via ResetWorld -> HideAll. Continue therefore handed
                // the player a free-roaming city they could not see. The camera blend above runs
                // first on purpose, so it happens behind the black rather than in view.
                //
                // A no-op on the paths that already faded in — Fade lerps from the overlay's
                // current alpha, so 0 -> 0 costs nothing.
                yield return ui.Fade(toBlack: false, seconds: 0.6f);
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
            Stage?.SetOpen(false);
            memoryOpen = false;

            // Otherwise a line cut off mid-sentence keeps talking over whatever comes
            // next — free roam, a retry, or the main menu.
            voice?.StopSpeaking();
            voiceAudio?.Stop();

            PlayerInputLock.ResetAll();
            flags.Clear();
            pickedChoice = -1;
        }

        // ------------------------------------------------------------------- voice

        /// <summary>
        /// The TTS component, found lazily. This runner ships inside a prefab, so it
        /// cannot hold a scene reference until the scene exists around it.
        /// </summary>
        /// <summary>
        /// The mission's own voice rig, created on demand. Deliberately not the NPCs':
        /// GroqDialogue drives those, gating its "E = Next" prompt on their IsSpeaking and
        /// listening for their OnSpeechFinished, so borrowing one would make a mission line
        /// look like an NPC's and let a mission abort cut off a conversation mid-sentence.
        /// </summary>
        void EnsureVoiceHost()
        {
            if (voiceAudio != null)
                return;

            var host = new GameObject("MissionVoice");
            host.transform.SetParent(transform, false);

            voiceAudio = host.AddComponent<AudioSource>();
            voiceAudio.playOnAwake = false;

            // Shares the AudioSource, so "is a line still playing" is one question however
            // the audio was produced.
            if (voice == null)
                voice = host.AddComponent<ElevenLabsTTS>();

            voice.audioSource = voiceAudio;
        }

        /// <summary>
        /// Speaks a line when the node opts in. The voice is cast per speaker: an id set
        /// on the SpeakerProfile wins, otherwise ELEVENLABS_&lt;KEY&gt;_VOICE_ID from .env,
        /// otherwise the shared NPC voice. Silent when nothing is configured, which is
        /// what keeps a fresh checkout playable without any ElevenLabs account.
        /// </summary>
        void SpeakLine(MissionNode node, SpeakerProfile speaker)
        {
            if (!(node.speakAloud || voiceEveryLine) || string.IsNullOrWhiteSpace(node.text))
                return;

            EnsureVoiceHost();

            // Baked first, and for a shipped build that is the only path taken: the whole
            // mission was generated once by ShinyMinds/Voice/Bake Mission Dialogue.
            AudioClip baked = MissionVoiceBank.Load(mission.missionId, node);

            if (baked != null)
            {
                voiceAudio.Stop();
                voiceAudio.clip = baked;
                voiceAudio.Play();

                return;
            }

            // Not baked, or the line has been edited since the last bake. Generating it now
            // keeps a mission playable while its dialogue is still being written.
            string voiceId = speaker != null && !string.IsNullOrWhiteSpace(speaker.elevenLabsVoiceId)
                ? speaker.elevenLabsVoiceId
                : GameConfig.VoiceIdForSpeaker(speaker?.key);

            if (string.IsNullOrWhiteSpace(voiceId))
            {
                if (warnedNoVoice)
                    return;

                warnedNoVoice = true;

                Debug.LogWarning($"[{mission.missionId}] No voice for speaker " +
                                 $"'{speaker?.key}'. Set {GameConfig.VoiceIdNameFor(speaker?.key)} " +
                                 $"or {GameConfig.NpcVoiceIdName} in .env, or bake the mission. " +
                                 "Lines play as subtitles only.", this);

                return;
            }

            voice.Speak(node.text, voiceId);
        }

        /// <summary>
        /// Holds the line on screen until its audio has finished. Without this the player
        /// can press through a sentence while it is still being spoken and the next line
        /// talks over it.
        /// </summary>
        IEnumerator WaitForSpeech()
        {
            // Both paths land on the same AudioSource, but a live line is still being
            // fetched before anything plays, so IsSpeaking has to be asked as well.
            while ((voiceAudio != null && voiceAudio.isPlaying)
                   || (voice != null && voice.IsSpeaking))
            {
                yield return null;
            }
        }

        /// <summary>The speaker's transform in the scene, or null for a bodiless voice.</summary>
        Transform SpeakerBody(SpeakerProfile speaker)
        {
            if (speaker == null || string.IsNullOrEmpty(speaker.actorKey))
                return null;

            IMissionActor actor = GetActor(speaker.actorKey);

            // An empty actorKey means a bodiless voice and is silent above — but a speaker that
            // NAMES an actor and does not find one is a missing character, and the only symptom
            // is their line quietly appearing in the narrator's subtitle bar instead of over
            // their head. Warned once per speaker so a whole conversation cannot bury the console.
            if (actor == null)
            {
                if (warnedMissingSpeaker.Add(speaker.actorKey))
                {
                    Debug.LogWarning($"Speaker '{speaker.key}' names actor '{speaker.actorKey}', " +
                                     "which is not registered in this scene. Their lines will play " +
                                     "in the subtitle bar and no body will appear. Run " +
                                     "'ShinyMinds/Setup/6. Wire Open Scene'.", this);
                }

                return null;
            }

            if (actor.GameObject == null || !actor.GameObject.activeInHierarchy)
                return null;

            return actor.Transform;
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
