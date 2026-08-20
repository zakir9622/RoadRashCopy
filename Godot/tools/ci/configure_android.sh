#!/usr/bin/env bash
# Point Godot's editor settings at the runner's JDK, Android SDK, and a
# generated debug keystore so --export-debug Android works headless.
set -euo pipefail

JAVA_HOME="${JAVA_HOME:-$(dirname "$(dirname "$(readlink -f "$(command -v java)")")")}"
ANDROID_SDK="${ANDROID_SDK_ROOT:-${ANDROID_HOME:-${HOME}/android-sdk}}"
KEYSTORE="${HOME}/debug.keystore"
SETTINGS_DIR="${HOME}/.config/godot"

if [[ ! -f "${KEYSTORE}" ]]; then
  keytool -genkeypair -keystore "${KEYSTORE}" -storepass android -keypass android \
    -alias androiddebugkey -keyalg RSA -keysize 2048 -validity 10000 \
    -dname "CN=Android Debug,O=Android,C=US"
fi

mkdir -p "${SETTINGS_DIR}"
# Godot 4.3+ uses editor_settings-MAJOR.MINOR.tres; keep the 4.x name too.
SETTINGS_BODY=$(cat <<EOF
[gd_resource type="EditorSettings" format=3]

[resource]
export/android/java_sdk_path = "${JAVA_HOME}"
export/android/android_sdk_path = "${ANDROID_SDK}"
export/android/debug_keystore = "${KEYSTORE}"
export/android/debug_keystore_user = "androiddebugkey"
export/android/debug_keystore_pass = "android"
EOF
)
printf '%s\n' "${SETTINGS_BODY}" > "${SETTINGS_DIR}/editor_settings-4.7.tres"
printf '%s\n' "${SETTINGS_BODY}" > "${SETTINGS_DIR}/editor_settings-4.tres"

echo "Android export configured:"
echo "  JAVA_HOME=${JAVA_HOME}"
echo "  ANDROID_SDK=${ANDROID_SDK}"
echo "  KEYSTORE=${KEYSTORE}"
