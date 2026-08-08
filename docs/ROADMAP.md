# Highway Renegade — development plan

How this project gets from a working skeleton to a game worth shipping, using only
free and open-source resources.

Written 2026-08-08, against merge commit `514e6ed`.

---

## 1. Where the project actually stands

Measured, not estimated.

| | |
|---|---|
| C# | ~10,400 lines across 64 scripts, 5 assemblies |
| Tests | 193 passing (178 EditMode, 15 PlayMode), ~7 min in CI |
| Build | Android APK, IL2CPP, ARM64, Vulkan-only, API 30+ |
| Pipeline | Green. Four static guards run licence-free on every push |
| Scenes | 1 committed (`TestTrack`), 2 generated at build time (`MainMenu`, `Garage`) |
| Art | **18 CC0 textures/HDRIs, fetched on demand. No models, no animations, no audio files** |

**What works.** Bike physics (powertrain, tyre model, riding assists, crash rules),
rival AI with persistent grudges, police pursuit and busting, melee combat with weapon
stealing, a campaign with chapters, save/load with schema migration, a repair economy,
thermal throttling management, and a complete UI Toolkit front end.

**What does not exist yet.** Anything that makes it *look* like a game. Every visual is
a runtime primitive — the bike is a block and two discs. There is no rider, no
animation, no engine audio recording, no building, no city.

**The honest gap.** This is a well-engineered simulation with no art. That is a much
better place to be than the reverse, but it means "does it look like Road Rash" is
currently answered by: no, not at all.

---

## 2. Asset acquisition — solved, and automated

The blocker on everything visual was that somebody had to manually download art. That
is now a command:

```bash
python3 Tools/Assets/fetch-assets.py
```

`Tools/Assets/manifest.json` lists every asset with its licence; the fetcher downloads
them, pins SHA-256 for each, and regenerates `Assets/_Project/Art/ATTRIBUTIONS.md`. It
**refuses** any licence not on an allow-list, so an unclear licence fails the run
instead of quietly shipping.

Binaries are gitignored on purpose. The repository stores the recipe, not the output —
the same reasoning that keeps scenes as generators rather than committed YAML, and it
keeps a game's worth of textures off GitHub's 1 GB LFS free tier.

### Verified-reachable sources

| Source | Licence | What it gives us | Status |
|---|---|---|---|
| **Poly Haven** | CC0 | PBR textures, HDRIs | **Working** — 18 assets fetched |
| **ambientCG** | CC0 | PBR materials, decals | API verified, adapter written |
| **Kenney** | CC0 | Low-poly vehicles, props, UI, audio | Reachable; needs correct download paths |
| **Quaternius** | CC0 | Low-poly vehicles, characters, nature | To add |
| **OpenGameArt** | Mixed — filter CC0 | Props, textures, music | Reachable |
| **Freesound** | CC0 / CC-BY | Engine, impact, ambience | Reachable; CC-BY needs credit, which the fetcher handles |
| **Sketchfab** | Filter CC0 / CC-BY | Motorcycles, street furniture | Manual download; licence per asset |
| **Mixamo** | Free, royalty-free | Rigged humanoids + animations | Free Adobe account, manual export |
| **Blender** | GPL, free | Retopology, LOD baking, atlasing | Local tool |

No paid frameworks anywhere in this plan.

### The one thing CC0 libraries will not give you

**A rigged motorcycle rider with racing animations.** Free bike *models* exist in
quantity; a rider rigged and animated for lean, brake-tuck, punch, kick and dismount
does not, as a coherent CC0 set. The realistic route:

1. Mixamo humanoid (free) → retarget to Unity Humanoid.
2. Mixamo has no bike animations. Author lean/tuck/swing poses in Blender against the
   Mixamo rig — a handful of additive clips layered over a single idle-on-bike pose.
3. Combat swings can be adapted from Mixamo's punch/melee set, which does exist.

This is the largest genuinely manual item in the plan and should not be estimated as
"download it".

---

## 3. What "realistic" means on a phone, and how to get it

Realism on mobile is not more polygons. At 60 fps on ARM with a Vulkan-only, ARM64
build, it comes from four things, in this order of payoff:

