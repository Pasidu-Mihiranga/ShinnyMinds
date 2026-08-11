# Cinematics plan — readable subtitles, framed shots, animated cutscenes

> **Status: items 1, 2A and 3A/3C are implemented.** Rebuild with
> **ShinyMinds ▸ Setup ▸ Run All Steps** (or just **Build Mission 01** +
> **Setup ▸ 1** if the scene is already wired). What remains is 3B (Timeline beats)
> and 2B (Cinemachine), both of which are deliberately deferred — see below.

Three problems, in the order they should be fixed. They are independent, and each is
shippable on its own.

| # | Problem | Root cause | Status |
|---|---|---|---|
| 1 | Subtitles unreadable | Canvas shader channels | **done** |
| 2 | Shots don't frame the event | Fixed camera poses can't know where the actors are | **2A done**, 2B deferred |
| 3 | Cutscenes feel like lerped puppets | Nothing is keyframed | **3A + 3C done**, 3B deferred |

## What shipped

- `FramedShotAction` — `TwoShot` / `OverShoulder` / `CloseUp` / `Wide`, computed from the
  actors' live positions, with a spherecast that pulls the camera in when scenery blocks
  the view.
- `CameraMoveAction` — push-in dolly with optional shake. **Non-blocking by default**, so
  a slow push keeps running underneath the dialogue it exists to underscore.
- `PlayAnimationAction` — plays a named animator state, optionally waiting for it to end,
  with a timeout so a bad state name cannot soft-lock a mission.
- `ActorMover.useRootMotion` — the clip drives travel instead of the transform, so feet
  plant instead of skating.
- `Subtitle SDF.mat` — the default font material with an underlay, applied to story text
  only.
- Mission 01 rewritten: the conversation beats now use framed shots, and every line is
  wrapped to roughly 42 characters over at most two lines.

---

## 1. Subtitles — root cause and fix

**Not a colour problem.** The generated prefab already held near-white text
(`0.96, 0.97, 1`) on a dark navy panel (`0.07, 0.09, 0.14`).

The canvas had `m_AdditionalShaderChannelsFlag: 0`. TextMeshPro's SDF shader reads
per-vertex scale data out of **TEXCOORD1**, and Normal/Tangent for its bevel variants.
A Canvas created through the GameObject menu gets `TexCoord1 | Normal | Tangent` set for
you; a Canvas created **from script** defaults to `Nothing`. The shader therefore received
zeros, the distance-field maths degenerated, and every glyph rendered muddy and dark no
matter what colour was assigned.

Fixed in `MissionUIBuilder.ApplyTextShaderChannels`. To repair the prefab you already
generated without rebuilding:

> **ShinyMinds ▸ Setup ▸ Fix Text Rendering On Existing Canvases**

It also repairs the two per-NPC Groq canvases if they have the same flag unset.

### Then apply subtitle craft on top

Once the text renders correctly, these are the things that actually make subtitles
readable in a 3D game, in order of impact:

1. **Never rely on the backing plate alone.** Camera cuts change what is behind the text.
   Give the subtitle a **dark outline or underlay** so it survives a bright sky *and* a
   dark interior. In TMP: enable *Underlay* (soft drop shadow, offset ~0.5, dilate 0.1) on
   a **material preset**, not the shared default material — editing the shared
   `LiberationSans SDF` material would restyle every TMP object in the project including
   the Groq NPC panels.
2. **Minimum 28–32px at 1080p** for a 8–14 year-old audience. The current 32 is right;
   don't shrink it to fit more text — split the line into two nodes instead.
3. **Line length ≤ ~42 characters**, two lines maximum. Long single lines are the main
   readability failure in the current script — several mission lines are far longer.
4. **Speaker name in the character's colour, body text always white.** Coloured body text
   loses contrast against arbitrary backdrops.
5. **Hold time.** Auto-advance, where used, needs ≈ 0.3s + 0.06s per character. A child
   reads roughly half as fast as an adult.

### Concrete follow-up tasks

- [ ] Create `Assets/TextMesh Pro/Resources/Fonts & Materials/Subtitle SDF.mat` as a
      preset of the default font material with Underlay enabled; assign it to `LineText`,
      `ThoughtText`, `SpeakerLabel`.
- [ ] Add a `MissionUIStyle` ScriptableObject holding font sizes, colours and the subtitle
      material, so styling lives in one asset rather than in the builder.
- [ ] Split the over-long narrator lines in `Mission01Builder` into two nodes each.

---

## 2. Cameras — why the shots miss the action

