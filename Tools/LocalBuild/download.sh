#!/usr/bin/env bash
# Downloads Unity 6000.0.38f1 + Android toolchain for a local headless build.
set -uo pipefail
cd "$(dirname "$0")"
mkdir -p dl

BASE="https://download.unity3d.com/download_unity/82314a941f2d"
GOOG="https://dl.google.com/android/repository"

fetch() {
  local url="$1" out="dl/$2"
  if [[ -s "$out" ]]; then echo "SKIP $2 (exists)"; return 0; fi
  echo "GET  $2"
  for attempt in 1 2 3; do
    curl -sSfL --retry 3 --retry-delay 5 -o "$out.part" "$url" && mv "$out.part" "$out" && return 0
    echo "retry $attempt failed for $2"; sleep $((attempt * 5))
  done
  echo "FAILED $2"; return 1
}

fail=0
fetch "$BASE/LinuxEditorInstaller/Unity-6000.0.38f1.tar.xz" editor.tar.xz || fail=1
fetch "$BASE/MacEditorTargetInstaller/UnitySetup-Android-Support-for-Editor-6000.0.38f1.pkg" android-support.pkg || fail=1
fetch "https://download.unity3d.com/download_unity/open-jdk/open-jdk-linux-x64/jdk17.0.9-9_8d1cbcce56285f3146cf7761353a643fe573b39e45bd94f35590dca39277f667.zip" jdk.zip || fail=1
fetch "$GOOG/android-ndk-r27c-linux.zip" ndk.zip || fail=1
fetch "$GOOG/cmake-3.22.1-linux.zip" cmake.zip || fail=1
fetch "$GOOG/build-tools_r34-linux.zip" build-tools.zip || fail=1
fetch "$GOOG/platform-tools_r34.0.5-linux.zip" platform-tools.zip || fail=1
fetch "$GOOG/platform-33_r02.zip" platform-33.zip || fail=1
fetch "$GOOG/platform-34-ext7_r02.zip" platform-34.zip || fail=1
fetch "$GOOG/platform-35_r01.zip" platform-35.zip || fail=1
fetch "$GOOG/commandlinetools-linux-8092744_latest.zip" cmdline-tools.zip || fail=1

echo "=== download phase done, fail=$fail ==="
ls -lh dl/
exit $fail
