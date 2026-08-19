# Project Highway Renegade

## Overview
Highway Renegade is an offline Android motorcycle combat racer (Road Rash–style) built in **Unity 6000.0.38f1** with **URP 17** and **Vulkan-only** rendering.

## Technology Stack
* **Engine:** Unity 6 (6000.0.38f1)
* **Graphics:** URP 17, Vulkan, IL2CPP ARM64
* **UI:** UI Toolkit
* **Architecture:** Layered assemblies (`Core` pure logic + `Gameplay` glue). Traffic uses object pooling; there is **no DOTS/ECS** in this project.
* **Target:** Android API 30–35, landscape

## Game Features
* **Campaign:** 5 chapters, 10 events, persistent rival grudges, chapter intro beats
* **Quick Race:** Unlocked tracks from `TrackCatalog` with free-run payouts
* **Garage:** Buy/equip/repair bikes; engine and tire upgrades (stages 0–5)
* **Tracks:** 5 catalog tracks generated as separate scenes with spline progress and curved-road support
* **Combat:** Melee, kick, weapon steal, police pursuit
* **Audio:** Procedural SFX + procedural music beds per biome/state
* **Performance:** Thermal throttling, render budget bootstrap, post-processing scales with heat

## Development
1. Clone and open in Unity 6000.0.38f1 with Android Build Support.
2. Run `python3 Tools/Assets/fetch-assets.py` for CC0 textures/HDRIs (optional).
3. Generate scenes: **Highway Renegade → Generate Menu Scenes** and **Generate Test Track**.
4. **Compile locally** (no Unity licence needed):
   ```bash
   Tools/CompileCheck/compile-check.sh --tests
   ```
5. **CI:** GitHub Actions no longer runs on every push/PR. The Android build runs on **merge to `main`** (APK release) or **manual dispatch** from the Actions tab. Compile Check is **manual dispatch** only.

## Ship Checklist
* Confirm final `applicationIdentifier` before Play Store upload (currently `com.highwayrenegade.game`).
* Add adaptive app icons under `Assets/Plugins/Android/` or Player Settings.
* Device-tune physics in `Powertrain`, `TyreModel`, and `RidingAssists`.
