# Setup & Build

> An earlier version of this file described a local Windows install (`D:\Unity\...`,
> Unity 6000.0.81f1, "23/23 tests", an Android-module UAC blocker). That was one
> contributor's machine and is not how the project builds. The project builds in CI on
> game-ci, and that is the source of truth.

## How it actually builds

The APK/AAB is produced by GitHub Actions (`.github/workflows/build-android.yml`) using the
game-ci Docker images — no local Unity install is required to ship. The pipeline:

1. **Check Unity credentials** — fails fast if the licence secrets are missing.
2. **EditMode Tests** (blocking) — a dedicated `testMode: EditMode` invocation. Compiles
   every assembly (including the Editor assembly, so `BuildScript`'s real Unity API usage is
   validated) and runs the 240+ tests. **The build gates on this.**
3. **PlayMode Tests** (advisory) — a separate `testMode: PlayMode` invocation. Kept
   non-blocking because play-mode entry hangs upstream (game-ci/unity-test-runner#188); it
   does not block the build.
4. **Build** — regenerates the scenes from code, applies Android settings via `BuildScript`,
   and produces the APK (or AAB).

Unity version: **6000.0.38f1**. URP 17, Vulkan-only, ARM64, IL2CPP, ASTC, Linear colour
space, Android minSdk 30 / targetSdk 35.

## Required CI secrets

Set these as GitHub Actions secrets:

- `UNITY_EMAIL`, `UNITY_PASSWORD`, `UNITY_LICENSE` — Unity Personal licence for CI.
- For a **signed release** (AAB): `ANDROID_KEYSTORE_BASE64`, `ANDROID_KEYSTORE_PASS`,
  `ANDROID_KEYALIAS_NAME`, `ANDROID_KEYALIAS_PASS`. A release build **fails** rather than
  silently debug-signing when these are absent.

Generate the keystore once and **never lose it** — losing it means you can never update the
published app:

```bash
keytool -genkeypair -v -keystore release.keystore -alias highwayrenegade \
  -keyalg RSA -keysize 2048 -validity 10000
```

**Never commit the keystore.**

## Producing a build

- **APK** (sideload / testing): the default `push`-to-`main` build, or dispatch the workflow
  with `build_format: apk`.
- **AAB** (Play Store): dispatch the workflow with `build_format: aab`. Requires the keystore
  secrets above.

`versionCode` is derived from the workflow run number, so every CI build is monotonically
newer — Play rejects a re-used `versionCode`.

## Static checks (run locally, no Unity needed)

The `Tools/CompileCheck` harness runs seven licence-free Python checkers plus a Roslyn
compile against hand-written Unity stubs. The checkers catch the defect classes that shipped
before (release-readiness regressions, debug instrumentation left in runtime code, broken
asset/meta references, unreachable components):

```bash
bash Tools/CompileCheck/compile-check.sh .
```

(The Roslyn step needs the .NET SDK; the seven Python checkers run standalone with
`python3 Tools/CompileCheck/check-*.py .`.)

## Installing on a device

Enable *Developer Options → USB Debugging*, connect by cable, download the APK artifact from
the workflow run, then:

```bash
adb install -r HighwayRenegade.apk
```

## Controls

| Input | Action |
|---|---|
| Touch — left half | steer (drag from where you touch) |
| Touch — right upper | throttle |
| Touch — right lower | brake |
| Touch — both pedals | handbrake / power slide |
| Gamepad | RT throttle, LT brake, left stick steer, A handbrake |
| Keyboard | W/S throttle+brake, A/D steer, Space handbrake, Esc pause/back |

## Play Store note

A personal Play developer account created after Nov 2023 must run a **closed test with 12
testers for 14 days** before production release. That is calendar time, not engineering
time — start it early.
