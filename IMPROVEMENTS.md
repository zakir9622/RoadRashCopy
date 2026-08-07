# Improvement Plan

Written 2026-08-08, against commit `f2b5a3b`. Companion to [ROADMAP.md](ROADMAP.md),
which tracks *features*; this tracks *risks, debt, and quality*.

---

## Honest assessment

2,842 lines of C#, 54 passing tests, a compiling project and a generated playable scene.
But it is worth being precise about what that does and does not prove.

**What the tests prove:** the pure logic is self-consistent. Damage maths, AI state
transitions, race ordering, pool bookkeeping.

**What nothing yet proves:** that the bike is fun, that the frame rate holds, that touch
input is responsive, that physics behaves in motion, or that the thing runs on a phone at
all. Every physics tuning number is an educated guess.

The gap between "54 tests pass" and "a good game" is almost the entire remaining project.

---

## P0 — Blocking, do first

### 0.1 Ship an APK and ride it
Nothing below is worth doing until the bike has been ridden. Tuning is guesswork otherwise,
and tuning is the highest-risk item in the whole project.
**Blocked on:** the Android module UAC click ([SETUP.md](SETUP.md)).

### 0.2 PlayMode tests for physics
The single largest hole in the test suite. EditMode tests cannot run physics — every
suspension, grip, and gravity behaviour is currently unverified in motion.

Worth asserting:
- Bike settles to a stable ride height and stays there (no pogo, no sink)
- Comes to rest without drifting on flat ground
- Survives a T-bone at max speed without NaN velocity (QA §3 calls this out explicitly)
- Recovers to upright after a bad landing
- `FixedUpdate` allocates 0 B over 500 steps

That last one is the only real enforcement of the zero-allocation mandate. Right now it
is a policy in a comment, not a check.

### 0.3 Wire up the object pool
`ObjectPool<T>` is built, tested, and **used by nothing**. Currently harmless because
nothing spawns at runtime, but the moment traffic, VFX, or weapons land it must be the
spawn path, or the mandate is quietly abandoned. Add a `PoolRegistry` and route all
runtime spawns through it before Phase 3 starts.

---

## P1 — Structural problems that get more expensive with time

### 1.1 Race progress is hardcoded to world Z
`RaceManager` measures progress as `transform.position.z`. That works only for a straight
track. The moment Phase 3 introduces curved procedural highways, standings break silently
and confusingly.

**Fix:** introduce a `TrackProgress` abstraction (distance along spline) now, backed by Z
for the placeholder track. Cheap today, invasive later.

### 1.2 Damage has no attributed source
`Damageable.ApplyDamage` does not record who dealt the hit. Consequences:
- Rivals get angry at the player for traffic collisions (documented in the code as a
  deliberate temporary simplification)
- No kill/assist attribution, no "revenge" targeting, no scoring

**Fix:** add `GameObject source` to the damage call and the `Damaged` event.

### 1.3 No persistence
Currency, unlocks, progress — all vanish on quit. Blocks the entire progression phase and
any story mode.
**Fix:** a `SaveData` DTO + JSON to `Application.persistentDataPath`, with a schema version
field from day one so saves can migrate rather than being wiped.

### 1.4 No audio whatsoever
Engine note is the primary speed cue after FOV. A racing game without engine audio feels
fundamentally broken, and it is cheap to add: one looping clip with pitch mapped to speed.

### 1.5 HUD is IMGUI
Fine as a placeholder, explicitly not production. IMGUI allocates on layout and cannot be
styled to a shippable standard. Migrate to UI Toolkit at Phase 6.

---

## P2 — Quality and robustness

### 2.1 Air-righting fails when fully inverted
`ApplyAirRighting` uses `Vector3.Cross(transform.up, Vector3.up)`, whose magnitude is zero
at both 0° and 180°. A perfectly inverted bike gets no correction torque. It is an unstable
equilibrium so any nudge breaks it, but a deterministic flip could stick.
**Fix:** fall back to a fixed axis when the cross product is near zero.

### 2.2 Grip force is very stiff
`lateralAccel = -lateralVel * grip / dt` at `dt = 0.02` produces ~28,000 N for a modest
slide. It works, but is close to the range where the solver can go unstable on a hard
collision. Worth watching once the bike is on-device; consider clamping peak tyre force.