**1. Lighting and sky (largest visual return per unit of cost).**
The two HDRIs already fetched drive image-based lighting. Baked lightmaps for static
geometry, one real-time directional light for the sun, reflection probes at intervals
along the road. A correctly lit primitive reads as more real than a detailed model
under flat ambient.

**2. Materials that respond correctly.**
The `arm` textures fetched pack ambient-occlusion, roughness and metallic into one
texture — one sampler instead of three, which matters on a tile-based mobile GPU. Wet
asphalt, sun glare on a fuel tank, and dusty verge come from roughness variation, not
geometry.

**3. Motion and camera.**
Speed is *felt*, not modelled: field-of-view that widens with velocity, camera shake
proportional to surface roughness, motion blur on the periphery, screen-space speed
lines above a threshold, chromatic aberration under damage. Cinemachine (free) handles
the rig; the existing `ChaseCamera` becomes its driver.

**4. Geometry, last.**
Low-poly models with good normal maps and correct LODs. A 3,000-triangle bike with a
baked normal map is indistinguishable from a 50,000-triangle one at racing speed and
costs a fifteenth of the bandwidth.

### Free Unity packages this uses

All first-party and free: URP, Shader Graph, VFX Graph, Cinemachine, Splines,
ProBuilder, Terrain Tools, Addressables, Burst, Jobs, Input System.

---

## 4. World and scenes

The current track is a 1,200 m straight generated from code. The path to real
environments keeps that generator and grows it.

**Phase A — the road becomes real.** `SplineHighwayGenerator` already exists and is
wired. Extend it to emit a mesh with proper UVs, apply the fetched asphalt material,
add shoulder blending and lane markings as decals.

**Phase B — biomes.** `LevelBiome` already exists as an enum with nothing behind it.
Give each biome a data asset: sky HDRI, fog colour and density, verge material, prop
set, traffic mix, music. Coast, desert, city, forest, night city. One generator, five
looks.

**Phase C — set dressing at density.** Buildings, overpasses, signage, barriers,
roadside furniture, spawned along the spline via the existing `ScenerySpawner` and
pooled through `ObjectPool`. GPU instancing for anything repeated.

**Phase D — streaming.** Addressables to load biome content per race so the APK stays
small and memory stays flat. This is what keeps a large game from crashing on a 4 GB
device.

---

## 5. NPCs

Current: three rivals with skill/aggression variation and persistent grudges, plus one
police unit.

| NPC | Now | Target |
|---|---|---|
| Rivals | 3, generic, roster-paired | 8–12 named riders, per-chapter, distinct bikes/liveries, personality-driven lines |
| Police | 1 chaser | Motorcycle units, roadblocks, a helicopter spotter at high heat |
| Traffic | Kinematic cubes, one lane behaviour | Cars, vans, buses, trucks with lane-change, braking, horns, and mirrors that react |
| Pedestrians | None | Crossings, pavement crowds, scatter reactions |
| Livestock | Placeholder template | Cows and deer that wander into the road — a Road Rash staple |

The AI substrate is already there: `RivalBrain` is pure logic and unit-tested, so new
behaviours are additions to a tested core rather than new systems.

---

## 6. Gameplay mechanics

Ordered by how much each adds per unit of work.

**Tier 1 — completes the core loop**
- Weapon variety beyond fists/chain/bat: crowbar, helmet swing, kick
- Traffic and scenery collisions that ragdoll the rider properly
- Nitro / slipstream draughting behind traffic
- Wheelies and stoppies as controlled, scoring states
- Race intro/countdown and a proper finish sequence

**Tier 2 — depth**
- Bike upgrades that visibly change the model, not just stats
- Rival rivalries that escalate across chapters with dialogue
- Police heat that persists between races
- Weather: rain reducing grip through the existing `TyreModel`, wet reflections
- Day/night cycle driven by the two HDRIs already fetched

**Tier 3 — retention**
- Time trials and ghost racing
- Challenge events (survive the police, no-crash runs)
- Cosmetic liveries
- Local leaderboards

Every one of these lands on a tested rules layer — `CombatMath`, `RaceRules`,
`RepairRules`, `CrashRules` are all pure C# with unit tests, so new mechanics get
tested at the maths level before any art exists.

---

## 7. Reliability — how "no crashes" is actually achieved

Not by hoping. By these, most of which already exist:

**Already in place**
- Four static guards on every push: compilation (both player and editor configs),
  serialized-reference names, per-assembly references, and reachability
