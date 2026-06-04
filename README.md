# ShinnyMinds

ShinnyMinds is a third-person city exploration prototype built in Unity. The project uses animated character movement, an orbiting camera, and a Blender-imported urban environment.

---

# Features

## Character System

- Humanoid character import
- Animator Controller setup
- Idle / Walk / Run / Jump animation states
- Backward movement and turning
- CharacterController movement with gravity

---

## Movement System

Controls:

| Key | Action |
|---|---|
| W | Walk Forward |
| Shift + W | Run |
| S | Walk Backward |
| A | Turn Left |
| D | Turn Right |
| Space | Jump |
| Mouse | Rotate Camera |

---

## Camera System

- Third-person orbit camera behavior
- Mouse-controlled rotation
- Smooth follow and interpolation
- Character-centered view

---

## Environment System

- Blender city imported as FBX
- World city model in `Assets/Environment/city/Worldcity.fbx`
- Texture folder at `Assets/Environment/city/textures/`
- Environment geometry designed for scene exploration

---

# Technologies Used

- Unity 6000.4.9f1
- C#
- Blender
- Universal Render Pipeline (URP)
- Unity Input System

---

# Project Structure

```text
Assets/
│
├── animations/
├── characters/
│   └── GİRL 1.FBX
├── Environment/
│   └── city/
│       ├── Worldcity.fbx
│       └── textures/
├── Materials/
├── Scenes/
│   └── SampleScene.unity
├── Settings/
├── textures/
└── TutorialInfo/

CameraController.cs
PlayerController.cs
```

---

# Setup Instructions

## 1. Clone Repository

```
git clone <repository-url>
```

---

## 2. Open Project
Open the project using:

```
Unity Hub
```

Recommended project version:

```
Unity 6000.4.9f1
```

---

## 3. Open Scene
Open:

```
Assets/Scenes/SampleScene.unity
```

---

# Character Setup
The player uses:

- Animator
- CharacterController
- PlayerController.cs

---

# Camera Setup
Hierarchy example:

```
GIRL 1
 └── CameraHolder
      └── Main Camera
```

---

# Animator Parameters

| Parameter | Type |
|---|---|
| Speed | Float |
| TurnLeft | Bool |
| TurnRight | Bool |
| Backward | Bool |
| Jump | Trigger |

---

# Blender Export Workflow
Recommended export settings:

```
Forward = -Z Forward
Up = Y Up
Apply Transform = ON
```

Texture structure:

```
Assets/Environment/city/Worldcity.fbx
Assets/Environment/city/textures/
```

---

# Important Learning Concepts
This project demonstrates:

- Animator Controllers
- Animation State Machines
- CharacterController movement
- Orbit camera systems
- Unity collision systems
- FBX import workflows
- Third-person game architecture

---

# Common Problems Solved

## Animation Freezing
Fix:

- Enable Loop Time

---

## Character Sinking Into Floor
Fix:

- Disable Root Motion
- Use stable colliders

---

## Walking Through Buildings
Fix:

- Add colliders to environment meshes
- Ensure the player has a CharacterController component
- Use physics-based collision checks

---

# Future Improvements
Possible future features:

- NPC AI
- Vehicles
- Inventory system
- Combat system
- Minimap
- Day/Night cycle
- Quest system
- Multiplayer
- Sound effects

---

# Screenshots

![Gameplay](screenshots/Gameplay.png)

---
