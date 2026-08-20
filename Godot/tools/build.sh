#!/usr/bin/env bash
# One-command pipeline: regenerate assets, run every test, export the APK.
# Requirements (all free): godot 4.7+, blender 4.x, python3, JDK 17+,
# Android SDK build-tools + platform-tools, export templates installed.
set -euo pipefail
cd "$(dirname "$0")/.."

echo "== 1/5 audio =="
python3 tools/gen_audio.py

echo "== 2/5 models =="
blender --background --python tools/blender/gen_assets.py >/dev/null 2>&1
ls assets/models/*.glb

echo "== 3/5 import =="
godot --headless --import >/dev/null 2>&1 || true

echo "== 4/5 tests =="
godot --headless --script res://tests/run_tests.gd
godot --headless --script res://tests/smoke.gd | grep -q "SMOKE OK"
echo "smoke: OK"

echo "== 5/5 export =="
mkdir -p build
godot --headless --export-debug "Android" build/HighwayRenegade.apk
ls -lh build/HighwayRenegade.apk
echo "DONE"