The current `CameraShotAction` moves the cutscene camera to a **fixed marker pose**. That
is a hard-coded answer to a question that changes at runtime: the marker cannot know that
the Stranger stopped 1.4m short, or that Aisha approached from a different angle because
the player walked in facing north.

Screenshot 2 is the failure mode — a valid camera pose pointing at empty pavement.

Marker poses are the right idea for *establishing* shots. They are the wrong idea for
**coverage of two actors talking**, which is most of this mission.

### The fix: frame from the subjects, not from a fixed point

Three options. **Recommendation: 2A now, 2B when you have time.**

#### 2A. Auto-framed shots — no new dependency, ~2 hours

Add a `FramedShotAction` that computes the camera pose from the actors it must show:

- **Two-shot**: given actors A and B, take the midpoint, get the A→B axis, place the
  camera off that axis by `angle` degrees, at a distance derived from their separation
  and the camera FOV so both fit with margin.
- **Over-the-shoulder**: place behind A's shoulder, look at B.
- **Close-up**: offset from one actor's head along their forward, with a slight angle.

Add a `LayerMask` line-of-sight check: raycast from the computed position to the midpoint,
and if a wall is hit, pull in or orbit until clear. That alone removes the "camera inside a
tree" failures visible in screenshot 3.

Keep `markerKey` as an optional **hint** for the general direction, so authored intent is
preserved but framing is computed.

```
FramedShotAction
  subjectActorKeys   [aisha, stranger]
  shotType           TwoShot | OverShoulder | CloseUp | Wide
  angleDegrees       35          // off the subject axis
  heightOffset       1.6         // eye level, not pivot level
  margin             1.25        // how much air around the subjects
  blendSeconds       1.0
  obstructionMask    everything except CameraIgnore
```

This is the highest value-per-hour item in this document. It makes every existing beat
frame correctly with **no marker placement at all**, which also removes most of step 9 of
the setup guide.

#### 2B. Cinemachine — the industry-standard answer, ~1 day

Cinemachine is **not currently installed**. Adding `com.unity.cinemachine` 3.x gives you,
without writing any of it:

- **CinemachineTargetGroup** — point a camera at a *group* (Aisha + Stranger) and it keeps
  both in frame as they move. This is exactly the two-shot problem.
- **CinemachineCamera + Rotation/Position Composer** — deadzone, soft zone, lookahead.
- **CinemachineDeoccluder** — the line-of-sight problem, solved properly.
- **Blends and a blend asset** — per-transition ease curves instead of one `SmoothStep`.
- **Impulse** — camera shake for beats like the stranger stepping closer.

Migration is contained: `MissionCameraDirector` keeps its `IMissionCamera` interface, and
`ShotTo` activates a virtual camera instead of lerping a transform. Mission data does not
change. That is the payoff of having gone through an interface in the first place.

**Recommendation:** do 2A first — it is hours, not days, and it unblocks the mission now.
Move to 2B when you start authoring mission 02, at which point the per-mission camera cost
starts to dominate.

#### 2C. Do nothing, place all 25 markers by hand

Viable, and it is what the setup guide currently describes. But it is ~25 poses per
mission, it breaks whenever an actor's path changes, and none of that effort transfers to
mission 02.

### Concrete follow-up tasks

- [ ] Implement `FramedShotAction` with `TwoShot`, `OverShoulder`, `CloseUp`, `Wide`.
- [ ] Add the obstruction raycast with a `CameraIgnore`-excluded mask.
- [ ] Replace `m_cam_meeting`, `m_cam_close`, `m_cam_choice`, `m_cam_teacher`,
      `m_cam_reunion` in `Mission01Builder` with framed shots. Keep `m_cam_gate` and
      `m_cam_end_a` as authored establishing/ending poses.
- [ ] Add `shakeAmplitude` to `CameraShotAction` for the "steps closer" beat.

---

## 3. Cutscenes as animation

This is the real question behind the screenshots. Right now every cutscene is
`Vector3.MoveTowards` plus animator parameter flips. That reads as puppets sliding around,
because there is no keyframed intent anywhere — no anticipation, no weight, no timing.

### Do not move the whole system to Timeline

It is the obvious idea and it is wrong here, for one specific reason: **Timeline cannot
branch.** This mission is three branches and three endings; the graph *is* the point. A
`PlayableDirector` binds tracks to specific scene objects, so every branch would need its
own `.playable` asset with its own bindings, and branching mid-timeline needs signal
emitters plus custom `PlayableAsset` subclasses.

### Do this instead: keep the director, make individual beats playable

The SO graph stays in charge of **structure** — branching, choices, endings, retry.
Individual **beats** become authored animation. Three new actions, in increasing power:

