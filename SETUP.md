# Setup Status

## Done

| Step | State |
|---|---|
| Git repo + GitHub remote + LFS | ✅ |
| Unity Hub | ✅ `D:\UnityHub\app` (extracted, no admin needed) |
| Unity **6000.0.81f1** LTS editor | ✅ `D:\Unity\Editors\6000.0.81f1` |
| Unity licence | ✅ Personal, activated |
| Package resolution | ✅ all pinned versions resolved |
| **C# compilation** | ✅ 5 assemblies, 0 errors |
| **EditMode tests** | ✅ **23 / 23 passing** |
| Test track scene | ✅ generated, 115 objects, in Build Settings |
| Android Build Support module | ⛔ **blocked — see below** |
| APK build | ⏳ blocked on the module |

---

## 🔴 The one remaining step

Building an APK needs Unity's **Android Build Support** module, which is not installed.

Every automated attempt failed with the same error:

```
[Android Build Support] failed to install.
Error given: The Windows elevation prompt was cancelled or timed out.
```

Unity's installers declare `requestedExecutionLevel = highestAvailable`, so on an
administrator account Windows always raises a UAC prompt. UAC runs on the **secure
desktop**, which no automation can interact with — a deliberate Windows security
guarantee, not a tooling gap.

Extraction (which worked for Unity Hub) does not work here: the module installer is a
newer NSIS variant that 7-Zip cannot unpack.

### Fix it in Unity Hub

1. Open `D:\UnityHub\app\Unity Hub.exe`
2. **Installs** tab → find **6000.0.81f1** → gear icon → **Add modules**
3. Tick:
   - ✅ **Android Build Support**
     - ✅ OpenJDK
     - ✅ Android SDK & NDK Tools
4. **Click Yes on the UAC prompt**

Most components are already downloaded and cached in `D:\Unity\HubDownloads`, so this
is mostly extraction.

### Then build

```bash
powershell -ExecutionPolicy Bypass -File D:\RoadRashCopy\Tools\unity.ps1 -Mode build -Apk
```

Output: `D:\RoadRashCopy\build\Android\HighwayRenegade.apk`

---

## Everyday commands

```bash
powershell -ExecutionPolicy Bypass -File D:\RoadRashCopy\Tools\unity.ps1 -Mode compile
```

```bash
powershell -ExecutionPolicy Bypass -File D:\RoadRashCopy\Tools\unity.ps1 -Mode test
```

Regenerate the test track:

```bash
& "D:\Unity\Editors\6000.0.81f1\Editor\Unity.exe" -quit -batchmode -nographics -projectPath D:\RoadRashCopy -executeMethod HighwayRenegade.Editor.TestTrackGenerator.Generate -logFile -
```

---

## Installing on your phone

Enable *Developer Options → USB Debugging*, connect by cable, then:

```bash
adb install -r D:\RoadRashCopy\build\Android\HighwayRenegade.apk
```

`adb` lives at `D:\Unity\Editors\6000.0.81f1\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\adb.exe`
once the Android module is installed.

---

## Controls

| Input | Action |
|---|---|
| Touch — left half | steer (drag from where you touch) |
| Touch — right upper | throttle |
| Touch — right lower | brake |
| Touch — both pedals | handbrake / power slide |
| Gamepad | RT throttle, LT brake, left stick steer, A handbrake |
| Keyboard | W/S throttle+brake, A/D steer, Space handbrake |

---

## Play Store (later)

A debug-signed build cannot be published. Generate a release keystore and **never lose it** —
losing it means you can never update the app:

```bash
keytool -genkeypair -v -keystore release.keystore -alias highwayrenegade -keyalg RSA -keysize 2048 -validity 10000
```

Then add `ANDROID_KEYSTORE_BASE64`, `ANDROID_KEYSTORE_PASS`, `ANDROID_KEYALIAS_NAME`,
`ANDROID_KEYALIAS_PASS` as GitHub Actions secrets (plus `UNITY_EMAIL`, `UNITY_PASSWORD`,
`UNITY_LICENSE` for CI). **Never commit the keystore.**

> Note: personal Play developer accounts created after Nov 2023 must run a **closed test
> with 12 testers for 14 days** before production release. That is calendar time, not
> engineering time — start it early.
