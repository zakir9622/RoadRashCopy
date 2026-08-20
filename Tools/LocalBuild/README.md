# Local Android build (no GitHub Actions)

Builds the release APK on any Linux x86_64 machine with ~15 GB free disk,
without CI and without Unity Hub.

```bash
Tools/LocalBuild/download.sh   # ~6.5 GB: editor, Android module, JDK, SDK, NDK
Tools/LocalBuild/install.sh    # extracts everything to ~/unity/6000.0.38f1
Tools/LocalBuild/build-apk.sh  # activates the licence and builds the APK
```

`build-apk.sh` needs a licence in the environment:

* `UNITY_LICENSE` — the full contents of `Unity_lic.ulf` (Personal), **or**
* `UNITY_SERIAL` + `UNITY_EMAIL` + `UNITY_PASSWORD` (Plus/Pro serial).

Where `Unity_lic.ulf` lives after activating Unity once on your own machine:

| OS      | Path |
|---------|------|
| Windows | `C:\ProgramData\Unity\Unity_lic.ulf` |
| macOS   | `/Library/Application Support/Unity/Unity_lic.ulf` |
| Linux   | `~/.local/share/unity3d/Unity/Unity_lic.ulf` |

The APK lands in `build/Android/`.
