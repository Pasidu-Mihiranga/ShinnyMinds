# Mission 01 — "The Road Home"

## Context

ShinyMinds is a Unity 6 educational game for children (8–14). The 3D city environment, a
playable third-person girl character (Aisha / `GİRL 1`), a school interior, roads, houses,
traffic and two LLM-driven road-safety NPCs already work. What does **not** exist is any
notion of a *mission*: no ScriptableObjects, no branching dialogue, no choices, no endings,
no cutscenes, no save state. `GroqDialogue.cs` is the only dialogue code and it is linear,
per-NPC, duplicated, and built on an `if/else` topic chain — exactly what `AGENTS.md`
forbids for missions.

The goal is to ship **"The Road Home"** as the game's first mission — a stranger-danger
story with three branches (go with the stranger / walk away / find a trusted adult) and
three graded endings — while building it as a **reusable mission system**, so mission 02
is a new `.asset` file and *zero new code*.

Confirmed direction:
- **Hybrid content** — the story is authored data; Groq LLM stays only for ambient NPC chatter.
- **Keyboard gameplay, mouse-picked choices** — WASD + `E` as today; the A/B/C decision uses clickable buttons with the cursor temporarily freed.
- **Fully staged cutscenes** — the stranger really walks up, Aisha really walks to the teacher, the mother really arrives.
- **Move all loose scripts into `Assets/Scripts/`** per `AGENTS.md`.

---

## Verified facts that shape the design

| Fact | Consequence |
|---|---|
| `CameraHolder` → child of `GİRL 1`; `Main Camera` → child of `CameraHolder` | Moving the player drags the camera. Use a **separate `CutsceneCamera`**, never reparent. |
| `Teacher` and `Mother` are in the scene but have **no Animator controller** assigned | They cannot move or emote until one is authored and assigned. |
| `CameraCollision` does an **unmasked** `SphereCast` with default `QueryTriggerInteraction` | Camera will slam forward the moment the Stranger stands behind Aisha. Already misbehaves near the two `InteractionZone` triggers. Must be fixed. |
| `MapToggle` (M key) does `SetActive(false)` on the Main Camera **GameObject** | Takes the `AudioListener` with it — would black out and mute a cutscene. |
| `GİRL 1` is scale 5; `Teacher`/`Mother` are scale 2; Mixamo clips are authored at scale 1 with `applyRootMotion = false` | Movement speed **must** be multiplied by `lossyScale.y` or feet slide. `PlayerController.walkSpeed = 3` already has this bug. |
| 3 GameObjects named `GameManager`, ~325 named `Waypoint (n)` | Never `GameObject.Find`. Resolve scene objects through a **key registry**. Never prefix a mission marker with `Waypoint`. |
| `CarAI.waypoints` is a serialized `Transform[]` | Deleting/reordering waypoints silently breaks car routes. Don't touch them. |
| `Assets/characters/NPC_Characters/Ch29_nonPBR.fbx` and `kaya.fbx` are imported but unused | `Ch29_nonPBR` is the Stranger. |
| `PlayerAnimator.controller` already has unused states `fear`, `sad`, `sit` | Free emotional beats — just needs triggers wired. |
| Two duplicate `BackgroundMusic` AudioSources (merge leftover) | Doubled volume; will mask the school bell. Delete one. |
| `activeInputHandler: 2`; all gameplay uses legacy `Input`, `EventSystem` uses `InputSystemUIInputModule` | Load-bearing. uGUI Buttons work. Do **not** switch Project Settings to New-only. |

---

## Phase 0 — Script reorganisation (own commit, no behaviour change)

**Precondition: `git status` must be clean.** There are currently uncommitted changes to
`Assets/PlayerController.cs` and `Assets/Scenes/SampleScene.unity` — commit or stash first.

Taxonomy under `Assets/Scripts/`: `Core/`, `Player/`, `CameraRig/`, `Interaction/`,
`Dialogue/`, `Vehicles/`, `UI/`, `Missions/`.
(`CameraRig`, not `Camera` — avoids ambiguity with `UnityEngine.Camera`.)

