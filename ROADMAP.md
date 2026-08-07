# Development Roadmap — Project Highway Renegade

Phased build order derived from [GAME_DESIGN_DOCUMENT.md](GAME_DESIGN_DOCUMENT.md),
[ARCHITECTURE.md](ARCHITECTURE.md), and [QA_AND_TESTING_STRATEGY.md](QA_AND_TESTING_STRATEGY.md).

**Sequencing principle:** the performance envelope (Vulkan, ADPF, zero-alloc pooling) is
established *before* content volume grows. Retrofitting a zero-allocation policy onto an
existing codebase is far more expensive than building to it from commit one.

---

## Phase 0 — Foundation *(current)*

| # | Deliverable | Status |
|---|---|---|
| 0.1 | Git repo + GitHub remote + Unity `.gitignore` / LFS | ✅ done |
| 0.2 | Unity 6 project skeleton, assembly definitions, folder layout | ✅ done |
| 0.3 | CI: automated Android build + EditMode/PlayMode tests | ✅ done |
| 0.4 | Verify project opens in Unity 6 LTS; confirm Vulkan-only, API 35 | ⬜ **needs local Unity** |

**Exit criteria:** empty scene builds to an `.aab` in CI and boots on a device at a stable 60 FPS.

---

## Phase 1 — Core Ride Feel

The single highest-risk item in the project. If the bike does not *feel* right, no amount of
content rescues it. Budget generous iteration time here.

- **1.1 Sphere-cast suspension** — raycast-per-wheel ground detection, spring/damper force.
- **1.2 Arcade-sim handling** — snappy acceleration curve, speed-sensitive steering falloff.
- **1.3 Rear-wheel drift** — lateral grip curve that breaks away under throttle + steer.
- **1.4 Custom gravity multiplier** — extra downforce on elevation changes (rider weight shift).
- **1.5 Touch input** — 4-simultaneous-touch handling, no ghosting (per QA §2).

**Exit criteria:** a playable bike on a flat test track that is *fun to just ride*, with
`Update()`/`FixedUpdate()` showing **0 B** GC allocation in the Profiler.

---

## Phase 2 — Performance Envelope

Built early and deliberately, per the architecture's zero-allocation mandate.

- **2.1 Object pooling framework** — generic pool; zero `Instantiate` during gameplay.
- **2.2 ADPF thermal integration** — read Android thermal status, drive a quality ladder.
- **2.3 Dynamic resolution scaling** — URP render-scale step-down under `SEVERE`.
- **2.4 Addressables + ASTC** — texture streaming, VRAM budget enforcement.
- **2.5 GPU Resident Drawer** — enable and validate against draw-call targets.

**Exit criteria:** forced thermal event (`adb shell am broadcast … THERMAL_EVENT`) degrades
quality smoothly without a crash or frame spike; 2-hour endurance run shows flat memory.

---

## Phase 3 — World & Traffic (DOTS)

- **3.1 Procedural highway splines** — async generation ahead of the player.
- **3.2 DOTS/ECS civilian traffic** — hundreds of vehicles, no MonoBehaviours.
- **3.3 Biome variants** — Coastal, Desert, City.
- **3.4 Streaming & culling** — despawn behind, prewarm ahead.

**Exit criteria:** 200+ traffic entities sustained at target frame rate.

---

## Phase 4 — Combat

- **4.1 Animation Rigging IK** — hands lock to handlebars, detach to swing.
- **4.2 Melee system** — fists, chain, bat; hit detection at speed.
- **4.3 Weapon stealing** — transfer mechanic mid-combat.
- **4.4 Impulse reactions** — sideways force on hit; NaN-velocity guards (per QA §3).

---

## Phase 5 — AI & Heat

- **5.1 Rival FSM** — Race / Draft / Attack / Evade states.
- **5.2 Aggression multiplier** — escalates when the player attacks.
- **5.3 Police heat system** — spawn scaling on destructive behavior.
- **5.4 "Busted" sequence** — currency penalty; guard against the race-finish race condition.

---

## Phase 6 — Meta & Progression

- **6.1 Garage** — bike upgrades, weapon purchases.
- **6.2 Currency & persistence** — save/load.
- **6.3 Race structure** — events, tiers, unlocks.

---

## Phase 7 — Polish & Ship

- **7.1 Device fragmentation pass** — phones, foldables (inner/outer), tablets.
- **7.2 Edge-to-edge / UI anchoring** — no letterboxing.
- **7.3 Controller support** — Xbox / DualSense latency validation.
- **7.4 Store readiness** — Play Console, signed AAB, privacy policy.

---

## Cross-cutting, every phase

- Frametime variance held under **2 ms** (QA §1).
- No `new` in `Update()` / `FixedUpdate()` (Architecture §4).
- Every merged PR passes CI build + tests.
