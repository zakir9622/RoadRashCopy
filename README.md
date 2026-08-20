# Road Rash

Godot 4.7 combat racer — illegal California circuit, 15-bike pack, fists/chain/bat/kick.
Open-source toolchain only (Godot MIT, Blender models, Poly Haven CC0, Kenney CC0, procedural SFX).

## Play

```bash
godot --path Godot
```

Desktop: WASD/arrows drive · J/K punch · L kick · Shift nitro · Esc pause.
Android: translucent steer pad (left) · GAS (right) · L/R punch, kick, nitro.

The race camera sits in the cockpit over the bars and rolls with lean.

Sideload the latest APK from [Releases](https://github.com/zakir9622/RoadRashCopy/releases).

## Game

- **Big Game** — 5 divisions × 5 events. Start last, finish top 4, don't get busted.
- **$1000** and a Panda 250. Shop: Panda / Shuriken / Kamikaze / Diablo at Olley's.
- **Der Panzer Klub** between races. Natasha grudges. Broke = game over.
- Tracks: Pacific Coast, Palm Desert, The City, Sierra Nevada, Night City.
- Cops immune to punches; bust only while crashed or running. Cows, deer, oil, traffic.

## Tests (the review gate)

Every PR and `main` push runs this and nothing else:

```bash
cd Godot
godot --headless --script res://tests/run_tests.gd   # logic + 300-frame sim
godot --headless --script res://tests/smoke.gd       # Menu / Race / Garage boot
```

That is CI **Review**. It does not export an APK, so it stays fast.

## APK

Local (needs Godot export templates, JDK 17, Android SDK):

```bash
Godot/tools/build.sh
```

CI exports a debug-signed arm64 APK only after Review passes, and only on **push to `main`** or **Actions → CI → Run workflow**. The APK is uploaded as an artifact and published as a GitHub Release. Pull requests do not build it.

## Layout

```
Godot/src/core     rules (stamina, combat, campaign, story) — no scenes
Godot/src/race     track-space kinematics, AI, camera
Godot/src/ui       menu, HUD, garage, results
Godot/tests        headless suite
Godot/tools        blender models, audio, CI helpers, APK pipeline
```

Movers live in track space (distance + lateral). Tracks are data in `track_catalog.gd`.
The Unity project, Unity CI, and CompileCheck tools are gone. This repo is Godot only.
