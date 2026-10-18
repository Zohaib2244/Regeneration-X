# 🧩 Unity Mesh Explosion & Reconstruction – DOTS + Rayfire Showcase

A **Unity 6 showcase project** that demonstrates a **visually satisfying explosion and reconstruction effect** using:

- 🧱 **Rayfire** for dynamic mesh fragmentation  
- ⚙️ **DOTS/ECS** for high-performance entity and physics management  
- ⏱️ **Custom time control** to pause, resume, or rewind the simulation

---

## 🧩 Main Modes & Features

### 💥 Explosion
- **Epicenter-based force:** Applies an outward force from a chosen epicenter, affecting all VOBs (fragments) within a radius.
- **Slomo effect:** Explosion is slowed down for a visually satisfying, cinematic look.

### �️ Reconstruction
- **Reconstruction point:** VOBs animate back to their original positions, starting with the ones closest to the reconstruction point.
- **Order control:**  
  - **Randomize VOBs:** Optionally randomizes the order in which VOBs reconstruct, or uses proximity to the reconstruction point.
  - **Freeze Unbatched VOBs:** If enabled, all VOBs freeze in place (even in mid-air) before reconstruction begins; if disabled, unbatched VOBs continue to simulate physics during reconstruction.
- **Reconstruction types:**  
  - **Default:** VOBs return to their original positions with smooth, linear motion.
  - **Spiral:** VOBs follow a spiral path as they animate back to their original positions (the main highlight!).
- **Highly customizable:** Easily switch between modes and tweak behaviors for unique effects.

### 🧲 Magnetic Mode
- **Magnetic point:** VOBs are attracted and deform toward a specified magnetic point, creating dynamic and organic movement.

---


## 🎯 Technologies Used

- 🧩 **Unity 6**  
  The latest version of the Unity engine powering the entire project.

- 🔨 **Rayfire Plugin**  
  For high-quality mesh shattering, fragmentation, and dynamic simulation.

- ⚡ **Unity DOTS & ECS**  
  Entity Component System architecture for managing thousands of fragments efficiently.

- 🧠 **Unity Physics**  
  DOTS-compatible physics system for realistic motion and collisions.

- 🚀 **Unity Jobs System**  
  Multithreaded execution of fragment logic for parallel performance gains.

- 💥 **Unity Burst Compiler**  
  Optimizes compute-heavy code for maximum performance during animation, physics, and time control.

---

---

## 🧊 Formations & Cube Mass

VOBs can turn into **any mesh**, and the player's cube count is a resource.

### Make a shape (no more JSON / enum edits)
1. **NuttyTools → VOB Formation Baker**
2. Drop in any model, prefab, scene object or Mesh, type a **Cube Cost** (e.g. sword = 120, sphere = 1000), press **Bake**.
3. That's it: a `VOBFormation` asset lands in `Gameplay/Formations`. The shape is scaled so one voxel = one cube, so cubes always tile perfectly.
   - Select several models in the Project window → **Bake Selected** to do a whole folder at once.
   - **Hollow** = most detail for the cost. **Solid** = chunkier, every cube is part of the volume.
   - **Preview** draws the result as wire cubes at the Scene view's focus point.
   - The formation's inspector has **Re-bake at cost** to change a shape's price later.
   - A hand-made layout (e.g. the Rayfire sphere) can be saved with **Save Layout As Formation** on its `VOBYAuthoring`.

### Morph
On `ECSCommandBridge`, assign **Target Formation** and press **Morph To Target Formation**, or fill **Hotkey Formations** and press 1–9 in Play mode. A VOBY can morph straight from one shape to another, or rebuild into one after exploding. Default and Spiral transitions both work.

### Cube mass: collect & lose
- Tick **Is Player** on the player's `VOBYAuthoring`. It now owns cubes (`VOBComponent.VOBYParent`) and has a collector.
- A shape needs at least its **cost** in cubes. Spare cubes pack inside the shape, then float in a halo around it.
- Loose cubes (`VOBLoose`) within **Collect Radius** get pulled in and absorbed into the next free spot.
- `ECSCommandBridge.DetachCubes(count, hitPoint, force)` knocks off the cubes nearest the hit as loose pickups; this is the hook for damage. Test with **X** (knock off) and **C** (collecting on/off).
- `PlayerCubeCount` and `CanMorphInto(formation, out missing)` are there for UI.
- **Player Anchor** makes the player VOBY follow any transform, so any character controller can drive it.