### 2.3 No CI build verification
`.github/workflows/build-android.yml` exists but has never run green — it needs
`UNITY_LICENSE`, `UNITY_EMAIL`, `UNITY_PASSWORD` secrets. Until then the pipeline is
decorative and regressions can land unnoticed.

### 2.4 Allocation verification needs on-device profiling
The PlayMode allocation test measures a differential (harness baseline vs bike enabled)
because Unity's PlayMode test runner allocates ~5 MB per 200 frames on its own, swamping
anything the bike does. The ~1.5 MB attributable to the bike is *believed* to be Unity's
contact processing rather than managed allocation in our code — the bike model uses only
non-allocating physics queries — but that cannot be proven from batch mode.

**Fix:** a Profiler capture on a real device, checking `GC.Alloc` samples inside
`BikeController.FixedUpdate`. Until then the test is a regression guard, not proof.

### 2.5 No crash/analytics reporting
Shipping without crash reporting means player-side crashes are invisible. The stated goal
is a crash-free release, which is unmeasurable without telemetry.

### 2.6 Single scene, no flow
No menu, no restart, no scene transitions. The APK boots straight into one race with no
way out.

---

## P3 — Content and polish

- Art and audio pipeline (currently 100% primitives)
- Police / heat system (Phase 5.3, penalty maths already tested)
- Garage, upgrades, bike roster
- Foldable and tablet layouts
- Localisation scaffolding, if non-English markets matter

---

## Suggested order

| Step | Why here |
|---|---|
| 1. Android module → APK → ride it | Everything downstream is guesswork until this happens |
| 2. PlayMode physics tests | Locks in behaviour before tuning changes it |
| 3. Tune ride feel on-device | The highest-risk item in the project |
| 4. Engine audio | Cheapest large improvement to game feel |
| 5. `TrackProgress` + damage source | Structural, cheap now, invasive later |
| 6. Save system | Unblocks progression and story mode |
| 7. Story mode spine | See below |
| 8. Traffic (DOTS) | Big; wants the pool wired first |
| 9. Police / heat | Builds on damage attribution |
| 10. Art pass, UI Toolkit, store prep | Last, once the game is proven fun |

---

# Story Mode

Not in the original GDD, so this is new design surface.

## What fits this game

Road Rash's own "story" was thin by modern standards — a ladder of five divisions, rival
racers with names and personalities, and money as the through-line. The narrative came
from **rivalry**, not cutscenes.

That matters here because the project already has the machinery for rivalry:
`RivalBrain` tracks an aggression multiplier that escalates when you hit a rider and
decays when you leave them alone. Today it resets every race. Making it **persist across
races** turns a mechanic that already exists into a story engine, almost for free.

## Proposed shape: a chapter spine with persistent rivals

**The spine (authored).** Five chapters, each a small set of races with an escalating
gatekeeper. Text beats between chapters — no cutscenes, no voice acting, nothing that
needs an art budget.

**The engine (emergent).** A persistent roster of named rivals who remember you:

- Wreck someone and their grudge carries into the next race. They hunt you specifically.
- Leave them alone and it cools over several races.
- Steal a rider's bat and they turn up next race with a chain and a grudge.
- Beat a gatekeeper cleanly and they respect you; win by wrecking them and they do not.

The result is that two players' stories genuinely differ, without authoring branches.
This is the cheapest possible route to narrative in a game with no art budget, and it
plays to what is already built.

**What it needs:**

| Piece | Effort |
|---|---|
| Save system (P1.3) | prerequisite |
| Persistent `RivalProfile` — name, grudge, loadout, record | small |
| `Chapter` / `RaceEvent` data as ScriptableObjects | small |
| Campaign progression + unlock gating | medium |
| Text beat presentation | small |
| Damage attribution (P1.2) | prerequisite for grudges |

Almost all of it is pure logic, which means almost all of it is unit-testable — the same
approach that made combat and AI cheap to verify.

## Alternatives considered

- **Pure career ladder** (divisions, no persistence between rivals) — cheapest, but throws
  away the grudge system that is already built and makes rivals interchangeable.
- **Authored narrative campaign** (characters, dialogue trees, cutscenes) — highest
  production cost, needs writing and art the project does not have, and is the least
  Road-Rash-like of the options.
