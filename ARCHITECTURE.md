# System Architecture & Technical Guidelines

> Describes what the project **is**, not what it might one day be. An earlier version of
> this file mandated a GPU Resident Drawer, Addressables texture streaming, DOTS/ECS
> traffic and a zero-allocation object pool wired through every spawn — none of which
> exist. A spec that describes an unbuilt system misleads every contributor who trusts it,
> so aspirational items now live in ROADMAP.md under the phase that would add them, and
> this file records the real design.

## Assemblies

Five runtime assemblies plus two test assemblies, layered so the dependency graph is
acyclic:

- **HighwayRenegade.Core** — the domain logic. Vehicle dynamics, tyre/powertrain models,
  combat maths, race rules, repair economy, the rival FSM, the campaign and save data
  types. Most of it is pure C# and unit-tested (~180 tests); this is the strongest part of
  the codebase. (It still contains a few MonoBehaviours in `Core/App` and `Core/Pooling`
  that belong in the gameplay layer — moving them out and turning on `noEngineReferences`
  is tracked in ROADMAP Phase 3.)
- **HighwayRenegade.Gameplay** — the MonoBehaviour layer. Bike controller, AI glue,
  spawners, UI screens, audio, progression session, app lifecycle. Thin glue over Core.
- **HighwayRenegade.Editor** — build script and the scene generators.
- **HighwayRenegade.Performance** — the thermal/quality manager.
- **HighwayRenegade.Platform** — the Android haptics JNI bridge.

Pattern: pure rule classes in Core, thin MonoBehaviour glue in Gameplay. That split is why
combat balance, race ordering and the AI state machine are unit-testable without a running
scene.

## Scenes are generated, not authored

`.unity` files are opaque YAML that cannot be reviewed in a diff. The three scenes
(TestTrack, MainMenu, Garage) are built from code by the editor generators, so the entire
level and front end stay reviewable and rebuildable from a clean clone. `BuildScript`
regenerates them on every build and commits the output back to `main`, so the generator is
the source of truth and the committed scene is the record of what shipped.

## Rendering (URP + Vulkan)

URP is generated and assigned by `RenderPipelineGenerator` (the project would otherwise
fall back to the built-in pipeline). Forward rendering, one shadow cascade, Linear colour
space, ASTC textures, Vulkan-only. Post-processing is a runtime volume stack. Thermal
scaling of render scale and shadows is handled by `ThermalManager` via ADPF.

## Performance framework (ADPF)

`ThermalManager` polls Android's thermal status and steps quality down under sustained
load — shadows and resolution first, frame rate last, because in a racer the frame rate is
the handling feel.

## Persistence

One save system: `SaveService`, AES-obfuscated (not secure — the key ships in the APK),
atomic temp-then-move writes, automatic backup recovery, schema migration. `AppLifecycle`
flushes it when the app is backgrounded, before Android can reap the process.

## Memory

The project ships `ObjectPool<T>`, tested but not yet wired through the runtime spawners —
routing traffic/scenery/VFX through it is tracked in ROADMAP Phase 4. The zero-allocation
discipline is real in the hot paths that exist today (the HUD pre-builds its number
strings; the physics step uses non-allocating queries).
