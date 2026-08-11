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
    InteractKey.cs            the only place the advance input (E / tap) is read
    TouchInput.cs             is this a touch device, and did a finger just land
    Save/                     MissionProgress, SaveFile, SaveService

  Missions/
    Data/                     pure data — no scene knowledge
      MissionData.cs          the ScriptableObject
      MissionNode.cs          Line / Thought / Cutscene / Choice / Ending / Memory
      MissionChoice.cs, MissionEnding.cs, SpeakerProfile.cs
      IMissionContext.cs      the only surface a CutsceneAction may touch
      CutsceneAction.cs       abstract base
      Actions/                15 concrete actions, one file each

    Runtime/                  scene-side
      MissionSceneRegistry.cs string key -> scene object
      MemoryStage.cs          the flashback diorama + its render camera
      MissionActor.cs         on GIRL 1 / Teacher / Mother / Stranger
      MissionMarker.cs        a named position + facing
      ActorMover.cs           walking without NavMesh
      MissionCameraDirector.cs cutscene camera
      MissionRunner.cs        the director
      MissionTrigger.cs       start a mission on walk-in

    UI/
      MissionUIView.cs        the one shared UI (pure view)
      MemoryBubble.cs         one speech balloon inside the memory box
      MemoryBubbleAnchor.cs   sizes the memory oval and aims its dots at its owner
      WorldSpeechBubble.cs    the balloon that tracks a speaker's head in the world
      TypewriterText.cs, MissionChoiceButton.cs, MissionEndingCard.cs, ObjectiveHud.cs

    Editor/
      Mission01Builder.cs     generates Mission01_TheRoadHome.asset

