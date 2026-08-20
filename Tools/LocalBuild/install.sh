#!/usr/bin/env bash
# Installs Unity 6000.0.38f1 + Android toolchain from the files in dl/.
# Layout follows the destinations in Unity's own release manifest.
set -euo pipefail
cd "$(dirname "$0")"

UNITY="/home/ubuntu/unity/6000.0.38f1"
AP="$UNITY/Editor/Data/PlaybackEngines/AndroidPlayer"
SDK="$AP/SDK"

log() { printf '\033[1;36m==>\033[0m %s\n' "$*"; }

# Move the contents of an archive's single top-level dir into dest (or the
# whole extraction if there is no single top dir).
place() {
  local extracted="$1" dest="$2"
  mkdir -p "$dest"
  local entries
  entries=$(ls -A "$extracted")
  if [[ $(echo "$entries" | wc -l) -eq 1 && -d "$extracted/$entries" ]]; then
    mv "$extracted/$entries"/* "$extracted/$entries"/.[!.]* "$dest"/ 2>/dev/null || true
    mv "$extracted/$entries"/* "$dest"/ 2>/dev/null || true
  else
    mv "$extracted"/* "$dest"/ 2>/dev/null || true
  fi
  rm -rf "$extracted"
}

if [[ ! -x "$UNITY/Editor/Unity" ]]; then
  log "Extracting editor (4.5 GB, takes a few minutes)"
  mkdir -p "$UNITY"
  tar -xJf dl/editor.tar.xz -C "$UNITY"
fi
log "Editor at $UNITY/Editor/Unity"

if [[ ! -d "$AP/Variations" ]]; then
  log "Extracting Android support module from the .pkg"
  rm -rf pkg-tmp payload-tmp
  mkdir -p pkg-tmp payload-tmp
  7z x -y -opkg-tmp dl/android-support.pkg > /dev/null
  payload=$(find pkg-tmp -name 'Payload*' | head -1)
  [[ -z "$payload" ]] && { echo "no Payload in pkg"; exit 1; }
  # Payload is a (possibly gzipped) cpio archive.
  ( cd payload-tmp && 7z x -y -si -tcpio > /dev/null < <(7z x -y -so "$OLDPWD/$payload" 2>/dev/null || cat "$OLDPWD/$payload") )
  src=$(find payload-tmp -type d -name AndroidPlayer | head -1)
  [[ -z "$src" ]] && { echo "AndroidPlayer dir not found in payload"; find payload-tmp -maxdepth 3 -type d; exit 1; }
  mkdir -p "$AP"
  cp -a "$src"/. "$AP"/
  rm -rf pkg-tmp payload-tmp
fi
log "Android support module in place"

if [[ ! -x "$AP/OpenJDK/bin/java" ]]; then
  log "Installing OpenJDK"
  rm -rf jdk-tmp && mkdir jdk-tmp && unzip -q dl/jdk.zip -d jdk-tmp
  place jdk-tmp "$AP/OpenJDK"
fi

if [[ ! -d "$AP/NDK/toolchains" ]]; then
  log "Installing NDK r27c"
  rm -rf ndk-tmp && mkdir ndk-tmp && unzip -q dl/ndk.zip -d ndk-tmp
  place ndk-tmp "$AP/NDK"
fi

if [[ ! -d "$SDK/platform-tools" ]]; then
  log "Installing SDK pieces"
  mkdir -p "$SDK"

  rm -rf t && mkdir t && unzip -q dl/platform-tools.zip -d t && mv t/platform-tools "$SDK/" && rm -rf t

  rm -rf t && mkdir t && unzip -q dl/build-tools.zip -d t
  mkdir -p "$SDK/build-tools"
  top=$(ls t | head -1) && mv "t/$top" "$SDK/build-tools/34.0.0" && rm -rf t

  mkdir -p "$SDK/platforms"
  for p in 33 34 35; do
    rm -rf t && mkdir t && unzip -q "dl/platform-$p.zip" -d t
    top=$(ls t | head -1) && mv "t/$top" "$SDK/platforms/android-$p" && rm -rf t
  done

  rm -rf t && mkdir t && unzip -q dl/cmdline-tools.zip -d t
  mkdir -p "$SDK/cmdline-tools"
  mv t/cmdline-tools "$SDK/cmdline-tools/6.0" && rm -rf t
  ln -sfn "$SDK/cmdline-tools/6.0" "$SDK/cmdline-tools/latest"

  rm -rf t && mkdir t && unzip -q dl/cmake.zip -d t
  mkdir -p "$SDK/cmake"
  top=$(ls t | head -1) && mv "t/$top" "$SDK/cmake/3.22.1" && rm -rf t

  # sdk-tools (legacy 'tools/') is optional for Unity's gradle build; skip.

  # Pre-accept SDK licences so nothing ever prompts.
  mkdir -p "$SDK/licenses"
  printf '\n24333f8a63b6825ea9c5514f83c2829b004d1fee\nd56f5187479451eabf01fb78af6dfcb131a6481e\n' > "$SDK/licenses/android-sdk-license"
  printf '\n84831b9409646a918e30573bab4c9c91346d8abd\n' > "$SDK/licenses/android-sdk-preview-license"
fi

log "Done. Verify:"
"$UNITY/Editor/Unity" -version 2>/dev/null || true
ls "$AP" | head
df -h / | tail -1