| Current | New |
|---|---|
| [PlayerController.cs](Assets/PlayerController.cs) | `Assets/Scripts/Player/PlayerController.cs` |
| [footstepaudio.cs](Assets/Audio/Footsteps/footstepaudio.cs) | `Assets/Scripts/Player/footstepaudio.cs` (leave the clips in `Assets/Audio/Footsteps/`) |
| [CameraController.cs](Assets/CameraController.cs), [CameraCollision.cs](Assets/CameraCollision.cs) | `Assets/Scripts/CameraRig/` |
| [doorController.cs](Assets/doorController.cs), [DoorSideDetector.cs](Assets/DoorSideDetector.cs), [NPCInteraction.cs](Assets/NPCInteraction.cs) | `Assets/Scripts/Interaction/` |
| [GroqDialogue.cs](Assets/GroqDialogue.cs), [ElevenLabsTTS.cs](Assets/ElevenLabsTTS.cs) | `Assets/Scripts/Dialogue/` |
| [CarAI.cs](Assets/CarAI.cs), [soundController.cs](Assets/Audio/Car%20Sound/soundController.cs) | `Assets/Scripts/Vehicles/` |
| [MapToggle.cs](Assets/MapToggle.cs), [MiniMapFollow.cs](Assets/MiniMapFollow.cs), [MiniMapArrow.cs](Assets/MiniMapArrow.cs) | `Assets/Scripts/UI/` |
| `Assets/TutorialInfo/Scripts/**` | **Do not move.** `ReadmeEditor.cs` must stay under a folder literally named `Editor`. Delete the whole `Assets/TutorialInfo/` folder + `Assets/Readme.asset` in a separate commit instead — it's unused Unity template boilerplate. |

**Safe procedure.** Do the move **inside the Unity Editor Project window**. Unity moves
`.cs` + `.cs.meta` atomically and preserves the GUID, so `SampleScene.unity` needs **zero
edits** (`m_Script: {guid: …}` is path-independent). If done from the shell instead, Unity
must be **closed** and you must `git mv` *both* the `.cs` and its `.cs.meta` — a `.cs`
arriving without its `.meta` mints a fresh GUID and turns every scene reference into
"Missing (Mono Script)", recoverable only by hand-editing 3 MB of YAML.

Do **not** rename classes (`doorController`→`DoorController`, `footstepaudio`→`FootstepAudio`)
or add namespaces in this commit. Do **not** add an `.asmdef` — asmdef assemblies cannot
reference `Assembly-CSharp` and `ReadmeEditor.cs` lives outside `Assets/Scripts`.

**Verify:** `git diff --stat` shows **renames only**; `SampleScene.unity` must not appear.
In Play mode: walk, run, turn, jump, camera, minimap, M-map, school door, both Groq NPCs.

---

## Phase 1 — Core services (`Assets/Scripts/Core/`)

These are prerequisites and also fix three live bugs.

**`PlayerInputLock.cs`** — static, refcounted by owner object, so a mission and a Groq NPC
can't unlock each other. `Acquire/Release(object)`, `PushCursorFree/PopCursorFree(object)`,
`ApplyCursor()`, `ResetAll()`, `event Action<bool> GameplayLockChanged`. Needs
`[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]` to clear statics — otherwise
"Disable Domain Reload" leaves the player permanently frozen on the second Play session.

**`PlayerLockBinder.cs`** (MonoBehaviour on `GİRL 1`) — subscribes to `GameplayLockChanged`.
`Apply(bool locked)` must do **three** things, not one:
1. `enabled = !locked` on `PlayerController`, `CameraController`, `MapToggle`, `footstepaudio`.
2. On lock, `footstepSource.Stop()` — `footstepaudio` only calls `Stop()` in its `else`
   branch, so disabling it while `W` is held leaves the walk loop playing forever.
3. On lock, zero the animator (`Speed`=0, `TurnLeft`/`TurnRight`/`Backward`=false) —
   `PlayerController` writes these every `Update`, so disabling it freezes them at their
   last value and Aisha walks in place through the whole conversation.

Both (2) and (3) are the existing `GroqDialogue` freeze bug. Leave `CameraCollision`
**enabled** — it's `LateUpdate` positional only.

**`CursorStateKeeper.cs`** — `Start()` and `OnApplicationFocus(true)` → `ApplyCursor()`.
Unity silently drops the cursor lock on alt-tab; this is required, not optional.

**`InteractKey.cs`** — the `E`-collision fix. The only place gameplay `E` is read.
`TryConsumeWorld()` (blocked while locked) and `TryConsumeUI()` (exclusive but not blocked),
both guarded by a `consumedFrame == Time.frameCount` check. Wire with execution order:

| File | Change |
|---|---|
| `NPCInteraction.cs` | `Input.GetKeyDown(KeyCode.E)` → `InteractKey.TryConsumeWorld()`; `[DefaultExecutionOrder(-50)]`; early-out `if (PlayerInputLock.IsLocked) return;` |
| `doorController.cs` | same substitution (default order 0 → door loses an overlap to a talkable NPC); hide `doorPrompt` while locked |
| `GroqDialogue.cs` | → `InteractKey.TryConsumeUI()`; `[DefaultExecutionOrder(-100)]` |
| `MissionRunner.cs` | → `InteractKey.TryConsumeUI()`; `[DefaultExecutionOrder(-100)]` |

Resulting priority: mission/dialogue advance ▸ NPC conversation ▸ door. Deterministic.

**Retrofits into existing files (surgical):**
- `CameraController.cs` — delete `Cursor.lockState = CursorLockMode.Locked;` from `Start()`
  (ownership moves to `PlayerInputLock`); add `if (PlayerInputLock.IsLocked) return;` at
  the top of `Update()` so the camera can't spin while the choice cursor is free.
