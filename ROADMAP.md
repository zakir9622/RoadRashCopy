# Development Roadmap — Project Highway Renegade

Derived from [GAME_DESIGN_DOCUMENT.md](GAME_DESIGN_DOCUMENT.md),
[ARCHITECTURE.md](ARCHITECTURE.md) and [QA_AND_TESTING_STRATEGY.md](QA_AND_TESTING_STRATEGY.md).

**Sequencing principle:** the performance envelope (Vulkan, ADPF, zero-alloc pooling) is
established *before* content volume grows. Retrofitting a zero-allocation policy onto an
existing codebase costs far more than building to it from commit one.

**Current state:** compiles clean on Unity 6000.0.81f1, **54/54 EditMode tests passing**,
playable test scene generated. Only the APK build is blocked — see [SETUP.md](SETUP.md).

---

## Phase 0 — Foundation ✅

| # | Deliverable | |
|---|---|---|
| 0.1 | Git repo, GitHub remote, Unity `.gitignore`, LFS | ✅ |
| 0.2 | Unity 6 skeleton, 5 layered assembly definitions | ✅ |
| 0.3 | CI: Android build + EditMode/PlayMode tests | ✅ |
| 0.4 | Unity 6000.0.81f1 LTS installed, licence active | ✅ |
| 0.5 | Headless compile/test/build driver ([Tools/unity.ps1](Tools/unity.ps1)) | ✅ |

---

## Phase 1 — Core Ride Feel ✅ *(tuning pending device testing)*

- **1.1** Sphere-cast suspension, spring/damper per wheel ✅
- **1.2** Arcade-sim handling, speed-sensitive steering falloff ✅
- **1.3** Rear-wheel drift via grip curve; slip angle derived from velocity ✅
- **1.4** Custom gravity multiplier + speed-scaled downforce ✅
- **1.5** Touch (4-finger), gamepad and keyboard input ✅

> Steering is **emergent** — the front wheel rotates and its grip force yaws the bike,
> rather than the body's yaw being set directly. Oversteer and counter-steer fall out of
> the physics rather than being scripted.

**Remaining:** every tuning number is an educated guess until ridden on a device.

---

## Phase 2 — Performance Envelope 🔶

- **2.1** Zero-allocation object pool + 6 tests ✅
- **2.2** ADPF thermal integration via JNI, with hysteresis ✅
- **2.3** Quality ladder: shadows → resolution → frame rate last ✅
- **2.4** Addressables + ASTC streaming ⬜
- **2.5** GPU Resident Drawer ⬜

> Frame rate is spent **last**. In a racer the frame rate *is* the handling feel, so
> resolution and shadows go first; 30 FPS only at Critical, to avoid an OS kill.

---

## Phase 3 — World & Traffic ⬜

- **3.1** Procedural highway splines, generated async ahead of the player
- **3.2** DOTS/ECS civilian traffic, hundreds of vehicles, no MonoBehaviours
- **3.3** Biomes: Coastal, Desert, City
- **3.4** Streaming and culling

> Requires re-adding `com.unity.entities` to the manifest — removed in Phase 0 to shrink
> package-resolution risk before anything depended on it.

---

## Phase 4 — Combat ✅ *(visuals pending)*

- **4.1** Animation Rigging IK — hands to handlebars ⬜ *(needs a rigged model)*
- **4.2** Melee: fists, chain, bat; arc + reach detection ✅
- **4.3** Weapon stealing ✅
- **4.4** Impulse knockback, i-frames, damage caps ✅

> Damage scales with **relative** speed. Two riders locked side by side at 200 km/h are
> stationary with respect to each other, so a punch feels like a punch.

---

## Phase 5 — Rival AI ✅

- **5.1** FSM: Race / Draft / Attack / Evade ✅
- **5.2** Aggression multiplier — escalates on hits, decays over time, capped ✅
- **5.3** Police heat system ⬜
- **5.4** "Busted" sequence ⬜ *(penalty maths done and tested)*

> The FSM is pure and Unity-free, covered by 17 tests. Entry/exit thresholds are
> deliberately asymmetric — symmetric ones make rivals flip state every frame at a
> boundary, which reads as a twitching, broken AI.

---

## Phase 6 — Meta & Progression 🔶

- **6.1** Race structure: countdown, standings, finish, payout ✅
- **6.2** Garage — bike upgrades, weapon purchases ⬜
- **6.3** Currency persistence / save-load ⬜
- **6.4** Event tiers and unlocks ⬜

---

## Phase 7 — Polish & Ship ⬜

- **7.1** Device fragmentation: phones, foldables, tablets
- **7.2** Edge-to-edge UI, no letterboxing
- **7.3** Controller latency validation (Xbox / DualSense)
- **7.4** Play Console, signed AAB, privacy policy, content rating

> Personal Play accounts need a **12-tester closed test for 14 days** before production.
> That is calendar time, not engineering time — start it early.

---

## Known gaps

| Gap | Why |
|---|---|
| **Art & audio** | All placeholder primitives. Needs an artist or asset packs. |
| **Device testing** | Frame rate, thermals and touch latency cannot be measured off-device. |
| **Is it fun?** | Unanswerable without a human riding it. The most important open question. |
| **PlayMode tests** | Only EditMode so far; physics behaviour is untested in motion. |

---

## Cross-cutting

- Frametime variance under **2 ms** (QA §1)
- No `new` in `Update()` / `FixedUpdate()` (Architecture §4)
- Every merged PR passes CI build + tests
