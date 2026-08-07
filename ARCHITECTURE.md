# System Architecture & Technical Guidelines

## 1. Core Rendering & Graphics (Vulkan + URP)
*   **GPU Resident Drawer:** Utilized for rendering dense environments and heavy traffic without CPU bottlenecking.
*   **Texture Streaming:** Addressables used to load HD textures dynamically. Force ASTC compression for optimal VRAM usage.

## 2. Performance Framework (ADPF)
*   **Thermal Monitoring:** Integrate Android Dynamic Performance Framework (ADPF).
*   **Dynamic Scaling:** 
    *   `THERMAL_STATUS_LIGHT`: Normal operation.
    *   `THERMAL_STATUS_MODERATE`: Disable post-processing (bloom, motion blur).
    *   `THERMAL_STATUS_SEVERE`: Step down rendering resolution dynamically to prevent OS-level app termination.

## 3. Traffic & AI Systems (DOTS / ECS)
*   Do not use standard Monobehaviours for civilian traffic.
*   Utilize Unity DOTS (Entity Component System) to instantiate and manage hundreds of civilian vehicles.
*   Traffic splines generated asynchronously ahead of the player.

## 4. Memory Management (Zero-Allocation Policy)
*   **Object Pooling:** Zero instantiation during active gameplay. Pre-pool all VFX, AI models, weapons, and UI elements during loading screens.
*   **Garbage Collection:** Pre-allocate all memory in `Awake()`. No `new` keywords in `Update()` or `FixedUpdate()`.
