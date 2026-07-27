# Mission System

How missions work in ShinyMinds. Read this before touching `Assets/Scripts/Missions/`.

## The one-sentence version

A mission is a **ScriptableObject** holding a flat graph of nodes; `MissionRunner` walks
that graph with a coroutine and stages it in the existing 3D scene. **Adding a mission
means adding an asset, not adding code.**

## Layout

```
Assets/Scripts/
  Core/
    PlayerInputLock.cs        who is allowed to control the game + cursor state
    PlayerLockBinder.cs       applies the lock to the player rig (on GIRL 1)
    CursorStateKeeper.cs      re-asserts cursor lock after alt-tab
    InteractKey.cs            the only place gameplay E is read
    Save/                     MissionProgress, SaveFile, SaveService

  Missions/
    Data/                     pure data — no scene knowledge
      MissionData.cs          the ScriptableObject
      MissionNode.cs          Line / Thought / Cutscene / Choice / Ending
      MissionChoice.cs, MissionEnding.cs, SpeakerProfile.cs
      IMissionContext.cs      the only surface a CutsceneAction may touch
      CutsceneAction.cs       abstract base
      Actions/                15 concrete actions, one file each

    Runtime/                  scene-side
      MissionSceneRegistry.cs string key -> scene object
      MissionActor.cs         on GIRL 1 / Teacher / Mother / Stranger
      MissionMarker.cs        a named position + facing
      ActorMover.cs           walking without NavMesh
      MissionCameraDirector.cs cutscene camera
      MissionRunner.cs        the director
      MissionTrigger.cs       start a mission on walk-in

    UI/
      MissionUIView.cs        the one shared UI (pure view)
      TypewriterText.cs, MissionChoiceButton.cs, MissionEndingCard.cs, ObjectiveHud.cs

    Editor/
      Mission01Builder.cs     generates Mission01_TheRoadHome.asset

Assets/GameData/Missions/     the authored mission assets
```

## Node kinds

| Kind | What it does | Advances on |
|---|---|---|
| `Line` | Someone speaks, shown in the dialogue panel | `E` (or `autoAdvanceSeconds`) |
| `Thought` | Internal monologue, thought-bubble panel | `E` |
| `Cutscene` | Runs an ordered `actions` list | when the list finishes |
| `Choice` | Mouse-clickable buttons | a click |
| `Ending` | The result card | Retry or Continue |

Every node has a `nextId`. `Choice` nodes route through the picked `MissionChoice.nextId`
instead. An empty `nextId` stops the mission.

## Why the serialization is split

**Story graph = flat list + string ids, no polymorphism.** Every narrative node is
structurally the same thing ("present something, then go to an id"); only which *fields*
are filled varies. Mission 01 alone is ~55 nodes, and `[SerializeReference]` stores the
concrete type by assembly + namespace + class name — a rename or re-namespace **nulls
every stored instance with no undo**. String ids also survive reordering and read well in
a diff.

**Cutscene actions = `[SerializeReference]`.** Here the variation genuinely is
behavioural: `MoveActorAction` and `FadeScreenAction` share nothing, and each carries its
own `Execute` coroutine. Unity 6 renders a type-picker dropdown for managed-reference list
elements with no custom editor code.

Rules that keep the `[SerializeReference]` risk contained:

1. **Never change `namespace ShinyMinds.Missions.Data`** on an action class.
2. One class per file, filename == class name.
3. Add `[MovedFrom(true, sourceNamespace: …, sourceClassName: …)]` *before* renaming one.

## Why coroutines, not Timeline

Timeline is a scene-authoring tool: a `PlayableDirector` binds tracks to specific scene
objects, so every branch would need its own `.playable` asset with its own bindings, and
branching mid-timeline needs signal emitters plus custom `PlayableAsset` subclasses. That
is more new API surface than the 15 tiny action classes, and it would put mission content
in `.playable` assets instead of `Assets/GameData/`.

Coroutines win because `yield return someIEnumerator` composes nested actions for free,
the project already uses them (`GroqDialogue.GetConversation`, `ElevenLabsTTS`), and a new
mission needs no code at all.

## Why string keys instead of direct references

ScriptableObjects cannot hold scene references, and `GameObject.Find` is unusable in this
project: `SampleScene` has ~325 objects named `Waypoint (n)` and **three** named
`GameManager`. `MissionActor` and `MissionMarker` register themselves with
`MissionSceneRegistry` in `Awake()`, and actions look actors and markers up by key.

