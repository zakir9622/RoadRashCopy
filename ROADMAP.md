# Development Roadmap — Highway Renegade

> Tracks *features and phases*. For risk/debt/quality see [IMPROVEMENTS.md](IMPROVEMENTS.md);
> for the real design see [ARCHITECTURE.md](ARCHITECTURE.md).
>
> An earlier version of this file described a system nobody built — DOTS/ECS traffic,
> Addressables streaming, a GPU Resident Drawer, "54 tests on Unity 6000.0.81f1". None of
> that exists and the version numbers were wrong. This version records what the project
> actually is and where it is actually going.

**Current state:** Unity **6000.0.38f1** + URP 17, Vulkan-only, ARM64/IL2CPP, Linear colour
space. **~12k lines of C# across ~75 files, 240+ EditMode tests.** The CI pipeline builds a
verified ~39 MB APK end to end. The domain layer (`HighwayRenegade.Core`) is pure C# and
well tested; the MonoBehaviour glue layer is thinner on coverage and is where the remaining
correctness work lives.

The sequencing principle is **correctness and shippability first, content second** — a
racer that forgets your progress or closes on the back button is not improved by more tracks.

## Post-merge status (2026-08-10)

The hardening pass (Phases 0–2, guardrails, docs, and the partial Phase 3/4/5 items below)
is **merged to `main`**. The pipeline is green and self-verifying on every PR. Next-phase
work continues on a fresh branch off `main`.

**Next up, in order:**
1. **Content reachability** — the biggest gap between "shippable" and "a game".
   - ✅ **Campaign is reachable.** The menu picks the next event via `Campaign.NextEvent(save)`
     and hands its id to the race through `CampaignSession.PendingEventId`, so races now record
     campaign progress (purse, chapter advance, gatekeeper) instead of being anonymous free
     runs, and the grid uses the event's authored rivals. Unit-tested.
   - ⬜ Track selection / `TrackCatalog` into generation so the 4-of-5 unreachable tracks run,
     plus `TrackProgress.SetSpline` for real distance-along-spline ranking on curved tracks.
2. **The `noEngineReferences: true` refactor** — a pure-vector rewrite of `TrackSpline`, then
   relocate the six remaining engine-coupled `Core` files to Gameplay, then flip the flag so
   the pure boundary is compiler-enforced forever. Done as one deliberate, CI-verified change.
3. **Device-led items** (need the user): on-device physics/handling tuning, render-budget
   baseline, CC0 art + icons, permanent package-ID confirmation, music.

---

## Phase 0 — Foundation ✅

- Git + GitHub + LFS, Unity `.gitignore`
- Five layered runtime assemblies + two test assemblies, acyclic dependency graph
- Scenes generated from code (reviewable, rebuildable from a clean clone)
- CI: Android build + EditMode/PlayMode test jobs on game-ci
- Headless build/test driver

---

## Phase 1 — Correctness ✅

The release-blocking correctness defects found in the senior review, all fixed on this
branch:

- **Persistence actually works.** The AES key was 30 bytes (AES accepts 16/24/32), so every
  save silently threw and was swallowed — the game forgot everything. Key is now derived by
  SHA-256 (32 bytes by construction) with a length assertion, per-write random IV, and a
  round-trip test suite that would have caught the original bug instantly.
- **One save system.** The second, plain-JSON `Core/Progression/SaveSystem.cs` is deleted;
  `SaveService` is the single source of truth.
- **App lifecycle.** `AppLifecycle` pauses the race, mutes audio and flushes the save on
  background/quit, before Android can reap the process.
- **Back button.** Predictive back is off and the hardware back button reaches the game
  (pause), rather than closing the app mid-race on API 35.
- **Race finish reaches the results screen**; `GameOver` is a handled end state, not a
  soft-lock.
- One-liner fixes: inverted `PoliceAI` session guard, `NotifyDisarmed` wiring, singleton
  `Instance` nulling in `OnDestroy`.

---

## Phase 2 — Shippability ✅

- Renamed to **Highway Renegade** (off the "Road Rash" trademark); explicit
  `applicationIdentifier`.
