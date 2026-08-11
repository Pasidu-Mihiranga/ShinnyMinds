# Mission 01 — Unity Editor setup

All the C# is written and compiles. This is the Editor work that cannot be done from
outside Unity.

## Quick start — one menu click

Open `SampleScene`, then run **ShinyMinds ▸ Setup ▸ Run All Steps**.

Then **save the scene (Ctrl+S)** and press **Play**.

That runs the whole pipeline in dependency order:

| Step | Menu | Builds |
|---|---|---|
| — | **ShinyMinds ▸ Build Mission 01** | the mission asset — 55 nodes, 3 branches, 3 endings |
| 1 | **Setup ▸ 1. Build Mission UI Prefab** | `MissionUI.prefab` — ~25 objects, every field wired |
| 2 | **Setup ▸ 2. Build Mission System Prefab** | `MissionSystem.prefab` — runner, cutscene camera, sfx, UI |
| 3 | **Setup ▸ 3. Configure Character Rigs** | Humanoid on Teacher / Mother / Ch29_nonPBR; looping on the cycle clips |
| 4 | **Setup ▸ 4. Build Animator Controllers** | `MissionActorAnimator.controller`; adds Fear/Sad/Sit/Laugh to `PlayerAnimator` |
| 5 | **Setup ▸ 5. Build Stranger Prefab** | `Stranger.prefab` from the unused Ch29_nonPBR |
| 6 | **Setup ▸ 6. Wire Open Scene** | player components, `CameraIgnore` layer, staging root, 25 markers, the Stranger, the memory stage |
| 7 | **Setup ▸ 7. Build Memory Stage** | `MemoryStage` — Scene 1's flashback set, its two stand-ins and its render camera (step 6 already calls this) |

You can also run them individually if you want to see each result. **Order matters** —
step 3 re-imports FBX rigs, and a rig re-import can drop component overrides on scene
instances, which is why scene wiring is last. If you change a rig later, run step 6 again.

Everything is **idempotent**: re-running reuses what exists rather than duplicating it,
so it is safe to run again after you move things around.

### What you still have to do by hand

- **Marker positions** (step 9). They are placed relative to the player, so the story runs
  end to end immediately — but the staging won't *look* right until you drag them in the
  Scene view. Each draws a labelled gizmo with a forward arrow.
- **Audio and badge sprites** (step 10). The school bell, the ending stingers and the three
  ending badges are unassigned. Every action tolerates a null clip, so nothing breaks.

Steps 1–10 below are the manual equivalent, kept as reference for understanding or
adjusting what the builders produced.

---

## 1. Build the mission asset

Menu: **ShinyMinds ▸ Build Mission 01 (The Road Home)**

Creates `Assets/GameData/Missions/Mission01_TheRoadHome.asset` with all 55 nodes, the three
branches and the three endings. Re-running rebuilds it in place, so the story stays
editable as code in `Assets/Scripts/Missions/Editor/Mission01Builder.cs`.

The Console should print `Graph OK — 55 nodes, 3 endings`. Any red line names the exact
node with a bad link.

> Edit dialogue either in the builder (then re-run the menu item) **or** directly in the
> asset Inspector — but not both, since re-running overwrites Inspector edits.

---

## 2. Add the `CameraIgnore` layer

**Edit ▸ Project Settings ▸ Tags and Layers**. Put `CameraIgnore` in the first empty User
Layer (usually 6; `MapIcon` already occupies 3).

No new **tags** are needed — everything resolves through `MissionSceneRegistry` keys, and
the only tag check uses the built-in `Player` tag that `GİRL 1` already has.

---

## 3. Wire the player rig (`GİRL 1`)

Add three components:

