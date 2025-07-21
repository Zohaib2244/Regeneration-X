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


