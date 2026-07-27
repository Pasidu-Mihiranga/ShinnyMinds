# Mission 01 — Unity Editor setup

All the C# is written and compiles. This is the Editor work that cannot be done from
outside Unity.

## Quick start — four menu clicks

Open `SampleScene`, then run these in order:

| # | Menu | Builds |
|---|---|---|
| 1 | **ShinyMinds ▸ Build Mission 01 (The Road Home)** | the mission asset — 55 nodes, 3 branches, 3 endings |
| 2 | **ShinyMinds ▸ Setup ▸ 1. Build Mission UI Prefab** | `MissionUI.prefab` — ~25 objects, fully wired |
| 3 | **ShinyMinds ▸ Setup ▸ 2. Build Mission System Prefab** | `MissionSystem.prefab` — runner, cutscene camera, sfx, UI |
| 4 | **ShinyMinds ▸ Setup ▸ 3. Wire Open Scene** | player components, `CameraIgnore` layer, staging root, all 25 markers |

Then **save the scene (Ctrl+S)** and press **Play**.

That is enough to play the whole story: intro, free-roam walk, the stranger encounter, the
A/B/C decision with a live mouse cursor, all three branches, and the ending card with
Retry. Step 4 places markers at rough positions relative to the player — the story runs,
but the staging won't look right until you drag them (step 9).

All four are **idempotent** — re-running reuses what exists instead of duplicating it, so
it is safe to run again after you move things around.

### What the quick start does *not* do

- **Animator states** (step 7). `Teacher` and `Mother` have no animator controller at all,
  so they cannot walk or emote yet, and the `Fear` / `Sad` / `Laugh` triggers don't exist.
  The mission logs a warning and carries on.
- **The Stranger** (step 8). No stranger character exists in the scene yet, so his lines
  play but nobody walks up.
- **Audio and badge sprites** (step 10). The school bell and ending stingers are unassigned.

Steps 1–6 below are the manual equivalent of the quick start, kept as reference for when
you want to understand or adjust what it built. **Steps 7–10 are still manual** and are
what turn a working text adventure into the staged scene.

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
├── DialoguePanel            bottom-centre
│   └── Frame                Image
│       ├── SpeakerLabel     TMP
│       ├── LineText         TMP + TypewriterText.cs
│       └── ContinuePrompt   TMP  ("Press E")
├── ThoughtPanel             upper-centre
│   └── Bubble               Image (translucent)
│       └── ThoughtText      TMP, italic, centred + TypewriterText.cs
├── ChoicePanel              centre, VerticalLayoutGroup + ContentSizeFitter
│   ├── ChoicePrompt         TMP
│   └── Choices              VerticalLayoutGroup
│       ├── ChoiceButton_0   Button + MissionChoiceButton.cs
│       ├── ChoiceButton_1   (same)
│       └── ChoiceButton_2   (same)
└── EndingCard               + MissionEndingCard.cs
    └── Frame
        ├── Badge            Image
        ├── EndingTitle      TMP
        ├── LessonText       TMP
        └── Buttons          HorizontalLayoutGroup
            ├── RetryButton      Button + TMP label "Try Again"
            └── ContinueButton   Button + TMP label "Continue"
```

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

**Camera poses** — position *and* rotation both matter:

`m_cam_aisha_cu` · `m_cam_meeting` · `m_cam_close` · `m_cam_choice` · `m_cam_end_a` ·
`m_cam_home_door` · `m_cam_teacher` · `m_cam_reunion`

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