Assets/GameData/Missions/     the authored mission assets
```

## Node kinds

| Kind | What it does | Advances on |
|---|---|---|
| `Line` | Someone speaks. A speech balloon over their head if they have a body in the scene, otherwise the subtitle bar | `E` (or `autoAdvanceSeconds`) |
| `Thought` | Internal monologue, played in the memory bubble with the stage switched off | `E` |
| `Cutscene` | Runs an ordered `actions` list | when the list finishes |
| `Choice` | Mouse-clickable buttons | a click |
| `Ending` | The result card | Retry or Continue |
| `Memory` | A line the speaker is *remembering*, played inside the memory bubble | `E` |

Every node has a `nextId`. `Choice` nodes route through the picked `MissionChoice.nextId`
instead. An empty `nextId` stops the mission.

## Speech balloons

Every `Line` whose speaker has a body in the scene is drawn as a balloon over that
speaker's head — white paper, black keyline, dark text, a tail pointing down at them. The
subtitle bar at the bottom of the screen is now only for the narrator, who has nobody to
hang a balloon on.

`WorldSpeechBubble` projects the speaker's head into canvas space every `LateUpdate`,
clamps the balloon inside the screen edges, and slides the tail along the balloon's
underside so it keeps pointing at the speaker even when the balloon has been pushed back
into view. It must be given the camera **currently drawing the world**, which is
`MissionCameraDirector.ActiveCamera` — for the length of a cutscene the main camera is
switched off and the cutscene camera is somewhere else entirely, so `Camera.main` would put
the balloon over thin air.

Every balloon, panel and button is drawn with one generated 9-sliced sprite
(`UI_RoundedRect.png`, `CornerRadius` in `MissionUIBuilder`), so the corner radius is the
same 24px on an 86px choice button and on a 620px ending card, and changing it is one
constant.

`Thought` nodes get no balloon over anyone's head — see below, they are played in the
memory bubble instead.

## Memory nodes

A `Memory` node is a `Line` that is not happening now. The world dims, a warm oval swells up
in the top-right corner, and inside it two stand-ins act the remembered exchange out while
their words appear in storybook speech balloons over their heads — white paper, dark text, a
tail pointing at the speaker, and no name label, because the tail already says who is
talking. Three shrinking dots step diagonally out from the oval's lower-left arc, back
towards the girl below it who is doing the remembering.

- The speaker's `SpeakerProfile.memorySide` (`Left` / `Right`) picks which stand-in speaks
  and which balloon the line lands in. A memory speaker's `actorKey` stays empty — nobody
  in the playable world moves or talks during a memory.
- **Consecutive nodes of the same kind** share one bubble: it opens on the first and closes
  after the last, and the previous balloon stays on screen dimmed so the pair reads as one
  conversation. Keep a remembered exchange contiguous in the graph. A change of kind swaps
  what is inside the oval, so `Memory` → `Thought` closes and reopens.
- What is inside the bubble is a live render, not art: `MemoryStage` (a camera and two
  stand-ins parked 600 m below the city on their own layer) draws into
  `Assets/Rendering/MemoryStage.renderTexture`, which the bubble displays. It is switched
  on only while a memory is open.
- With no `MemoryStage` in the scene, a `Memory` node degrades to an ordinary line in the
  dialogue bar rather than opening an empty box.
- The staging above is fixed, not computed: `MemoryBubbleAnchor` parks the oval in the corner
  at 860×580 and puts the dots back exactly where the prefab placed them. Mission 01's memory
  plays at the school gate with the girl standing still below it, so the authored trail
  already points at her. Only a `Thought` follows its owner — see below.

Mission 01 uses this for the one exchange that was never happening at the school gate:
`s1_mother` / `s1_aisha`, the instruction Aisha's mother gave her that morning.

## Thought nodes

A `Thought` is the same bubble with nobody in it. It is not happening now either — it is
Aisha's own voice — so it gets the dimmed world, the same warm oval and the same trail of
dots, but the masked `Frame` holding the render is switched off and `MemoryStage` is never
opened, so its camera renders nothing. The line lands in a single balloon centred in the
paper the picture would have filled: same white paper and black keyline as everywhere else,
italic, and no tail, because a tail points at a speaker and there is nobody in the oval to
point at.

The oval is **440×250** for a thought against 860×580 for a memory — one balloon does not need
the room a 16:9 render and two stand-ins do. Both sizes live on `MemoryBubbleAnchor`, which is
why the continue prompt is anchored at a fraction of the oval's height and the `ThoughtBalloon`
is 320 wide: everything inside has to survive the resize, and an ellipse cuts the corners
first. Shrink `thoughtSize` in the Inspector and the balloon's width has to come down with it.

Unlike a memory, a thought **follows the person thinking it**. `MemoryBubbleAnchor` projects
the owner's head every `LateUpdate`, leans the oval to whichever side of the head faces screen
centre, clamps it inside the screen edges, and lays the dots along the line from the oval's rim
to the head — an ellipse, so the rim is not simply half the width. A thought happens wherever
Aisha happens to be standing, so a trail authored to one corner would point at empty tarmac as
soon as she moved. With nobody on screen to point at — her head behind the cutscene camera —
the oval parks in the corner and the dots are hidden, because a trail with nothing on the end
of it is worse than no trail.

`MissionRunner` names the owner: the speaker's body if they have one, otherwise
`playerTransform`.

`MissionRunner` decides this per node (`isThoughtBubble`) and `MissionUIView.OpenMemory`
takes `withStage` — the picture has to be off *before* the oval swells, or the opening
frames flash the last memory's still. A prefab built before `ThoughtBalloon` existed has no
balloon to put the line in, so `HasMemoryThought` is false and the old centred
`ThoughtPanel` plays it instead; re-run **ShinyMinds/Setup/1. Build Mission UI Prefab** to
get the bubble.

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

`InteractKey` is the only place the gameplay advance input is read, and it allows at most
one consumer per frame. Priority comes from `[DefaultExecutionOrder]`:

| Order | Consumer | Method |
|---|---|---|
| −100 | `GroqDialogue`, `MissionRunner` | `TryConsumeUI()` — not blocked by the lock |
| −50 | `NPCInteraction` | `TryConsumeWorld()` — blocked while locked |
| 0 | `DoorController` | `TryConsumeWorld()` — blocked while locked |

## Touch

On a phone or tablet there is no `E`, so `TouchInput` makes a tap count as the same press
and `InteractKey.TryConsumeUI()` accepts either. Both go through the one frame guard above,
so a tap can no more double-advance than a key can.

A tap anywhere on the play area advances, rather than one small hit box: while a line is on
screen the lock has already hidden the stick and the buttons, so a thumb landing anywhere
can only mean "next". `TouchInput` ignores taps that land on a control handling its own
pointer — the stick, Jump, Run, a choice button — which is what keeps a drag or a button
press from also advancing the line. Plain panels and speech balloons are not controls, so
tapping the dialogue box itself works.

Every prompt reads its wording from `InteractKey.AdvanceLabel` (`"Press E"` / `"Touch"`) so
the screen can never name an input the device does not have. `MissionRunner` carries the two
strings as `continuePromptText` and `touchContinuePromptText`. To check the touch wording and
tap-to-advance on desktop, tick **simulateTouchOnDesktop** on `MobileControls`' `MobileInput`
— that also makes a mouse click advance.

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
