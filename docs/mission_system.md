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

### The on-screen controls

`ShinyMinds/Mobile/Rebuild Touch Controls` builds them. The stick and both buttons are **black
frosted glass**: a smoked disc, a mottled milky frost, a black bezel, an etched gauge circle and
four chevrons. Two rules keep it matte, and both were learnt by breaking them:

- **No value is lighter than what is behind it.** The frost is tinted black rather than white for
  that reason — a light haze is the obvious way to draw ground glass and it works over the road,
  but over anything pale it reads as a smear of polish across the disc.
- **Nothing is drawn outside the base circle.** No halo, no bloom, no contact shadow. A soft edge
  spilling onto the road reads as a glow around the control however dark it is, so the bezel
  carries the edge alone.

Every piece is a *generated white shape tinted at the Image*, so the whole control recolours from
the six `Color`s at the top of `MobileControlsBuilder`. `JoystickSpriteFactory` draws the rings,
the chevron, the frost noise and the button glyphs from a handful of numbers; the discs come from
the same
`UI_Ellipse.png` the mission UI uses. Change a thickness or a falloff and delete the PNG in
`Assets/Art/UI` to see it, the same as `CornerRadius`.

Jump and Run carry **icons** rather than JUMP / RUN captions, and sit **diagonally**: Jump in the
corner with Run one step up and in, along the arc a right thumb sweeps.

The icons are `Assets/Art/UI/Icon_Jump.png` and `Icon_Run.png`, rasterised at 512px from the
supplied `jump.svg` / `run.svg` — Unity has no SVG importer without `com.unity.vectorgraphics`, so
the SVGs at the project root are inert. Each is a finished badge: a grey disc inside a ring with
the figure knocked out of the disc.

**They go on the button exactly as drawn** — the whole badge at the button's own size, tinted pure
white so the artwork's greys come through untouched, and with no keyline added. A keyline would be
a change to the design, and the design is not ours to change. The ring lands on the button's rim,
and because the figure is negative space the frosted glass shows through it, which is what keeps
the figure legible over both a pale wall and the dark road.

If either PNG goes missing the builder falls back to a generated line-art glyph and says so in the
Console. That fallback *is* white with a black keyline — a bare line-art glyph in one flat colour
would vanish against one background or the other.