- `GroqDialogue.cs` — `playerController.enabled = false/true` → `PlayerInputLock.Acquire/Release(this)`.
  Keep the serialized field so the scene YAML doesn't churn.
- `CameraCollision.cs` — add `[SerializeField] LayerMask blockers = ~0;` and pass it plus
  `QueryTriggerInteraction.Ignore` to the `SphereCast`.

**Verify:** talk to `Ch03_nonPBR` — Aisha stands still, footsteps stop, camera frozen, `F`
returns control, cursor stays locked. Alt-tab and back → still locked. Stand where a door
trigger and an `InteractionZone` overlap, press `E` → exactly one thing happens.

---

## Phase 2 — Data model (`Assets/Scripts/Missions/Data/`)

**The serialization decision is split, deliberately.**

- **Story graph → flat node list with string IDs, no polymorphism.** Every narrative node is
  structurally "present something, then go to an ID"; the variation is which *fields* are
  filled, not which *methods* run. This mission alone is ~55 nodes. `[SerializeReference]`
  stores the concrete type by name — a rename or re-namespace **nulls every stored
  instance, with no undo**. Not worth risking 55 authored nodes to save a few unused fields.
  String IDs also read well in `git diff`.
- **Cutscene actions → `[SerializeReference]`, and here it is correct.** The variation *is*
  behavioural (`MoveActorAction` vs `FadeScreenAction` share nothing) and each carries its
  own `Execute` coroutine. Unity 6 renders a **type-picker dropdown** for managed-reference
  list elements with zero custom editor code. Volume is low (~40 instances).
  Mitigations from day one: lock `namespace ShinyMinds.Missions.Data` **before** authoring
  content; `[MovedFrom]` on any later rename; one class per file, filename == class name.

**Types:**

- `MissionData : ScriptableObject` — `[CreateAssetMenu(menuName = "ShinyMinds/Mission Data")]`.
  `missionId`, `title`, `objective`, `startNodeId`, `List<MissionNode> nodes`,
  `List<MissionEnding> endings`, `List<SpeakerProfile> speakers`. Lazy `Dictionary` lookups
  for `GetNode`/`GetEnding`/`GetSpeaker`, invalidated in `OnValidate()` so Play-mode edits apply.
- `MissionNode` — `id`, `MissionNodeKind kind` (`Line`, `Thought`, `Cutscene`, `Choice`,
  `Ending`), `speakerKey`, `text`, `autoAdvanceSeconds`, `speakAloud`,
  `[SerializeReference] List<CutsceneAction> actions`, `prompt`, `List<MissionChoice> choices`,
  `endingId`, `nextId`.
- `MissionChoice` — `label`, `nextId`, `MissionChoiceTone tone` (Unsafe/Neutral/Safe/Safest;
  analytics tagging only, gates nothing).