**Never name a mission marker `Waypoint …`** — `CarAI.waypoints` is a serialized
`Transform[]`, so a mis-drag would silently send a car to a cutscene mark. Use the `m_`
prefix.

## The input lock

`PlayerInputLock` is refcounted by owner object, so a mission and a Groq NPC conversation
can never unlock each other. `PlayerLockBinder` (on `GIRL 1`) does three things on lock,
not one:

1. Disables `PlayerController`, `CameraController`, `MapToggle`, `footstepaudio`.
2. Stops the footstep `AudioSource` — `footstepaudio` only calls `Stop()` in its
   `else` branch, so disabling it mid-stride leaves the loop playing forever.
3. Zeroes `Speed` / `TurnLeft` / `TurnRight` / `Backward` — `PlayerController` writes those
   every `Update`, so a disabled controller freezes them and the character walks in place.

Points 2 and 3 were both visible in the pre-existing Groq NPC dialogue.

`CameraCollision` stays **enabled** during a lock; it is `LateUpdate` positional only.

### Cursor

Cursor ownership belongs to `PlayerInputLock`, not `CameraController`. `ShowChoice` calls
`PushCursorFree`/`PopCursorFree` around the decision so the player clicks with the mouse
while the camera stays frozen. `CursorStateKeeper.OnApplicationFocus` re-asserts the lock
because Unity drops it on alt-tab.

## The E key

`InteractKey` is the only place gameplay `E` is read, and it allows at most one consumer
per frame. Priority comes from `[DefaultExecutionOrder]`:

| Order | Consumer | Method |
|---|---|---|
| −100 | `GroqDialogue`, `MissionRunner` | `TryConsumeUI()` — not blocked by the lock |
| −50 | `NPCInteraction` | `TryConsumeWorld()` — blocked while locked |
| 0 | `DoorController` | `TryConsumeWorld()` — blocked while locked |

## Retry

`MissionRunner` sets `pendingRestartId` and lets `RunFrom` loop naturally — it never stops
a coroutine from inside itself. `ResetWorld()` puts every registered actor back to the
pose captured in `MissionActor.Awake()`.

Retry is an **in-place reset, not a scene reload**: the project has zero `SceneManager`
usage, `SampleScene` is 3 MB with the whole city mesh, and `PlayerController.Start()` does
raycast-dependent ground snapping we don't want re-running.

`MissionActor.ResetToSpawn()` must disable the `CharacterController` around the move —
a `CharacterController` silently reverts direct transform writes — then `Rebind()` the
animator to clear any latched `Fear` / `Sad` / `Laugh` trigger.

## Scale

Mixamo clips are authored for a ~1-unit rig with root motion off, but `GIRL 1` is scale 5
and `Teacher`/`Mother` are scale 2. `ActorMover` multiplies world speed by
`transform.lossyScale.y` for this reason. `PlayerController.walkSpeed = 3` already has the
uncorrected version of this bug — don't copy it.

## The camera

`Main Camera` is a grandchild of `GIRL 1`, so moving the player during a cutscene would
drag it, and `CameraCollision` would fight any pose we set. `MissionCameraDirector` uses a
separate root-level `CutsceneCamera` and toggles the **Camera component only** — never the
GameObject, which carries the `AudioListener`. Exactly one Base camera renders at a time,
so no URP camera stacking is involved.

`TakeOver()` force-activates the Main Camera GameObject first, because `MapToggle` (M key)
deactivates it.

## Saving

`SaveService` writes `shinyminds_save.json` to `Application.persistentDataPath` with
Newtonsoft (already a dependency via `GroqDialogue`). Attempts always increment; stars and
`bestEndingId` only improve, so replaying a worse branch never erases a good result.

## Known issues to fix later

- **Groq and ElevenLabs API keys are committed in plaintext inside `SampleScene.unity`.**
  They should be rotated and moved to a gitignored config asset. The mission system
  deliberately adds no new keys.
- `ElevenLabsTTS.GenerateSpeech` builds its JSON by string concatenation and always
  overwrites a single `voice.mp3`, so a quote character breaks it and two overlapping calls
  race on the file. `MissionNode.speakAloud` is therefore **false** by default; fix the TTS
  path before enabling it.
- No `.asmdef`. Adding one requires moving every script *and* an Editor asmdef in the same
  commit, because asmdef assemblies cannot reference `Assembly-CSharp`.
