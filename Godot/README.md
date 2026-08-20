# Highway Renegade — Godot Edition

Road Rash-style motorcycle combat racer. 100% open-source toolchain, zero
licensing, zero paid assets: Godot 4.7 (MIT), Blender-generated models,
Poly Haven CC0 textures/HDRIs, Python-synthesized audio.

## Play (desktop)

```bash
godot --path Godot
```

Controls: WASD/arrows drive · J/K punch left/right · L kick · Shift nitro ·
Esc pause. On Android everything is on-screen touch.

## Game

- **Campaign** — 5 chapters, classic rule: finish top 4 to advance.
- **Quick Race** — any unlocked track: Coast Run, Palm Desert, Downtown,
  Sierra Pass, Night City.
- **Garage** — 3 bikes, engine/tire upgrade stages, funded by race purses and
  knockout bonuses. Get busted by the police and the fine comes out of it.
- Combat: fists/chain/bat/kick with stamina; hard hits steal weapons.

## Build everything from a clean clone

```bash
Godot/tools/build.sh
```

That regenerates audio (`tools/gen_audio.py`), models
(`tools/blender/gen_assets.py`, headless Blender), runs the full test suite,
and exports `Godot/build/HighwayRenegade.apk`.

Android export needs the standard free pieces once per machine: Godot export
templates, JDK 17+, Android SDK build-tools + platform-tools, and a debug
keystore — paths configured in Godot's Editor Settings (`export/android/*`).

## Tests

```bash
godot --headless --script res://tests/run_tests.gd   # 52 logic/sim checks
godot --headless --script res://tests/smoke.gd       # boots every scene
xvfb-run godot --rendering-driver vulkan \
  --script res://tests/screenshot.gd                 # renders visual QA shots
```

The unit suite includes a 300-frame full-grid race simulation (rivals, police,
combat, bust rules) that runs entirely without a GPU.

## Architecture notes

- All movers use **track-space kinematics** (distance along the spline,
  lateral offset). Deterministic, cheap, headless-testable, and immune to
  physics tunneling at 200 km/h.
- Every track is data in `src/core/track_catalog.gd`; geometry, props,
  guardrails and lighting are generated per-biome by `src/race/track.gd`.
- Pure game rules (stamina, combat, campaign ledger, bike specs) live in
  `src/core/` with no scene dependencies — that's what the unit suite covers.
- Props/guardrails/traffic render via MultiMesh: a handful of draw calls.
- The road is one custom shader: CC0 asphalt + procedural, worn lane paint.