#### 3A. `PlayAnimationAction` — a clip on one actor (easiest, do first)

```
PlayAnimationAction
  actorKey       stranger
  stateName      "StepCloser"
  crossFade      0.2
  waitForEnd     true
```

Plays a state on the actor's animator and optionally blocks until it finishes. Good for
emotes, gestures, reactions — "the stranger looks uncomfortable", "Aisha shrinks back".
This is a ~40-line action and it immediately upgrades the emotional beats.

**Enable root motion for these clips.** The single biggest reason the current walking looks
wrong is `applyRootMotion = false` with a hand-driven transform — the feet slide because
the animation's own translation is discarded. For a scripted cutscene walk you want the
opposite: let the clip drive the movement.

#### 3B. `PlayTimelineAction` — a fully directed beat (the cinematic answer)

```
PlayTimelineAction
  directorKey    "cutscene_stranger_approach"
  bindings       [aisha -> Track1, stranger -> Track2, camera -> Track3]
  waitForEnd     true
```

For the handful of beats that deserve real direction — the stranger's approach, the Path C
teacher intervention, the Path B doorstep — author a Timeline with animation tracks,
a cinemachine/camera track, an audio track and signal emitters. The action resolves the
`PlayableDirector` by key through the same `MissionSceneRegistry` pattern, rebinds tracks
to the registered actors, plays, and yields until done.

`com.unity.timeline` 1.8.12 is **already installed and unused**, so there is no new
dependency.

**Which beats are worth it:** the stranger's approach (the whole tension of the mission
lives here), and each of the three endings. Roughly 4 timelines. Everything else stays
procedural — a lerp is fine for "walk to the door".

#### 3C. Root-motion cutscene walks

Add a `walkClipMode` to `ActorMover`: when set, drive movement by enabling root motion and
playing a locomotion state, using `OnAnimatorMove` to steer toward the target rather than
translating the transform directly. Feet plant, weight shifts, turns look like turns.

Note the existing scale problem interacts here: `GİRL 1` is scale 5, Teacher/Mother are
scale 2, and Mixamo clips assume ~1. Root motion is scaled by the transform, so this
actually *fixes* the speed-matching hack in `ActorMover.ScaleFactor` rather than fighting it.

### Recommended order

1. **3A `PlayAnimationAction`** — half a day, biggest immediate quality gain per hour.
2. **2A `FramedShotAction`** — shots start landing on the action.
3. **3C root motion** for the four scripted walks.
4. **3B `PlayTimelineAction`** for the stranger approach only. Judge from that one whether
   the remaining three are worth authoring.
5. **2B Cinemachine**, when mission 02 starts.

### What "good" looks like for the stranger approach

> **Amended for mission 01.** All five angles below are in `BuildScene2` and are wanted. What
> was removed is the **travelling between them**: every shot is now a hard cut (`Cut`, i.e.
> `blendSeconds: 0`) and both `Push` dollies are gone.
>
> A blend from a wide to a close-up is the camera flying several metres down its own view axis,
> which on screen is a zoom in — and the opening blend out to the wide is a zoom out. Cutting
> changes the angle without the lens ever appearing to move. So point 3 below ("push in
> slightly, a dolly not a cut") is the one piece of this deliberately not implemented.

Worth being concrete, because this beat carries the whole lesson:

- Start on a **wide** two-shot — the street feels open and safe.
- As he says "I know your mother, Lia", **cut closer and lower**. Tighter framing reads as
  pressure without anything scary happening.
- On "steps closer", **push in** slightly (a dolly, not a cut) and add a small impulse.
- On Aisha's thought "I don't know this person", go to a **close-up on her face**, holding
  slightly longer than feels comfortable. The pause is the teaching moment.
- The choice panel appears over that held close-up.

None of that needs Timeline. Points 1–4 are `FramedShotAction` plus a push-in parameter,
which is why 2A is ranked so highly.

---

## Sequencing summary

| Order | Item | Why first |
|---|---|---|
| ✅ done | Canvas shader channels | subtitles were unreadable |
| 1 | Subtitle material + line-length pass | content is unusable until legible |
| 2 | `PlayAnimationAction` (3A) | biggest quality gain per hour |
| 3 | `FramedShotAction` (2A) | removes ~20 markers, fixes every shot |
| 4 | Root-motion walks (3C) | removes foot-sliding |
| 5 | `PlayTimelineAction` (3B) for one beat | evaluate before committing to four |
| 6 | Cinemachine (2B) | when mission 02 begins |

Items 2–4 are each independently shippable and none of them changes `MissionData`'s
schema, so authored content survives all of it.