| Component | Field | Value |
|---|---|---|
| `MissionActor` | Actor Key | `aisha` |
| | Animator | the `GİRL 1` Animator |
| | Mover | the `ActorMover` below |
| | Character Controller | the `GİRL 1` CharacterController |
| `ActorMover` | Animator | the `GİRL 1` Animator |
| | Character Controller | the `GİRL 1` CharacterController |
| | Ground Mask | everything **except** `CameraIgnore` |
| `PlayerLockBinder` | Player Controller | `PlayerController` on `GİRL 1` |
| | Camera Controller | `CameraController` on `CameraHolder` |
| | Map Toggle | `MapToggle` on the root `GameManager` |
| | Footsteps | `footstepaudio` on `GİRL 1` |
| | Footstep Source | the AudioSource `footstepaudio` uses |
| | Animator | the `GİRL 1` Animator |

`ActorMover`'s **Reset** button auto-fills the animator and controller if you add it first.

### Fix `CameraCollision`

On `CameraHolder`, `CameraCollision` now has a **Blockers** layer mask. Set it to
everything **except** `CameraIgnore`. Without this the camera slams into Aisha's back the
moment the Stranger stands behind her, and it already misbehaves near the two
`InteractionZone` trigger spheres.

---

## 4. Build `MissionUI.prefab`

Create at `Assets/Prefabs/UI/MissionUI.prefab`.

Root: **Canvas** — Screen Space Overlay, **Sort Order 100** (the two per-NPC Groq canvases
sit at 0, so this always draws above them) — plus **CanvasScaler** set to *Scale With
Screen Size*, 1920×1080, Match 0.5, and a **GraphicRaycaster**. Put `MissionUIView` on the
root.

```
MissionUI                    Canvas + CanvasScaler + GraphicRaycaster + MissionUIView
├── Letterbox
│   ├── BarTop               Image (black), anchored top, stretch X, height 0
│   └── BarBottom            Image (black), anchored bottom, stretch X, height 0
├── FadeOverlay              Image (black, alpha 0), full screen
├── ObjectiveHud             + ObjectiveHud.cs, top-left
│   └── ObjectiveText        TMP
├── DialoguePanel            bottom-centre — the NARRATOR ONLY; everyone with a body
│   └── Frame                Image                    speaks in a balloon instead
│       ├── SpeakerLabel     TMP
│       ├── LineText         TMP + TypewriterText.cs
│       └── ContinuePrompt   TMP  ("Press E")
├── WorldBalloon             full screen + WorldSpeechBubble.cs
│   └── Balloon              the shared speech balloon, moved over the speaker's head
│                            every frame; its tail slides to keep pointing at them
├── WorldContinuePrompt      TMP ("Press E"), bottom-right of the screen
├── ThoughtPanel             upper-centre — FALLBACK ONLY. Thoughts now play in the
│   └── Bubble               Image (translucent)     memory bubble below; this is what
│       └── ThoughtText      TMP, italic, centred    a prefab built before ThoughtBalloon
│                            + TypewriterText.cs     existed still falls back to
├── MemoryPanel              full screen + CanvasGroup (fades the whole memory in/out)
│   │                        + MemoryBubbleAnchor.cs — sizes the oval (860×580 with the
│   │                        stage, 440×250 for a thought). A memory keeps the corner and
│   │                        the dots authored here; a thought follows the thinker
│   ├── Dim                  Image (black, alpha 0.40)
│   └── Bubble               Image (UI_Ellipse), warm paper, centre-anchored; position
│       │                    and size come from MemoryBubbleAnchor at runtime,
│       │                    scaled 0.86 → 1 on open
│       ├── TailDot_0/1/2    Image (UI_Ellipse), 32/21/13 px stepping diagonally out
│       │                    from the oval's lower-left arc. These offsets ARE the
│       │                    memory pose; a thought's are computed from the head
│       ├── Frame            Image (UI_Ellipse) + Mask — crops the render to an oval.
│       │   │                Switched OFF for a Thought node: no stage, no picture
│       │   └── Render       RawImage ← MemoryStage.renderTexture, 832×468 (16:9),
│       │                    wider than the oval so the mask crops rather than stretches
│       ├── ContinuePrompt   TMP ("Press E") — the dialogue panel's own is hidden with it.
│       │                    Anchored at 6.6% of the oval's height, not a pixel offset,
│       │                    so it survives the resize
│       ├── LeftBalloon      MemoryBubble.cs — Tail (45°-rotated square), white Fill,
│       │                    Neck patch, all black-outlined, + centred dark Line
│       │                    and Typewriter. Order matters: see the builder's comment
│       ├── RightBalloon     (same, tail leaning the other way)
│       └── ThoughtBalloon   (same balloon, 320 wide, 26pt, centred, italic, tail and
│                            neck switched off) — the only thing in the oval for a Thought
├── ChoicePanel              centre, VerticalLayoutGroup + ContentSizeFitter
│   ├── ChoicePrompt         TMP
│   └── Choices              VerticalLayoutGroup
│       ├── ChoiceButton_0   Button + MissionChoiceButton.cs
│       ├── ChoiceButton_1   (same)
│       └── ChoiceButton_2   (same)
└── EndingCard               full screen + MissionEndingCard.cs — the summary screen
    ├── Scrim                Image (black, alpha 0.55), Raycast Target OFF
    └── Card                 1040×700 paper, 24px corners, black keyline
        ├── Badge            Image (Sprite, never an emoji glyph)
        ├── EndingTitle      TMP, coloured by EndingQuality
        ├── Stars            HorizontalLayoutGroup
        │   └── Star ×3      Image (UI_Star), tinted gold when earned, grey when not
        ├── Rule / Rule2     Image, 2px hairline
        ├── LessonText       TMP
        ├── AttemptText      TMP, bottom-left  ("Attempt 2")
        ├── BestText         TMP, bottom-right ("Best so far 2 of 3")
        └── Buttons          HorizontalLayoutGroup
            ├── RetryButton      Button, PRIMARY (accent fill, white label) "Try Again"
            └── ContinueButton   Button, secondary (paper, dark label) "Continue"
```