**The stick moves her; it never moves the camera.** It drives four directions — forward, back,
left, right — read against the camera's heading, and she turns to face the one she is walking in
(`faceTurnSpeed` on `PlayerController`, separate from the keyboard's on-the-spot `turnSpeed`). The
camera angle belongs to the look gesture alone, which took two fixes to hold:

- **`CameraHolder` is a child of the girl**, so it inherited her rotation: transform parenting
  carried the camera round as she faced each new walking direction, which no amount of input
  gating could stop. The rig now owns its heading in world degrees (`yaw`) and writes
  `transform.rotation` **every frame, including while input is locked**, discarding what the
  parent contributed. Her rotation moves the camera's *position* — the holder sits at her origin,
  which is what makes it follow her — and never its direction. Anything reaching for
  `transform.Rotate` in that script is a bug, and so is putting that write behind the lock guard:
  a cutscene that turns her to face someone would then swing the player's view a half-turn with
  her, which is what `Face("aisha", "stranger")` did in the stranger meeting.
- `CameraController` zeroes Mouse X/Y while `MobileInput.StickDragging` is true. On mobile, legacy
  Input reports the primary touch as the mouse, so without this a thumb on the stick arrives as
  mouse delta and swings the view while she walks.
- The auto-centre that swings the rig round behind her is keyboard-only now. The stick used to
  count as movement there so a stick turn dragged the view along — with the stick no longer
  turning her on the spot, counting it would hand it the camera by the back door.
- On unlock, `yaw` re-syncs to her heading, so a cutscene that walked or turned her elsewhere
  doesn't hand control back with the camera pointing at where she used to be facing.

Nothing is opaque — the road shows through every layer, which is what makes it read as glass.
The frost is a *material* effect, not a background blur: it does not refract what is behind it.
A true blur would mean sampling `_CameraOpaqueTexture` from a custom UI shader, which needs
**Opaque Texture** enabled on the URP asset and does not survive a Screen Space Overlay canvas
in every URP version — worth doing only if the frost alone proves not enough.

None of this touches input: `TouchJoystick` still owns the drag and `MobileInput` still samples
it. The layers all have **Raycast Target off** — only the base disc catches the finger, because
a child that swallowed the pointer would cancel the drag under it.

## Rotations without zooms

A shot change moves the camera in two ways at once, and only one of them is usually wanted. The
angle is a rotation; the *distance* is a zoom. A blend from a wide (≈9 m out) to a close-up
(≈1.6 m) is the lens travelling 7 m down its own view axis, which reads as a zoom in however
smooth it is.

Two things keep a sequence to rotation alone:

- `FramedShotAction.fixedDistance` overrides the distance each shot type would have picked, so
  Wide / TwoShot / OverShoulder / CloseUp become *directions* on one orbit. Mission 01's stranger
  meeting sets all five to `OrbitRadius` (5.5 m).
- `ShotToPose` takes an `orbitPivot` — the aim point — and slerps the camera's arm around it
  instead of lerping straight across. Two poses at the same radius then hold that radius through
  the entire blend. Without it the straight line cuts the chord and dips the camera closer
  mid-blend, which is a small zoom in and out again.

The price is real and worth stating: at a fixed radius a "close-up" is not close. Getting close
*is* moving the camera in. `OrbitRadius` trades intimacy against how much of the street stays in
frame.

## Parallel means parallel; sequential means "after everything"

`RunActions` joins **every pending parallel coroutine before it starts a sequential action**. So
two actors only move together if *both* their moves are marked `Parallel`. Mark one and the other
waits for the first to finish its entire walk — which is how Path A had the Stranger stroll 33 m
off-screen alone while Aisha stood at the kerb, reading as her refusing to go with him. Put the
`Fade` or the next beat after them as a *sequential* action and it still waits for both, which is
usually what you want.

## A missing actor is silent by design, so it warns now

Every consumer of an actor key degrades rather than failing: `MoveActorAction` becomes a no-op, a
`Framed` shot on `["aisha", "teacher"]` frames Aisha alone, and a speaker whose `actorKey` does not
resolve has their line fall back to the narrator's subtitle bar. That is deliberate — it keeps the
story playable — but it made an entirely absent character look like a directing choice. **Neither
the Teacher nor the Mother was in the scene at all**, only their markers, so Path C played with an
invisible teacher.

Two warnings now cover it: `FramedShotAction` reports a *partly* resolved subject list (it only
warned when none resolved), and `MissionRunner.SpeakerBody` reports a speaker that names an actor
the scene does not have, once per speaker. `MissionCharacterBuilder.EnsureTeacherAndMother()` places
both from the imported-but-unused `Teacher.fbx` / `Mother.fbx`, and runs inside **Wire Open Scene**
just before `WireOtherActors` does the component wiring.

## Why characters float

Unity's Humanoid retargeting seats the body from the Avatar's proportions, and for a Mixamo rig
(FBX root exported at hip height) the rendered body can sit well above the GameObject origin. The
residual **scales with the actor**, so a scale-5 girl floats five times as far as a scale-1 one.
Grounding the origin is therefore not the same as grounding the character, and disabling the
Animator only appears to fix it because the bones fall back to the bind pose — where the origin
really is at the feet.

`ActorMover` corrects this every frame (`RenderedFootLift` + `SnapDown`) for anyone who has one.
**The memory stand-ins do not have one** — they are a Transform and an Animator — so nothing
grounded them and the scale-2 Mother hung above the diorama floor against its sky backdrop.
`MemoryStage.GroundStandIns()` now does it when the memory opens.

Both places measure over a short settling window and keep the **smallest** lift seen, never the
live one: the retargeting residual is constant, the clip's own vertical motion is not, and
correcting by the live value flattens an animation into a glide. Neither can measure on frame one,
because until the Animator has evaluated, the bounds still describe the bind pose.

## Who owns an actor's speed

`ActorMover`'s authored speeds are for a roughly 1-unit rig and are **multiplied by the actor's
scale**, so a bigger actor's longer stride matches its travel. Right for the mission-only actors
(Stranger, Teacher, Mother — all scale 2).

Wrong for the player, and badly: GIRL 1 is **scale 5**, so 3.5 m/s run became 17.5 m/s while
`PlayerController` moves the same body at 6 m/s. Path B's 20 m walk home was over in about a
second. `ActorMover.SpeedFor` now defers to `PlayerController`'s own walk / run / backward speeds,
unscaled, whenever one is on the object — one character, one speed, whoever is driving.

The residual is visible if you look: at 6 m/s a scale-5 rig's feet slide, because the stride wants
17.5. They slide identically under player control, which is the point.

## Layers are for raycasts, not for hiding

`CameraIgnore` and `Vehicle` exist so *physics queries* skip things: camera collision must not
slam forward when a character stands behind the player, and `ActorMover`'s ground ray must not
hit a passing car roof. Neither is a rendering instruction.

But **a layer minted after a camera's culling mask was set is absent from that mask**, silently.
That is how the Stranger — whose entire 67-object hierarchy is `CameraIgnore` — became invisible
to the main camera, along with all 20 traffic objects on `Vehicle`. Only the cutscene camera, whose
mask is "everything", ever showed him, so the bug hid wherever a mission took the camera over and
appeared everywhere it did not.

`MissionSceneSetup.FixCameraCulling()` ORs both layers back in, and runs from **Wire Open Scene**
and from **Put Traffic On The Vehicle Layer**. `MemoryStage` is deliberately excluded: the diorama
belongs to `MemoryCamera` alone.

## Open world, not a cutscene on rails

Nothing starts on load. `MissionRunner.autoStartMission` is deliberately **null** in both the
prefab and the scene instance, and the mission is entered like anything else in the city:

1. The player walks to the school gate and enters `MissionOffer_School` — a 7 m sphere trigger on
   `m_aisha_start`, about 20 m from where they spawn.
2. `MissionTrigger` shows the banner: mission title, objective, and `"<AdvanceLabel> to begin"` —
   so it reads "Press E" on a keyboard and "Touch" on a phone, from the same source as every other
   prompt.
3. It accepts on `InteractKey.TryConsumeWorld()`, at execution order **−60** so an accept beats the
   NPC zones (−50) and doors (0) sharing that doorstep. Walk away and the offer withdraws.
4. When the mission ends and the player takes **Continue**, `MissionRunner` clears the objective,
   releases the input lock and hands the camera back to the follow rig — free roam again. Every
   ending sets `allowContinue = true` for that reason; a retry-only ending would trap them on the
   summary screen.
5. The zone re-arms on exit, so a finished mission can be replayed by walking back. `rearmSeconds`
   (1.5 s) stops the banner reappearing under the player's finger as the summary closes.

`startImmediately = true` restores the old walk-in-and-it-takes-over behaviour for a mission that
is meant to ambush.

## The summary screen

`Ending` nodes show `MissionEndingCard` over a scrim: badge, outcome title coloured by
`EndingQuality`, the stars earned, the lesson, a progress line, and **Try Again** / Continue.

- The **stars are art**, not `★` characters — TMP's default LiberationSans atlas has no glyph for
  those and renders them as empty boxes, the same reason `MissionEnding.badge` insists on a Sprite.
  `UI_Star.png` is generated as a ten-vertex polygon with supersampled point-in-polygon coverage,
  and the row tints rather than swaps: earned stars gold, the rest grey, so an unearned star still
  holds its place.
- The **progress line** is what makes it a summary rather than a result. `MissionRunner` calls
  `SaveService.RecordEnding` *before* showing the card, so `attempts` already counts this run and
  `bestStars` is current — the card reads them itself via `GetProgress(missionId)`, which is why
  `ShowEnding` takes the mission id.
- **Try Again is the primary button** (accent fill, white label) with Continue as quiet paper. A
  safety lesson the player got wrong is exactly the case where retrying is the point, and
  `MissionEnding.allowRetry` / `allowContinue` still decide which buttons exist at all.

The scrim's Raycast Target is **off**, like `FadeOverlay`: a full-screen raycast target under
`InputSystemUIInputModule` swallows every click and the card's own buttons stop responding.

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
