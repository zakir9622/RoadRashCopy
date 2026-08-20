#!/usr/bin/env bash
# Download official Godot 4.x (and optionally Android export templates) into a
# cacheable directory. Idempotent. Used by GitHub Actions and local CI.
set -euo pipefail

VERSION="${GODOT_VERSION:-4.7.2}"
CACHE="${GODOT_CACHE:-${HOME}/.cache/godot-ci}"
BIN_NAME="Godot_v${VERSION}-stable_linux.x86_64"
ZIP_URL="https://github.com/godotengine/godot/releases/download/${VERSION}-stable/${BIN_NAME}.zip"
TPZ_URL="https://github.com/godotengine/godot/releases/download/${VERSION}-stable/Godot_v${VERSION}-stable_export_templates.tpz"
TDIR="${HOME}/.local/share/godot/export_templates/${VERSION}.stable"

curl_auth() {
  if [[ -n "${GITHUB_TOKEN:-}" ]]; then
    curl -fsSL -H "Authorization: Bearer ${GITHUB_TOKEN}" "$@"
  else
    curl -fsSL "$@"
  fi
}

mkdir -p "${CACHE}" "${HOME}/.local/bin"
if [[ ! -x "${CACHE}/${BIN_NAME}" ]]; then
  echo "Downloading Godot ${VERSION}..."
  curl_auth -o "${CACHE}/godot.zip" "${ZIP_URL}"
  unzip -o "${CACHE}/godot.zip" -d "${CACHE}"
  chmod +x "${CACHE}/${BIN_NAME}"
  rm -f "${CACHE}/godot.zip"
fi
ln -sfn "${CACHE}/${BIN_NAME}" "${HOME}/.local/bin/godot"
if [[ -n "${GITHUB_PATH:-}" ]]; then
  echo "${HOME}/.local/bin" >> "${GITHUB_PATH}"
fi
export PATH="${HOME}/.local/bin:${PATH}"
godot --version

if [[ "${INSTALL_TEMPLATES:-}" == "1" ]]; then
  mkdir -p "${TDIR}"
  if [[ ! -f "${TDIR}/android_debug.apk" && ! -f "${TDIR}/android_source.zip" ]]; then
    echo "Downloading export templates ${VERSION}..."
    curl_auth -o "${CACHE}/templates.tpz" "${TPZ_URL}"
    python3 - "${CACHE}/templates.tpz" "${TDIR}" <<'PY'
import shutil, sys, tempfile, zipfile
from pathlib import Path
tpz, dest = Path(sys.argv[1]), Path(sys.argv[2])
dest.mkdir(parents=True, exist_ok=True)
with tempfile.TemporaryDirectory() as tmp:
    with zipfile.ZipFile(tpz) as z:
        z.extractall(tmp)
    src = Path(tmp) / "templates"
    if not src.is_dir():
        src = Path(tmp)
    for item in src.iterdir():
        target = dest / item.name
        if item.is_dir():
            shutil.copytree(item, target, dirs_exist_ok=True)
        else:
            shutil.copy2(item, target)
PY
    echo "${VERSION}.stable" > "${TDIR}/version.txt"
    rm -f "${CACHE}/templates.tpz"
  fi
fi
