# Project Highway Renegade (Road Rash Spiritual Successor)

## Overview
A high-fidelity, high-performance vehicular combat racing game tailored for modern Android devices (Android 15+). Designed to run flawlessly at 60/120 FPS across mobile phones, foldables, and tablets without memory leaks or thermal throttling crashes.

## Technology Stack
*   **Engine:** Unity 6 (LTS)
*   **Graphics API:** Vulkan (Strictly enforced)
*   **Render Pipeline:** Universal Render Pipeline (URP) with Spatial-Temporal Post-Processing (STP)
*   **Architecture:** Entity Component System (ECS) via Unity DOTS for traffic pooling
*   **Target OS:** Android 15 (API Level 35)

## Quick Start for Development
1. Clone the repository.
2. Open in Unity 6 LTS.
3. Ensure Android Build Support is installed.
4. Go to `Project Settings > Player > Android`.
5. Set Minimum API Level to 30, Target API to 35.
6. Verify Vulkan is the only Graphics API in the list.