> `Assets/Art/UI/UI_Ellipse.png` is generated on the first build: a 512px white disc,
> stretched to make the oval bubble, its mask and the tail dots. Unity's built-in `Knob`
> is only a few dozen pixels across and turns soft and wobbly at bubble size.

> `Assets/Art/UI/UI_RoundedRect.png` is generated the same way: a 96px rounded square,
> imported with a **24px 9-slice border**, which gives every balloon, panel and button its
> corner radius. Unity's built-in `UISprite` was doing this and it is a 32px texture with a
> 3px border — sliced, its corners stay 3 canvas pixels at any size, which on a 1400-wide
> dialogue bar is indistinguishable from a square. Change `CornerRadius` in
> `MissionUIBuilder` and delete the PNG to reshape all of them at once.

> **Turn *Raycast Target* OFF** on `FadeOverlay`, `BarTop` and `BarBottom`. With
> `InputSystemUIInputModule`, a full-screen raycast target swallows every button click and
> the choice buttons will appear dead.

Then assign every field on `MissionUIView`, on each `MissionChoiceButton` (its own Button
and label), on `MissionEndingCard` (root, badge, title, lesson, both buttons), and on
`ObjectiveHud` (root = the `ObjectiveHud` object, text = `ObjectiveText`).

`MissionUIView.Choice Buttons` is a list — add all three in order.

---

## 5. Build `MissionSystem.prefab`

Create at `Assets/Prefabs/Missions/MissionSystem.prefab`.

```
MissionSystem              MissionRunner + MissionCameraDirector + CursorStateKeeper
├── Sfx                    AudioSource — Play On Awake OFF, Spatial Blend 0 (2D)
├── CutsceneCamera         Camera component DISABLED, NO AudioListener
└── MissionUI              instance of the prefab from step 4
```

> Delete the `AudioListener` that Unity adds to a new Camera. A second listener spams
> warnings and halves the mix.

`MissionRunner` fields:

| Field | Value |
|---|---|
| Auto Start Mission | `Mission01_TheRoadHome` |
| Auto Start Delay | `0.5` |
| Ui | the `MissionUI` child |
| Camera Director | `MissionCameraDirector` on the root |
| Sfx | the `Sfx` child |
| Player Transform | `GİRL 1` (assign after dropping into the scene) |

`MissionCameraDirector` fields: **Main Camera** = the scene `Main Camera`, **Cutscene
Camera** = the child, **Ui** = the `MissionUI` child. (Main Camera and Player Transform are
scene objects, so set them on the *instance* in the scene, not in the prefab.)

Drop one instance into `SampleScene` at the root.

---

## 6. Minimum markers to test the text

Steps 7–10 need all 25 markers, but for a text-only playthrough you only need these three,
so create them now and check the story end to end.

Create an empty root object `Mission01_Staging` and put a **`MissionStagingRoot`** component
on it. Under it add a `Markers` child. Each marker is an empty GameObject with a
`MissionMarker` component and a **Marker Key**. `MissionMarker` draws a labelled gizmo
sphere and a forward arrow, so you can place them visually in the Scene view.

> `MissionStagingRoot` is required, not optional. Unity never calls `Awake()` on a
> component of an **inactive** GameObject, so the Stranger and Mother — both of which start
> inactive — would never register themselves and every action targeting them would silently
> do nothing. `MissionStagingRoot` walks its children including inactive ones and
> initialises them. Keep `Mission01_Staging` itself **active**.

| Key | Where |
|---|---|
| `m_aisha_start` | at the school gate, facing down the road |
| `m_road_corner` | a short walk away, where the Stranger appears |
| `m_cam_gate` | a camera pose looking at the gate |

### ✅ Test now

Press Play. The intro should fade in, the four opening lines should type out and advance
on `E`, you should get control to walk to `m_road_corner`, the decision panel should appear
with a **visible mouse cursor**, and clicking an option should run that branch's dialogue
to its ending card. Retry should return to the start.

Missing markers log a warning and skip — the story still runs. Check
`%USERPROFILE%\AppData\LocalLow\<company>\<product>\shinyminds_save.json` for the recorded
result.

**Sign the narrative off here before continuing.**

---

## 7. Animator work

**Do this before step 8** — re-importing an FBX with a changed rig can drop
`m_AddedComponents` overrides on its scene instances.

First set **Rig ▸ Animation Type = Humanoid** on `Teacher.fbx`, `Mother.fbx` and
`NPC_Characters/Ch29_nonPBR.fbx`. The existing `Assets/New Human Template.ht` can be
reused as the avatar definition.

### `PlayerAnimator.controller`

Add triggers `Fear`, `Sad`, `Sit`, `Laugh`. Add a `laugh` state from `Laughing.fbx`
(`fear`, `sad` and `sit` states already exist and are currently unreachable).

Wire `Any State → fear / sad / sit / laugh` on the matching trigger, and each emote → `idle`
with *Has Exit Time* ≈ 0.9, transition 0.25s.

> Add **`Speed < 0.1` as a second condition** on every Any-State emote transition, and
> uncheck *Can Transition To Self*. Otherwise a stray trigger interrupts walking during
> free roam.

### `MissionActorAnimator.controller` (new)

Create at `Assets/Animations/Controllers/MissionActorAnimator.controller`.

- Parameters: `Speed` (float), `IsTalking` (bool), triggers `Fear`, `Sad`, `Laugh`.
- Base layer: 1D blend tree on `Speed` — `0` → `Look Around`, `2` → `Walking`,
  `6` → `Slow Run`. These values must match `ActorMover`'s `walkAnimValue`/`runAnimValue`,
  which in turn match `PlayerController`'s hardcoded 0 / 2 / 6.
- `IsTalking` → `Talking (1)`.
- Any-State emotes as above.

