#!/usr/bin/env bash
# Activates Unity with the user's licence and builds the release APK.
#
# Needs (as environment variables, e.g. injected Cloud Agent secrets):
#   UNITY_LICENSE  - full contents of Unity_lic.ulf   (preferred)
#     or UNITY_SERIAL + UNITY_EMAIL + UNITY_PASSWORD  (Plus/Pro serial route)
set -uo pipefail
cd "$(dirname "$0")"

UNITY="/home/ubuntu/unity/6000.0.38f1/Editor/Unity"
PROJECT="/workspace"
LOG="/home/ubuntu/unity-setup/build.log"

if [[ -n "${UNITY_LICENSE:-}" ]]; then
  mkdir -p ~/.local/share/unity3d/Unity
  printf '%s' "$UNITY_LICENSE" > ~/.local/share/unity3d/Unity/Unity_lic.ulf
  echo "Wrote Unity_lic.ulf ($(wc -c < ~/.local/share/unity3d/Unity/Unity_lic.ulf) bytes)"
elif [[ -n "${UNITY_SERIAL:-}" && -n "${UNITY_EMAIL:-}" && -n "${UNITY_PASSWORD:-}" ]]; then
  echo "Activating with serial"
  "$UNITY" -batchmode -nographics -quit \
    -serial "$UNITY_SERIAL" -username "$UNITY_EMAIL" -password "$UNITY_PASSWORD" \
    -logfile /dev/stdout | tail -20
else
  echo "ERROR: no UNITY_LICENSE (.ulf contents) or UNITY_SERIAL+UNITY_EMAIL+UNITY_PASSWORD in the environment."
  exit 1
fi

echo "=== building APK ==="
"$UNITY" -batchmode -nographics -quit \
  -projectPath "$PROJECT" \
  -buildTarget Android \
  -executeMethod HighwayRenegade.Editor.BuildScript.BuildAndroid \
  -customBuildPath build/Android -buildApk \
  -logfile "$LOG"
code=$?

echo "=== unity exit: $code ==="
tail -40 "$LOG"

apk=$(find "$PROJECT/build" -type f -name '*.apk' -size +5M | head -1)
if [[ -n "$apk" ]]; then
  echo "APK: $apk ($(ls -lh "$apk" | awk '{print $5}'))"
else
  echo "No APK produced - see $LOG"
  exit 1
fi
