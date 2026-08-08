# CompileCheck — license-free C# verification

Building this project in Unity requires an activated Unity licence. Until one is
configured, `Assets/_Project/Scripts` cannot be compiled by CI at all — which is how
this codebase reached ~2,000 lines carrying **44 compile errors that nobody had ever
seen**, including a missing package, a missing `Powertrain` method, and half a dozen
`[SerializeField]` fields that were referenced but never declared.

Roslyn does not need a Unity licence. This harness compiles every gameplay assembly
against real Unity reference assemblies and gives the project a green gate that is
independent of licensing.

```bash
Tools/CompileCheck/compile-check.sh          # compile the game assemblies
Tools/CompileCheck/compile-check.sh --tests  # also compile the test assemblies
```

Requires the .NET SDK (`sudo apt-get install -y dotnet-sdk-8.0`) and network access to
`api.nuget.org`. References are cached in `.work/` after the first run.

## How it works

1. Pulls reference assemblies from NuGet: `UnityEngine.Modules` (the Unity engine API),
   `NUnit` (the real assertion library), and the .NET Framework reference assemblies
   (Unity's NuGet DLLs are net45-flavoured and bind against `mscorlib`).
2. Compiles `Stubs/` — the packages that are not publicly downloadable, because
   `packages.unity.com` is not reachable offline: Input System, URP / core render
   pipeline, uGUI, and the Test Framework attributes.
3. Compiles each assembly in the order the `.asmdef` files declare, so an assembly that
   reaches across a boundary it has not declared fails here exactly as it would in Unity.
4. Repeats the whole graph **twice** — once as the Android player, once with
   `UNITY_EDITOR` — because this codebase compiles genuinely different branches in each
   (native JNI haptics vs. the managed fallback, editor-only quit handling).

## What this proves, and what it does not

**It does prove:** every type, member, signature, overload, namespace and assembly
reference in the compiled assemblies resolves; both the player and editor branches are
valid; the asmdef dependency graph is honest.

**It does not prove:**

- **That the game runs.** Nothing is executed. No scene loads, no prefab is checked, no
  serialised field is bound. A `NullReferenceException` on frame one would pass here.
- **That the APK builds.** IL2CPP, the Android toolchain, shader compilation, asset
  import and Play Store packaging are all untouched. Only a real Unity build proves that.
- **The `HighwayRenegade.Editor` assembly.** `BuildScript.cs` and `TestTrackGenerator.cs`
  depend broadly on `UnityEditor`, which is not available offline; modelling that API
  well enough to be meaningful is not worth the risk of a wrong stub validating broken
  code. **These two files remain unverified.**
- **Anything covered by `reference-gaps.txt`.** See below.

## Fidelity of the stubs

The stubs are the weak point of this design: a stub written to match what the code
*calls*, rather than what the package *declares*, would silently validate broken code.
Every signature in `Stubs/` was therefore transcribed from Unity's published source
(`Unity-Technologies/Graphics`, `Unity-Technologies/UnityCsReference`) rather than
inferred from call sites.

This is not academic — it is how the harness caught `PostProcessingSetup` calling
`VolumeProfile.TryAdd`, a method that does not exist in any version of URP. A
convenience stub would have hidden it.

## `reference-gaps.txt`

Unity publishes no reference assemblies for 6000.x anywhere publicly reachable, so the
harness compiles against 2021.3.33. A few APIs the project legitimately uses postdate
that release. Those lines are listed in `reference-gaps.txt` and excluded from failure.

**Each entry is one line of code that is not machine-verified**, so each carries a
written justification. Keep the list as short as possible: the default response to a
compile error is to fix the code, not to add an entry. Where a missing symbol is a core
Unity type rather than a version difference (for example `UnityEngine.Handheld`), prefer
adding it to `Stubs/UnityGaps.cs` with its real signature — that keeps it checked.