Assign it to `Stranger`, `Teacher` and `Mother`. **`Teacher` and `Mother` currently have no
animator controller at all**, so they cannot move or emote until you do this.

---

## 8. The Stranger

Create `Assets/Prefabs/Characters/Stranger.prefab` from
`Assets/characters/NPC_Characters/Ch29_nonPBR.fbx` (`kaya.fbx` is the fallback — both are
imported but unused).

Add: `Animator` (MissionActorAnimator), `MissionActor` with Actor Key `stranger`,
`ActorMover`. Set the layer to `CameraIgnore`.

Place an **inactive** instance under `Mission01_Staging` (which has `MissionStagingRoot`
from step 6 — that is what lets an inactive actor still register).

Also add `MissionActor` + `ActorMover` to the existing `Teacher` (`teacher`) and `Mother`
(`mother`) objects, set both to layer `CameraIgnore`, and **set `Mother` inactive** — she is
activated by Paths B and C.

> `Mother` sits outside `Mission01_Staging`, so either re-parent her under it, or tick
> **Scan Whole Scene** on `MissionStagingRoot`.

---

## 9. The remaining markers

All under `Mission01_Staging/Markers`.

**Staging**

| Key | Purpose |
|---|---|
| `m_stranger_spawn` | behind Aisha, out of frame |
| `m_stranger_call` | where he stops to call her name |
| `m_stranger_close` | his "steps closer" position |
| `m_stranger_flee` | Path C exit, far down the road |
| `m_aisha_stepback` | Path B, one pace backwards |
| `m_patha_exit` / `m_patha_exit_stranger` | Path A walk-off pair |
| `m_home_path` | Path B, toward the main road |
| `m_home_door` / `m_mother_door` | Path B doorstep pair |
| `m_teacher_stand` / `m_aisha_at_teacher` | Path C approach pair |
| `m_mother_arrive_spawn` / `m_mother_arrive` | Path C mother arrival |

**Camera poses.** Only **three** of these are still read by the mission — every other shot is a
`FramedShotAction` computed from where the actors actually stand, so it needs no marker at all
(there are 10 of those in `Mission01_TheRoadHome.asset`).

| Key | Beat | Aimed by | Rotation matters |
|---|---|---|---|
| `m_cam_gate` | the opening hard cut, on the fade up from black | its own rotation | **yes** |
| `m_cam_aisha_cu` | the close-up as the mission opens | `lookAtActorKey: aisha` | no — position only |
| `m_cam_end_a` | the Path A ending, as she walks off | its own rotation | **yes** |

`m_cam_meeting`, `m_cam_close`, `m_cam_choice`, `m_cam_home_door`, `m_cam_teacher` and
`m_cam_reunion` are left over from before `FramedShotAction` existed. Nothing reads them; they can
be deleted.

> **Never name a marker `Waypoint …`.** The Hierarchy already returns ~325 hits for that
> string and `CarAI.waypoints` is a serialized `Transform[]`, so a mis-drag creates a car
> that drives to a cutscene mark and nothing in code will tell you.

Tip: to place a camera marker, move the Scene view to the shot you want, select the marker
and use **GameObject ▸ Align With View**.

---

## 10. Audio and cleanup

`Assets/Audio/Ambient/` and `Assets/Audio/UI/` are empty. Source and import:

| Clip | Used by |
|---|---|
| `school_bell.wav` | `s1_open` — assign to the `PlaySoundAction` in the mission asset |
| `ui_click.wav` | choice buttons |
| `ending_good.wav` / `ending_bad.wav` | `MissionEnding.stinger` |

Also assign the three `MissionEnding.badge` sprites (a Sprite, **not** an emoji — TMP's
default atlas renders ❌✅🏆 as empty boxes).

**Delete one of the two `BackgroundMusic` GameObjects.** There are two, left over from the
audio branch merge; they double the volume and will mask the school bell.

---

## 11. The memory stage (Scene 1's flashback)