- `MissionEnding` — `id`, `EndingQuality quality`, `title`, `lesson`, **`Sprite badge`**
  (a Sprite, *not* an emoji glyph — TMP's default LiberationSans atlas renders ❌✅🏆 as tofu),
  `AudioClip stinger`, `stars`, `completesMission`, `allowRetry`, `allowContinue`.
- `SpeakerProfile` — `key`, `displayName`, `nameColor`, `actorKey` (empty for narrator /
  memory voices), `elevenLabsVoiceId`.
- `IMissionContext` — the only surface actions may touch: `GetActor(key)`, `GetMarker(key)`,
  `Ui`, `Camera`, `Sfx`, `Player`, `SetFlag`/`GetFlag`. Keeps the data layer free of scene knowledge.

**Action set** (`Data/Actions/`, one tiny file each, all deriving from
`abstract class CutsceneAction { public bool runInParallel; public abstract IEnumerator Execute(IMissionContext ctx); }`):

`WaitAction` · `MoveActorAction` (actorKey, markerKey, run, walkBackwards, faceActorKeyOnArrive) ·
`FaceAction` · `TeleportActorAction` · `SetActorActiveAction` · `AnimatorParamAction`
(Trigger/Bool/Float + `holdSeconds`) · `PlaySoundAction` · `CameraShotAction` ·
`CameraReleaseAction` · `FadeScreenAction` · `LetterboxAction` · `SetObjectiveAction` ·
`SetPlayerControlAction` · `WaitForPlayerAtMarkerAction` · `SetFlagAction`.

Every action must tolerate a null clip / missing actor (log a warning, skip) so later
phases are testable before audio exists.

**Verify:** `Assets ▸ Create ▸ ShinyMinds ▸ Mission Data` works; adding an element to a
node's `actions` list shows the type-picker with all 15 action types.

---

## Phase 3 — Scene runtime (`Assets/Scripts/Missions/Runtime/`)

ScriptableObjects can't reference scene objects and `GameObject.Find` is unusable here
(325 `Waypoint (n)`, 3 `GameManager`). So: **string keys through a self-registering registry.**

- **`MissionSceneRegistry.cs`** — two static dictionaries, `Register`/`Unregister`/
  `GetActor`/`GetMarker`/`AllActors`, warns on duplicate keys,
  `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]` clear.
- **`MissionActor.cs`** — on `GİRL 1`, `Teacher`, `Mother`, `Stranger`. `actorKey`,
  `Animator`, `ActorMover`. `Awake()` captures spawn pos/rot/`activeSelf` and registers.
  `ResetToSpawn()` **must** handle the `CharacterController`: `cc.enabled = false` → set
  transform → `cc.enabled = true`, then `animator.Rebind(); animator.Update(0f);` to clear
  latched emote triggers. Writing `transform.position` on an enabled CC is silently reverted.
- **`MissionMarker.cs`** — `markerKey` + register; `OnDrawGizmos` draws a labelled sphere and
  forward arrow so staging is editable in the Scene view.
- **`ActorMover.cs`** — walking **without NavMesh**. No NavMesh is baked, and baking one over
  `Worldcity.fbx` is a multi-hour detour; the mission's paths are short, straight and
  hand-authored via markers, so pathfinding buys nothing. `Vector3.MoveTowards` +
  `Quaternion.RotateTowards` + a downward ground ray. Drives the *existing* animator params
  (`Speed` 0/2/6 to match `PlayerController`'s hardcoded values, `Backward`, `TurnLeft`/`TurnRight`).
  **Critical:** multiply world speed by `Mathf.Abs(transform.lossyScale.y)` — at scale 5 a
  `walkSpeed` of 1.4 makes Aisha inch forward while her legs cycle at full stride.
  Uses `characterController.Move()` when one exists (with a downward gravity term), else
  `transform.position +=` plus `SnapDown()`.
- **`MissionCameraDirector.cs`** — **dedicated `CutsceneCamera`, never move `Main Camera`.**
  `CutsceneCamera` is a root GameObject in the `MissionSystem` prefab with its `Camera`
  component **disabled** and **no `AudioListener`** (a second listener spams warnings and
  halves audio). `TakeOver()` forces the Main Camera **GameObject** active (defends against
  `MapToggle`), copies its world pose, enables `cutsceneCamera.enabled`, then sets
  `mainCamera.enabled = false` — disabling the **component only** keeps the AudioListener
  alive and sidesteps URP camera stacking (exactly one Base camera renders).
  `ShotTo(marker, blend, lookAt)`, `Release(blend)`, `HardRelease()`.
- **`MissionTrigger.cs`** — `BoxCollider(isTrigger)` + `MissionData` + `once`. Not used by
  Mission 01 (which auto-starts) but needed for 02+, so build it now.

**Verify:** a temporary debug key calls `Teacher.Mover.MoveTo(marker)` — she walks with feet
planted, turns to face the marker, returns to idle. No duplicate-key warnings.

---

## Phase 4 — Shared UI (`Assets/Prefabs/UI/MissionUI.prefab`)

One prefab, Screen Space Overlay, **Sort Order 100**, CanvasScaler *Scale With Screen Size*
1920×1080 match 0.5.

```
MissionUI                    MissionUIView  <-- the only script here
├── Letterbox/{BarTop,BarBottom}      Image, raycastTarget OFF
├── FadeOverlay                       Image black a=0, full screen, raycastTarget OFF
├── ObjectiveHud                      TMP, top-left
├── DialoguePanel/Frame               SpeakerLabel · LineText (+TypewriterText) · ContinuePrompt
├── ThoughtPanel/Bubble               ThoughtText — italic, centred  <-- Aisha's inner voice
├── ChoicePanel                       ChoicePrompt + 3× ChoiceButton (Button + TMP)
└── EndingCard/Frame                  Badge(Image) · EndingTitle · LessonText · Retry/Continue
```

`MissionUIView` implements `IMissionUi` and is a **pure view** — no game logic, no
`MissionData` mutation. `ShowChoices` activates only `min(3, choices.Count)` buttons so
future missions can have 2 or 4. Optionally also accept `Alpha1/2/3` for accessibility.

**Raycast-target hygiene:** `FadeOverlay` and both letterbox bars must have *Raycast Target*
**off** — with `InputSystemUIInputModule`, a full-screen raycast target swallows every click.

**Coexistence with the two existing per-NPC canvases:** they are Overlay at
`sortingOrder = 0`, so `MissionUI` at 100 always draws above. `PlayerInputLock` guarantees
they're never interactively live at the same time. Leave their `ConstantPixelSize` 800×600
scalers alone — that's unrelated refactoring.

Sub-scripts, each under ~60 lines: `TypewriterText.cs`, `MissionChoiceButton.cs`,
`MissionEndingCard.cs`, `ObjectiveHud.cs`.

**Verify:** toggle each panel manually; choice buttons click with the cursor freed;
`MissionUI` draws above the NPC canvases; legible at 1920×1080 **and** 1280×720.

---

## Phase 5 — `MissionRunner` + full authored graph (text-only milestone)

**Sequencing approach: coroutine-driven director reading SO action lists.** Not Timeline
(a `PlayableDirector` binds tracks to specific scene objects, so every branch needs its own
`.playable` with its own bindings and custom `PlayableAsset` subclasses for branching — more
new API surface than 15 tiny action classes, and it would put mission content in `.playable`
assets instead of `Assets/GameData/`, contradicting `AGENTS.md`). Not hand-written per-scene
coroutines (that *is* the "large if/else chains" `AGENTS.md` forbids). Coroutines win: the
team already writes them (`GroqDialogue.GetConversation`, `ElevenLabsTTS`),
`yield return someIEnumerator` composes nested actions for free, and every new mission
becomes a new `.asset` with **zero new code**.

`MissionRunner : MonoBehaviour, IMissionContext` — `[DefaultExecutionOrder(-100)]`.
Fields: `autoStartMission`, `autoStartDelay` (must be > 0 so `PlayerController.SnapToGround()`
finishes first), `ui`, `cameraDirector`, `sfx`, `playerTransform`, `typewriterCharsPerSecond`.

`RunFrom(startNodeId)` is a `while` loop over node IDs switching on `MissionNodeKind`:
`Line`/`Thought` → `ShowLine`; `Cutscene` → `RunActions`; `Choice` → `ShowChoice`;
`Ending` → `ShowEnding`. Unknown node ID → `Debug.LogError` and stop.

- `RunActions` — sequential by default; `runInParallel` actions start immediately and are
  **joined before the next sequential action** (and at the end of the list). Document this
  in the `runInParallel` tooltip.
- `ShowLine` — `PlayerInputLock.Acquire(this)`, set the speaker's `IsTalking`, typewriter
  where the first `E` **completes the text instead of advancing**, then wait for `E`
  (or `autoAdvanceSeconds`).
- `ShowChoice` — `Acquire` + `PushCursorFree(this)` → cursor visible, camera already frozen
  → wait for a click → `PopCursorFree(this)` re-locks and hides.
- `ShowEnding` — stinger, `SaveService.RecordEnding`, show the card, wait for Retry/Continue.

**Retry: use the `pendingRestartId` route, not `StopAllCoroutines()` from inside the loop.**
The Retry button sets `pendingRestartId = mission.startNodeId`; `RunFrom` performs
`ResetWorld()` and loops naturally. This never stops a running coroutine from inside itself.

`ResetWorld()`: stop every `ActorMover`; `foreach (var a in MissionSceneRegistry.AllActors()) a.ResetToSpawn();`
`cameraDirector.HardRelease()`; `ui.HideAll()`; `PlayerInputLock.ResetAll()`; clear `flags`.

**Retry is an in-place reset, not a scene reload** because: the project has *zero*
`SceneManager` usage; `SampleScene` is 3 MB with the full `Worldcity` mesh, so a reload is a
multi-second black screen for a 9-year-old who just wants to try again; and
`PlayerController.Start()`'s raycast-dependent `SnapToGround()`/`AdjustCharacterControllerToModel()`
would re-run against whatever colliders happen to be loaded.

### Authored content — `Assets/GameData/Missions/Mission01_TheRoadHome.asset`

Speakers: `narrator` (no display name) · `aisha` (actorKey `aisha`) · `stranger` · `teacher` ·
`mother` · `mother_memory` (displayName "Mother", **no actorKey** — she isn't in the world yet).
All pronouns normalised to she/her (the source PDF mixes his/her).

**Scene 1 — School Ends**

| id | kind | content |
|---|---|---|
| `s1_open` | Cutscene | `SetPlayerControl(false)` · `Fade(black,0s)` · `Letterbox(on)` · `Teleport(aisha→m_aisha_start,snap)` · `CameraShot(m_cam_gate,0s)` · `PlaySound(school_bell)‖` · `Fade(clear,1.5s)` · `Wait(1.0)` |
| `s1_narr` | Line | narrator — "A warm Tuesday afternoon. School is over." |
| `s1_mother` | Line | mother_memory — "Come straight home after school, Aisha." |
| `s1_aisha` | Line | aisha — "Okay, Ammi." |
| `s1_setoff` | Cutscene | `CameraShot(m_cam_aisha_cu,1.0)` · `Wait(1.2)` · `Letterbox(off)` · `CameraRelease(0.8)` · `SetObjective("Walk home")` · `SetPlayerControl(true)` |
| `s1_walk` | Cutscene | `WaitForPlayerAtMarker(m_road_corner, r=3, ignoreY)` |

**Scene 2 — Someone Calls Her Name**

| id | kind | content |
|---|---|---|
| `s2_stop` | Cutscene | `SetPlayerControl(false)` · `SetObjective("")` · `Letterbox(on)` · `SetActorActive(stranger,true)` · `Teleport(stranger→m_stranger_spawn)` · `CameraShot(m_cam_meeting,1.0)` |
| `s2_call` | Line | stranger — "Hey! Aisha!" |
| `s2_turn` | Cutscene | `Move(stranger→m_stranger_call,walk)‖` · `Face(aisha→stranger,0.6s)` |
| `s2_confirm` | Line | stranger — "You are Aisha, right?" |
| `s2_yes` | Line | aisha — "Yes…" |
| `s2_think1` | **Thought** | aisha — "How does he know my name?" |
| `s2_thought_so` | Line | stranger — "I thought so." |
| `s2_like_mother` | Line | stranger — "You look just like your mother." |
| `s2_closer` | Cutscene | `Move(stranger→m_stranger_close,walk,face aisha)` · `CameraShot(m_cam_close,0.8)` |
| `s2_knows` | Line | stranger — "I know your mother, Lia." |
| `s2_work` | Line | stranger — "We work together." |
| `s2_surprised` | Cutscene | `AnimatorParam(aisha,"Fear",Trigger,hold 1.0)` |
| `s2_offer` | Line | stranger — "Actually, she asked me to help you get home today." |
| `s2_hesitate` | Cutscene | `AnimatorParam(aisha,"Sad",Trigger,hold 0.8)` · `CameraShot(m_cam_choice,1.0)` |
| `s2_think2` | **Thought** | aisha — "I don't know this person." |
| `s2_choice` | **Choice** | "What should Aisha do?" — A "Go with the stranger"→`a1` (Unsafe) · B "Walk away"→`b1` (Safe) · C "Find a trusted adult"→`c1` (Safest) |

**Path A** — `a1_okay` aisha "Okay." → `a2_good` stranger "Good choice." → `a3_walkoff`
Cutscene: `CameraShot(m_cam_end_a,1.0)` · `Move(stranger→m_patha_exit_stranger,walk)‖` ·
`Move(aisha→m_patha_exit,walk)` · `Fade(black,2.0)` → `a4_end` Ending `ending_unsafe`.

**Path B** — `b1_stepback` Cutscene: `Move(aisha→m_aisha_stepback,walkBackwards)` ·
`AnimatorParam(aisha,"Fear",hold 0.5)` → `b2` aisha "No thank you." → `b3` stranger "Your
mother asked me to help." → `b4` aisha "I need to go home." → `b5_leave` Cutscene:
`Move(aisha→m_home_path,run)` · `Fade(black,1.2)` → `b6_arrive` Cutscene:
`Teleport(aisha→m_home_door)` · `SetActorActive(mother,true)` · `Teleport(mother→m_mother_door)` ·
`CameraShot(m_cam_home_door,0s)` · `Fade(clear,1.0)` → `b7` mother "You're home!" → `b8`
narrator "Aisha tells her mother what happened. Her mother's expression turns serious." →
`b9` mother "You made a safer choice by walking away." → `b10` mother "But there was an even
better choice." → `b11` aisha "There was?" → `b12_end` Ending `ending_safe`.

**Path C** — `c1_look` Cutscene: `CameraShot(wide,0.8)` · `Face(aisha→m_teacher_stand,0.6)` →
`c2` narrator "Aisha sees her class teacher nearby." → `c3_walk` Cutscene:
`Move(aisha→m_aisha_at_teacher,run,face teacher)` · `CameraShot(m_cam_teacher,1.0)` → `c4`
aisha "Teacher, this man says he knows my mother." → `c5` teacher "Hello. Can I help you?" →
`c6_uncomf` Cutscene: `Face(stranger→aisha,0.4)` · `AnimatorParam(stranger,"Fear",hold 0.8)` →
`c7` stranger "Never mind." → `c8_flee` Cutscene: `Move(stranger→m_stranger_flee,run)` ·
`SetActorActive(stranger,false)` → `c9` narrator "The teacher stays with Aisha and calls her
mother." → `c10_arrive` Cutscene: `Fade(black,0.8,hold 0.4)` · `SetActorActive(mother,true)` ·
`Teleport(mother→m_mother_arrive_spawn)` · `CameraShot(m_cam_reunion,0s)` · `Fade(clear,0.8)` ·
`Move(mother→m_mother_arrive,run,face aisha)` → `c11` mother "You did exactly the right
thing." → `c12` mother "When you're unsure, always find a trusted adult." → `c13_relief`
Cutscene: `AnimatorParam(aisha,"Laugh",hold 1.2)` → `c14_end` Ending `ending_best`.

**Endings**

| id | quality | title | stars | completes | retry | continue | lesson |
|---|---|---|---|---|---|---|---|
| `ending_unsafe` | Unsafe | Unsafe Choice | 0 | no | yes | no | "Aisha went with someone she did not know. Never go anywhere with a stranger, even if they know your name or your family's names." |
| `ending_safe` | Safe | Safe Choice | 2 | no | yes | yes | "Walking away from a stranger is a good safety step, but this is not the safest choice." |
| `ending_best` | Best | Best Safety Choice | 3 | **yes** | yes | yes | "When something feels wrong or confusing, seek help from a trusted adult immediately. This is the safest choice." |

**Groq stays out of the critical path.** The two existing NPCs keep their untouched
`GroqDialogue` for ambient road-safety chatter during the `s1_walk` free-roam segment.
`MissionNode.speakAloud` defaults to **false** — mission lines are text-only unless opted in.

**Persistence** (`Assets/Scripts/Core/Save/`) — `MissionProgress` / `SaveFile` / `SaveService`,
Newtonsoft JSON (already a dependency via `GroqDialogue`) to
`Application.persistentDataPath + "/shinyminds_save.json"` (same location `ElevenLabsTTS`
already writes to). `RecordEnding` keeps the best stars, always increments attempts, ORs
`completed`. Gives the future parent dashboard a clean contract.

**Verify — this is the sign-off milestone.** The complete story is playable as pure text,
all three branches reachable, Retry from each ending returns to `s1_open`,
`shinyminds_save.json` appears with the right `bestEndingId`. **Get the narrative approved
here, before any staging work.**

---

## Phase 6 — Staging (scene work)

Everything goes into **two prefab instances plus four small component additions** — that is
the entire scene diff.

**New prefabs**

| Prefab | Contents |
|---|---|
| `Assets/Prefabs/Characters/Stranger.prefab` | `Ch29_nonPBR.fbx` (fallback `kaya.fbx`), Humanoid rig, `Animator(MissionActorAnimator)`, `MissionActor(actorKey="stranger")`, `ActorMover`, layer `CameraIgnore` |
| `Assets/Prefabs/Missions/MissionSystem.prefab` | `MissionRunner`, `CursorStateKeeper`, 2D `AudioSource`, child `CutsceneCamera` (Camera **disabled**, no AudioListener) + `MissionCameraDirector`, child `MissionUI` instance |
| `Assets/Prefabs/Missions/Mission01_Staging.prefab` | `Markers` (all `MissionMarker` empties) + an **inactive** `Stranger` instance |

**Root objects added to `SampleScene.unity`: exactly two** — `MissionSystem` and
`Mission01_Staging`, one `PrefabInstance` YAML block each.

**Existing objects modified (4 small override entries)**

| Object | Change |
|---|---|
| `GİRL 1` | add `MissionActor("aisha")`, `ActorMover` (CharacterController wired), `PlayerLockBinder` |
| `Teacher` | assign `MissionActorAnimator` (currently has **none**), add `MissionActor("teacher")`, `ActorMover`; layer `CameraIgnore` |
| `Mother` | same, `actorKey="mother"`; **set inactive at start** |
| `CameraHolder` | set `CameraCollision.blockers` to exclude `CameraIgnore` |

Also: **delete one of the two duplicate `BackgroundMusic` GameObjects.**

**Markers** (inside `Mission01_Staging`) — `m_aisha_start`, `m_road_corner`,
`m_stranger_spawn`, `m_stranger_call`, `m_stranger_close`, `m_stranger_flee`,
`m_aisha_stepback`, `m_patha_exit`, `m_patha_exit_stranger`, `m_home_path`, `m_home_door`,
`m_mother_door`, `m_teacher_stand`, `m_aisha_at_teacher`, `m_mother_arrive_spawn`,
`m_mother_arrive`, plus camera poses `m_cam_gate`, `m_cam_aisha_cu`, `m_cam_meeting`,
`m_cam_close`, `m_cam_choice`, `m_cam_end_a`, `m_cam_home_door`, `m_cam_teacher`, `m_cam_reunion`.

> **Never prefix a marker with `Waypoint`.** The Hierarchy already returns ~325 hits for that
> string, and `CarAI.waypoints` is a serialized `Transform[]` — one mis-drag creates a car
> that drives to a cutscene mark, and nothing in code will tell you. The `m_` prefix keeps
> markers findable.

**Tags: no changes.** `TagManager.asset` stays at `tags: []`; everything resolves through
`MissionSceneRegistry` keys and the only tag check uses the built-in `Player` tag.

**Layers: add one.** Set the first free user layer (verify index 6 is empty) to
`CameraIgnore`; put `Stranger`, `Teacher`, `Mother` and the two `InteractionZone` triggers
on it; set `CameraCollision.blockers = ~(1 << CameraIgnore)`. Without this the camera slams
into Aisha's back the instant the Stranger stops behind her.

**Verify:** every beat staged — bell + gate intro, Stranger approach, Path A walk-off, Path B
doorstep, Path C teacher + flee + reunion. Camera never clips through Aisha or the Stranger.
Retry mid-cutscene fully resets actor positions.

---

## Phase 7 — Animation, audio, polish

**Prerequisite, do this first:** set *Rig ▸ Animation Type = Humanoid* on `Teacher.fbx`,
`Mother.fbx`, `Ch29_nonPBR.fbx` (reuse the existing `Assets/New Human Template.ht`).
Re-importing an FBX with a changed rig can **drop `m_AddedComponents` overrides** on its
scene instances — so do it *before* adding `MissionActor`/`ActorMover` in the scene.

- **`PlayerAnimator.controller`** — add triggers `Fear`, `Sad`, `Sit`, `Laugh`. Wire
  `Any State → fear/sad/sit`, plus a new `laugh` state from `Laughing.fbx`. Each emote → `idle`
  with *Has Exit Time* ≈ 0.9, transition 0.25s. **Add `Speed < 0.1` as a second condition on
  every Any-State emote transition** so a stray trigger can't interrupt walking during free
  roam. Uncheck *Can Transition To Self*.
- **New `Assets/Animations/Controllers/MissionActorAnimator.controller`** — params `Speed`
  (float), `IsTalking` (bool), triggers `Fear`/`Sad`/`Laugh`. Base layer = 1D blend tree on
  `Speed` (0→`Look Around`, 2→`Walking`, 6→`Slow Run`); `IsTalking`→`Talking (1)`; Any-State
  emotes as above. Assign to `Stranger`, `Teacher`, `Mother`.
- **Audio to source** — `Assets/Audio/Ambient/` and `Assets/Audio/UI/` are empty. Needed:
  `school_bell.wav`, `ui_click.wav`, `ending_good.wav`, `ending_bad.wav`.
- Letterbox, typewriter, fades, objective HUD final pass.

**Verify:** `fear`/`sad`/`laugh` fire and return to idle cleanly; bell audible over the
(now single) background music; no emote leaks into free roam.

---

## Phase 8 — Docs

Per `AGENTS.md`, create `docs/mission_system.md`, `docs/npc_system.md`,
`docs/folder_structure.md`. Note as future work: the `.asmdef` question, and that the
**Groq and ElevenLabs API keys are committed in plaintext inside `SampleScene.unity`** and
should be rotated and extracted to a gitignored config asset. The mission system must not
add any new keys.

---

## Gotchas

1. **Moving a `.cs` without its `.cs.meta`** nulls every scene reference. The #1 way to destroy this project.
2. **`[SerializeReference]` type renames null stored actions.** Lock the namespace before authoring; `[MovedFrom]` for later renames.
3. **Scale mismatch** — `ActorMover` must multiply speed by `lossyScale.y`.
4. **`CharacterController` ignores `transform.position` writes** while enabled — teleport and retry must disable/re-enable it.
5. **`SnapToGround()` only runs in `Start()`** — teleports need an explicit `SnapDown()`.
6. **`CameraCollision`'s unmasked, trigger-inclusive `SphereCast`** — fix in Phase 1 or the Stranger breaks the camera.
7. **`MapToggle` disables the Main Camera GameObject**, taking the AudioListener — lock it out during missions and force the GameObject active in `TakeOver()`.
8. **Cursor lock drops on focus loss** — `CursorStateKeeper` is required.
9. **Never `GameObject.Find`** — 3 `GameManager`, ~325 `Waypoint (n)`.
10. **Static registries need `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]`** or "Disable Domain Reload" leaves a permanently frozen player.
11. **`ElevenLabsTTS` builds JSON by string concatenation** and always overwrites one `voice.mp3` — any `"` in a line breaks it, and overlapping calls race on the file. Keep `speakAloud = false`; if enabling it, first switch to `JsonConvert.SerializeObject` and per-call filenames.
12. **TMP emoji render as tofu** — `MissionEnding.badge` is a `Sprite` for exactly this reason.
13. **`activeInputHandler: 2` is load-bearing** — switching to New-only breaks all 14 gameplay scripts at once.

---

## End-to-end verification

1. **Phase 0** — `git diff --stat` shows renames only; `SampleScene.unity` absent; no "Missing (Mono Script)"; all existing gameplay still works.
2. **Phase 1** — Groq NPC dialogue no longer walks-in-place or loops footsteps; `E` never double-fires.
3. **Phase 5** — full story playable as text, all three branches, Retry works, save file written. *Narrative sign-off gate.*
4. **Phase 6** — full staged playthrough of each branch; camera clean; mid-cutscene Retry resets everything.
5. **Phase 7** — emotes, audio, polish.
6. **Regression each phase** — walk/run/jump, camera, minimap, `M` map, school door, both Groq NPCs, car traffic.
