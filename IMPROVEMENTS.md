# Improvement Plan — Risks, Debt & Quality

> Companion to [ROADMAP.md](ROADMAP.md) (features) and [ARCHITECTURE.md](ARCHITECTURE.md)
> (design). An earlier version claimed "2,842 lines, 54 tests, no persistence, no audio" —
> the line/test counts were stale and several "risks" have since been fixed. This version
> records what is actually true and what actually remains.

## Honest assessment

~12k lines of C# across ~75 files, 240+ EditMode tests, a CI pipeline that produces a
verified ~39 MB APK, and a domain layer that is genuinely well modelled and well tested.

**What the tests prove:** the pure logic is self-consistent — vehicle dynamics, combat maths,
race ordering, repair economy, the rival FSM, and now save round-tripping.

**What nothing yet proves:** that the bike is *fun*, that the frame rate holds under thermal
load, that touch latency is acceptable, or that the physics feels right in motion. Every
handling number is an educated guess until ridden on a device. That gap is the largest
remaining risk in the project.

---

## Resolved (this branch)

These were the release-blocking risks; they are fixed and guarded by tests/checkers:

- **Persistence works.** AES key was 30 bytes → every save silently failed. Fixed (SHA-256
  key derivation, per-write IV) and pinned with a round-trip test suite.
- **One save system**, one flow. The duplicate plain-JSON save path is deleted.
- **App lifecycle** — background/quit pauses the race and flushes the save.
- **Back button** reaches the game instead of closing the app on API 35.
- **Race finish** reaches the results screen; `GameOver` is handled.
- **Shippability** — Highway Renegade rename, explicit package id, monotonic `versionCode`,
  AAB build path, Linear colour space, deliberate stripping + `link.xml`, native symbols.
- **Debug instrumentation** stripped from runtime code, with a checker that fails the build
  if `[Bisect]`/`TEMPORARY` markers return.

---

## Open — structural (Phase 3)

- **`SceneLoader` duplication.** `Core/App/SceneLoader.cs` carries an NRE path and dead
  events, and is the *preferred* path from the main menu while the hardened flow is unused.
  Delete it and repoint the menu.
- **Core purity is not enforced.** A few MonoBehaviours remain in `HighwayRenegade.Core`;
  setting `noEngineReferences: true` after moving them makes the compiler enforce the pure
  boundary permanently.
- **`ISaveStore` seam** so corruption/partial-write/backup-recovery are testable without
  touching disk.
- **Pure `CampaignLedger`** extracted from `CampaignSession` (all money + grudge state, 0
  direct tests today).

---

## Open — performance & feel (Phase 4, device-led)

- Render budget over-spent by default: native res + MSAA *and* FXAA + full-screen post.
  Baseline should be ~0.75 render scale, one AA technique.
- `ThermalManager` never restores quality after a throttle, and its `TierChanged` event has
  **no subscribers** — the most expensive post passes never scale down.
- `ObjectPool<T>` is tested and used by nothing; runtime spawners still `Instantiate`/
  `Destroy`. Route spawns through the pool before content volume grows.
- `SettingsManager` applies expensive quality changes + `PlayerPrefs.Save()` on every slider
  tick — debounce it.
- `AndroidHaptics` needs `android.permission.VIBRATE` via a custom manifest (raw JNI, so
  Unity cannot infer it) or the vibration toggle silently does nothing.

---

## Open — content (Phase 5)

- Rival AI has no racing line (`AimPointAhead()` is identically zero) — a **prerequisite**
  for curved tracks.
- `TrackCatalog` has no production caller — 4 of 5 tracks are unreachable.
- No music playback; no countdown/weapon/damage/police/nitrous HUD feedback.
- Placeholder primitives throughout — needs CC0 art with per-asset licence vetting.

---

# Story Mode

Not in the original design surface, so this is proposed design. It plays to what is already
built: `RivalBrain` tracks an aggression multiplier that escalates when you hit a rider and
decays when you leave them alone, and `CampaignSession` already carries grudge state. Making
rivalry **persist across races** turns an existing mechanic into a story engine almost for
free.

## Proposed shape: a chapter spine with persistent rivals

**The spine (authored).** A handful of chapters, each a small set of races with an escalating
gatekeeper. Text beats between chapters — no cutscenes, no voice acting, nothing that needs
an art budget.

**The engine (emergent).** A persistent roster of named rivals who remember you:

- Wreck someone and their grudge carries into the next race — they hunt you specifically.
- Leave them alone and it cools over several races.
- Steal a rider's weapon and they turn up next race with a new one and a grudge
  (`disarmedByPlayer` is now wired for exactly this).
- Beat a gatekeeper cleanly and they respect you; win by wrecking them and they do not.

Two players' stories genuinely differ without authoring branches — the cheapest route to
narrative in a game with no art budget.

**What it needs:**

| Piece | State |
|---|---|
| Save system | ✅ (works now) |
| Damage attribution / disarm signal | ✅ wired |
| Persistent `RivalProfile` — name, grudge, loadout, record | small, mostly present |
| `Chapter` / `RaceEvent` data | `Campaign.cs` exists; needs `_eventId` wiring to trigger |
| Campaign progression + unlock gating | medium |
| Text beat presentation | small |

Almost all of it is pure logic — the same approach that made combat and AI cheap to verify.

## Alternatives considered

- **Pure career ladder** (no persistence between rivals) — cheapest, but throws away the
  grudge system that already exists and makes rivals interchangeable.
- **Authored narrative campaign** (dialogue trees, cutscenes) — highest production cost,
  needs writing and art the project does not have, and is the least Road-Rash-like option.