Menu: **ShinyMinds ▸ Setup ▸ 7. Build Memory Stage** (step 6 already runs it).

`s1_mother` / `s1_aisha` are not a conversation at the school gate — they are what Aisha's
mother told her that morning, which Aisha is remembering as she leaves. They are `Memory`
nodes, and they play inside the memory bubble with the two of them acting it out.

The set is built in the open scene:

```
MemoryStage                  MemoryStage.cs (must stay ACTIVE — it toggles the child)
└── Diorama                  switched on only while a memory is on screen
    ├── MemoryCamera         solid warm background, culls to the MemoryStage layer,
    │                        renders into Assets/Rendering/MemoryStage.renderTexture
    ├── Backdrop             unlit quad showing whatever texture sits in Assets/Art/Memory
    ├── KeyLight / FillLight point lights, range 14, no shadows
    ├── Aisha_Memory         GİRL 1 model, scale 5  → speakers with memorySide = Left
    └── Mother_Memory        Mother model, scale 2  → memorySide = Right
```

It sits at **y = −600** on its own `MemoryStage` layer, and every other camera in the scene
has that layer removed from its culling mask, so the stand-ins can never be seen or reached
from the road.

### Tuning the framing

The stand-ins are posed from constants, and character heights differ per model, so the
first build may frame them loosely.

1. Select `MemoryStage/Diorama` and tick it **active** in the Inspector.
2. Select `MemoryCamera` — Unity shows a live camera preview in the Scene view. You can
   also double-click `Assets/Rendering/MemoryStage.renderTexture` to see what it renders.
3. Nudge the camera (or the two stand-ins) until they read like a two-shot with both of
   them turned three-quarters towards the lens — and **keep the top quarter of the frame
   clear**, because the two speech balloons sit across it.
4. Set `Diorama` **inactive** again and save the scene.

Plain **Build Memory Stage** keeps a set that already exists, so your nudges survive
re-running the setup. **Setup ▸ Rebuild Memory Stage From Scratch** throws them away and
starts from the constants in `MissionMemoryStageBuilder`.

> **After changing anything about how the memory looks, run
> **ShinyMinds ▸ Setup ▸ Apply Memory Bubble Changes**.** The bubble is half prefab (size,
> corner, balloons, tail) and half scene (the framing of the stand-ins), and editing the
> builder scripts changes *neither* until they are re-run — running only one of the two is
> what a half-applied result looks like. This item does both.

### Changing the backdrop

`Assets/Art/Memory/` holds the picture behind the pair — currently the flat house
illustration, so the memory reads as home rather than as another patch of city. Any texture
dropped in that folder is picked up on the next rebuild (keep exactly one there), sized to
the frame by width with its own aspect preserved, and drawn **unlit** so the lights that
model the two figures do not rake across a drawing and give it away as a flat plane.
`MemoryBackdrop.mat` is generated beside it. With the folder empty, the memory falls back
to the camera's plain paper colour.

> The stand-ins need `MissionActorAnimator.controller` (step 4) or they will stand in a
> T-pose. They are separate models, not the scene's `Mother` — she is still needed, alive
> and inactive, for Paths B and C.

---

## Regression checklist

After each step, confirm the pre-existing game still works:

- [ ] Walk, run, turn, jump
- [ ] Mouse camera + camera collision near walls
- [ ] Minimap, `M` map toggle
- [ ] School door opens/closes on `E` from both sides
- [ ] Both Groq NPCs still talk — and now Aisha **stands still** with **no looping
      footsteps** during the conversation (this was broken before)
- [ ] Standing where a door trigger and an `InteractionZone` overlap, `E` does exactly one thing
- [ ] Alt-tab away and back: cursor is still locked
- [ ] Car traffic still drives its routes
- [ ] Scene 1: the mother/Aisha exchange opens as a memory bubble with both of them moving
      inside it, and the bubble closes before Aisha sets off
- [ ] Nothing from the memory stage is visible from the road, and the minimap is unchanged
