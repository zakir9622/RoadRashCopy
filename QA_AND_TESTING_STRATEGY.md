# QA & Testing Strategy

## Overview
Given the target for a crash-free, 60/120 FPS experience on Android 15, rigorous testing protocols must be implemented, focusing heavily on performance profiling, device fragmentation, and memory management.

## 1. Performance & Profiling (The "Zero-Drop" Mandate)
*   **FPS Consistency:** Monitor frametimes using Unity Profiler and Snapdragon Profiler. Variance must not exceed 2ms per frame to avoid micro-stutters.
*   **Memory Leaks:** 
    *   Execute strict 2-hour endurance runs on target devices.
    *   Monitor Garbage Collection (GC) allocations. The `Update()` loop must show 0 bytes of allocation.
*   **Thermal Testing (ADPF):** Force thermal throttling via ADB commands (`adb shell am broadcast -a android.intent.action.THERMAL_EVENT --ei status 2`) to verify the game dynamically scales down graphics without crashing.

## 2. Device Fragmentation & Compatibility
*   **Form Factors:** Test across standard displays, foldables (inner and outer screens), and tablets. Ensure UI anchors and Edge-to-Edge display logic adapt without letterboxing.
*   **Graphics API Fallback:** While Vulkan is enforced, test OS-level ANGLE translations on devices where native Vulkan drivers are unstable or outdated.
*   **Input Testing:** Verify multi-touch inputs (up to 4 simultaneous touches for acceleration, steering, and combat) to ensure zero ghosting. Test Bluetooth controller latency (Xbox/DualSense).

## 3. Gameplay Systems Validation
*   **Physics Edge-Cases:** Test extreme collision vectors (e.g., T-boning traffic at max speed) to ensure the ragdoll physics do not clip through the terrain or cause NaN (Not a Number) velocity crashes.
*   **State Machine Boundaries:** Force edge-case AI scenarios (e.g., triggering "Busted" state at the exact moment a race finishes) to prevent soft-locks.