- `versionCode` derived from `GITHUB_RUN_NUMBER` (monotonic — Play rejects a re-used code).
- CI can build an **AAB** (`-buildApk` is conditional on the format input); release builds
  fail fast without a keystore rather than shipping debug-signed.
- **Linear** colour space (PBR/URP renders correctly).
- Managed stripping set deliberately with a `link.xml` so `JsonUtility` save fields survive
  on device.
- Native debug symbols enabled for Play Console crash symbolication.
- Orientation locked to landscape (both sides).

**Remaining before a first upload:** confirm the final `applicationIdentifier`
(`com.highwayrenegade.game` is the placeholder — it is *permanent once published*), and app
icons.

---

## Phase 3 — Structural refactor 🔶

Deleting duplication rather than patching around it, so the app-layer bug class cannot
recur.

- ✅ Reset `GameStateManager` statics via `[RuntimeInitializeOnLoadMethod]` (they survive
  the editor's domain-reload-off, which broke PlayMode test isolation).
- ✅ Delete `Core/App/SceneLoader.cs`; repoint `MainMenuScreen` at the hardened
  `GameFlowManager` flow (it refuses to change state when the target scene is missing from
  the build settings, the desync bug `SceneLoader` had).
- ⬜ `noEngineReferences: true` on `HighwayRenegade.Core`. The domain layer (Vehicle,
  Combat, AI, RaceRules, Story, SaveData) is *already* engine-free; only seven files still
  touch `UnityEngine`. Six of them (`Pooling/*`, `App/GameFlowManager`, `App/GameStateManager`,
  `App/SettingsManager`, `App/CrashReporter`) are relocatable to Gameplay. The blocker is
  `Race/TrackSpline`: it is built on `Vector3`/`Mathf` and is referenced by `Race/TrackDefinition`
  (which must stay in Core as domain data), so the flip needs a pure-vector rewrite of the
  spline first. Moving the six without that rewrite is churn with no enforcement payoff, so
  it is deferred as one deliberate change.
- ⬜ Extract `ISaveStore` behind `SaveService` (test corruption/partial-write/backup
  recovery without touching disk).
- ⬜ Extract a pure `CampaignLedger` from `CampaignSession`.

---

## Phase 4 — Performance & feel (device-led) ⬜

- Baseline render scale ~0.75; one AA technique, not MSAA *and* FXAA.
- Wire `ThermalManager.TierChanged` to the post stack (today it has no subscribers, so the
  most expensive passes never scale down); restore quality state when a race ends.
- Route runtime spawners through the existing, tested `ObjectPool<T>`.
- Debounce settings commits; cache the `PowerManager` JNI ref.
- **On-device tuning pass.** Every physics number is an educated guess until ridden — the
  single largest risk to "high end".

---

## Phase 5 — Content & high-end ⬜

- Racing line from `TrackSpline` into `RivalAIController` (**prerequisite** for curved
  tracks — the AI currently cannot steer through a curve).
- Route `TrackCatalog` into generation + a track-select screen (4 of 5 tracks are currently
  unreachable).
- Campaign event wiring (`_eventId`), so `Campaign.cs` is actually triggered.
- CC0 art (Kenney / Quaternius / Poly Haven) with per-asset licence vetting; strong
  lighting/VFX to carry the look.
- Music + AudioMixer (there is currently no music playback).
- HUD combat feedback: countdown, weapon, damage direction, police warning, nitrous.
- Safe-area handling (the NITRO button currently risks the gesture bar).

---

## Known gaps requiring the user / a device

| Gap | Why |
|---|---|
| Physics/handling tuning | Cannot be measured off-device; needs riding. |
| CC0 model licence vetting | Each asset's licence must be confirmed before shipping. |
| Final package ID + icons | Package ID is permanent once published; needs a decision. |
| "Is it fun?" | Unanswerable without a human riding it. |

---

## Cross-cutting invariants

- No `new` in `Update()` / `FixedUpdate()` on the hot paths.
- Every push keeps the static checkers and EditMode tests green.
- PlayMode stays advisory in CI pending the upstream game-ci hang
  (game-ci/unity-test-runner#188).
