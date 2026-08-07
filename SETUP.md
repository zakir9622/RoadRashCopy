# Setup — Getting to a Running APK

Steps marked 🔴 **require you** (elevation, credentials, or physical hardware — things no
automation can do). Everything else is already done or automated.

---

## 1. 🔴 Install Unity Hub

The installer is already downloaded and signature-verified at
`D:\Unity\Downloads\UnityHubSetup.exe` (signed by *Unity Technologies SF*).

Unity Hub's installer declares `RequestExecutionLevel admin`, so Windows forces a UAC
prompt. UAC runs on the **secure desktop**, which no automation tool can interact with —
this is a Windows security guarantee, not a tooling gap.

In an **Administrator PowerShell** (right-click Start → *Terminal (Admin)*):

```bash
Start-Process -Wait -FilePath "D:\Unity\Downloads\UnityHubSetup.exe" -ArgumentList '/S','/D=D:\UnityHub'
```

Silent — no wizard. Installs to `D:\UnityHub`.

---

## 2. 🔴 Sign in and activate a licence

Open `D:\UnityHub\Unity Hub.exe` and sign in with your Unity account.
A free **Personal** licence is sufficient.

*Why you:* entering account passwords is something I will never do on your behalf.

---

## 3. Install the Editor (I can automate this once Hub exists)

Editors must go on **D:** — C: only has ~18 GB free and this needs ~15 GB.

In Hub: *Settings → Installs → Editor install location* → `D:\Unity\Editors`.

Then install the newest **Unity 6 LTS (6000.0.x)** with these modules:

| Module | Why |
|---|---|
| Android Build Support | required |
| ├ OpenJDK | Gradle needs a JDK |
| └ Android SDK & NDK Tools | compiles the native ARM64 binary |
| Windows IL2CPP Build Support | IL2CPP scripting backend |

Or, once Hub is installed, tell me and I'll run the CLI equivalent:

```bash
& "D:\UnityHub\Unity Hub.exe" -- --headless install --version <VERSION> --module android-sdk-ndk-tools openjdk --childModules
```

> **Note:** [`ProjectSettings/ProjectVersion.txt`](ProjectSettings/ProjectVersion.txt) currently
> says `6000.0.32f1`. Tell me the exact version Hub installs and I'll match it — a mismatch
> makes Unity offer to upgrade the project on open.

---

## 4. Open the project

Hub → *Add* → `D:\RoadRashCopy`.

First open takes several minutes: Unity resolves packages, imports assets, and generates
`Library/`, `.sln`, and `.csproj` (all correctly gitignored).

**Verify:** *Project Settings → Player → Android*
- Graphics APIs: **Vulkan only** (no GLES3)
- Minimum API Level **30**, Target API Level **35**
- Scripting Backend **IL2CPP**, Target Architecture **ARM64**

These are enforced in code by [`BuildScript.cs`](Assets/_Project/Scripts/Editor/BuildScript.cs),
so a build corrects them even if the UI drifts.

---

## 5. Build an APK locally

```bash
& "D:\Unity\Editors\<VERSION>\Editor\Unity.exe" -quit -batchmode -nographics -projectPath "D:\RoadRashCopy" -executeMethod HighwayRenegade.Editor.BuildScript.BuildAndroid -buildApk -logFile -
```

Output: `D:\RoadRashCopy\build\Android\HighwayRenegade.apk`

---

## 6. 🔴 Install on your device

Enable *Developer Options → USB Debugging*, connect, then:

```bash
adb install -r "D:\RoadRashCopy\build\Android\HighwayRenegade.apk"
```

*Why you:* physical-device behaviour — real frame rate, thermal throttling, touch latency —
cannot be validated from an emulator or from here. Your QA doc mandates exactly this.

---

## 7. CI secrets (optional, for cloud builds)

[`.github/workflows/build-android.yml`](.github/workflows/build-android.yml) builds on every
push. It needs these repo secrets (*Settings → Secrets and variables → Actions*):

| Secret | Purpose |
|---|---|
| `UNITY_EMAIL` | Unity account email |
| `UNITY_PASSWORD` | Unity account password |
| `UNITY_LICENSE` | contents of your `.ulf` licence file |

Until they're set, CI will fail at the licence step — that's expected, not a broken pipeline.

### Play Store signing

A debug-signed build **cannot** be published. Generate a release keystore (keep it safe
forever — losing it means you can never update the app):

```bash
keytool -genkeypair -v -keystore release.keystore -alias highwayrenegade -keyalg RSA -keysize 2048 -validity 10000
```

Then add `ANDROID_KEYSTORE_BASE64` (base64 of the file), `ANDROID_KEYSTORE_PASS`,
`ANDROID_KEYALIAS_NAME`, `ANDROID_KEYALIAS_PASS` as secrets.

**Never commit the keystore.**

---

## Current status

| Step | State |
|---|---|
| Git repo + GitHub remote | ✅ |
| Unity project scaffold, asmdefs, build script | ✅ |
| CI workflow | ✅ (needs secrets) |
| Unity Hub downloaded & verified | ✅ |
| Unity Hub **installed** | 🔴 step 1 |
| Editor installed | ⬜ after step 1 |
| Playable scene | ⬜ blocked on Editor |