- 193 automated tests
- An allocation regression test that already caught a per-frame string allocation
- `ThermalManager` for sustained-performance throttling
- `ObjectPool` so spawning does not churn the heap

**To add**
1. **Null-safety at every scene boundary.** The single largest crash source in this
   codebase's history was references that came back null in generated scenes. The
   `UIScreen.Require<T>` pattern — report loudly, degrade gracefully, never throw —
   should be applied to every system that reaches across a scene.
2. **A boot smoke test.** Menu → race → finish → results → garage → menu, asserted end
   to end in PlayMode. Currently missing, and it is the single most valuable test the
   project does not have.
3. **Device matrix testing.** Free: Firebase Test Lab's free tier, plus any physical
   Android device. This is the only way to catch Vulkan driver issues, thermal
   behaviour and real memory limits.
4. **Crash reporting.** Unity Cloud Diagnostics is free at this scale; Sentry has a
   free tier. Without it, a crash on a user's device is invisible.
5. **Memory budget enforcement.** A test that fails if a loaded race scene exceeds a
   fixed budget. On a 4 GB device, exceeding it is not a slowdown, it is a kill.
6. **Graceful degradation.** Quality tiers already exist in settings; bind them to
   actual LOD bias, shadow distance, and traffic density.

### Performance budgets (60 fps on a mid-range 2022 Android)

| Budget | Target |
|---|---|
| Frame time | 16.6 ms |
| Draw calls | < 150 |
| Triangles | < 300k |
| Texture memory | < 512 MB |
| Steady-state GC allocation | 0 bytes/frame |
| APK size | < 150 MB |

The zero-allocation target is already being enforced by a test, and already caught one
regression.

---

## 8. Phasing

Each phase ends with something runnable, not a checkpoint on paper.

**Phase 1 — Make it look like a road.** Import the fetched textures, build the material
set, apply the HDRI sky and lighting, generate a proper road mesh with markings.
*Done when: a screenshot reads as a real road.*

**Phase 2 — Put a bike and a rider on it.** Source a CC0 motorcycle, retopologise and
LOD it in Blender, Mixamo rider retargeted, lean and tuck poses authored.
*Done when: the thing you steer looks like a motorcycle with a person on it.*

**Phase 3 — Fill the world.** Traffic vehicle models, roadside props, buildings, one
complete biome dressed end to end.
*Done when: a full 1,200 m has nothing placeholder in it.*

**Phase 4 — Make it feel fast.** Cinemachine rig, speed FOV, motion blur, particles,
engine audio, impact audio, haptics tuning.
*Done when: it feels fast without the speedometer.*

**Phase 5 — Content.** Five biomes, 8–12 rivals, the full campaign, Tier 1 and 2
mechanics.
*Done when: there is more than one race to play.*

**Phase 6 — Ship.** Device matrix, crash reporting, memory budgets, store listing.
*Done when: it survives a week on real devices.*

---

## 9. Division of labour

Being precise about this matters more than the plan itself.

**I can do autonomously**
- All C# — systems, mechanics, AI, tools
- Scene generators, material setup, shader graphs as code
- Asset acquisition via the fetcher, and extending it to new CC0 sources
- Tests, static guards, CI, build configuration
- Diagnosing and fixing CI failures

**I cannot do**
- **Run the game.** I have no Unity licence locally and no display. Every visual claim
  is unverified until a human or CI runs it.
- **See images.** I cannot check whether a texture looks right, or judge a screenshot.
- **Model or animate in Blender.** I can write import scripts, not sculpt a fuel tank.
- **Test on a device.** Thermal behaviour, Vulkan drivers and real memory limits need
  hardware.
- **Judge whether it is fun.** That is the one thing that decides whether this is worth
  finishing, and only a person playing it can answer it.

**The highest-value thing anyone can do right now** is install the current APK and play
one race. Everything above is built on the assumption that the physics feel good, and
nobody has confirmed that.

---

## 10. Immediate next steps

1. Fetch the assets and import them (`python3 Tools/Assets/fetch-assets.py`)
2. Build the road material and apply the HDRI sky — Phase 1
3. Add the boot smoke test (menu → race → results → garage)
4. Source a CC0 motorcycle and get it in the game — Phase 2
5. Play the APK and report what actually feels wrong
