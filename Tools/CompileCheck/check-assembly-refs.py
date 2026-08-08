#!/usr/bin/env python3
"""
Validates that each assembly declares the references its source actually needs, and
that every built-in Unity module a type comes from is listed in Packages/manifest.json.

Why this exists: the Roslyn harness next door compiles against every Unity module DLL at
once, with a single stubs assembly referenced by everything. That is what makes it able
to check the code at all offline - and it is exactly why it cannot see this class of
error. Unity resolves types per assembly, through .asmdef references and the module list
in the manifest; the harness resolves them globally. Code that compiles cleanly here can
still fail in Unity with:

    error CS0234: The type or namespace name 'Universal' does not exist in
                  the namespace 'UnityEngine.Rendering'
    error CS1069: The type name 'ParticleSystem' could not be found ...
                  This type has been forwarded to assembly 'UnityEngine.ParticleSystemModule'

Both of those reached CI on the first real Unity build. This check closes that gap
statically: no Unity licence, no image pull, runs in under a second.

Exits non-zero when a requirement is unmet.
"""
import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
SCRIPTS = ROOT / "Assets" / "_Project" / "Scripts"
MANIFEST = ROOT / "Packages" / "manifest.json"

# Namespaces that live in a package assembly and need an explicit .asmdef reference.
# Unity does not resolve these transitively: referencing URP does not grant Core.
NAMESPACE_REQUIRES = {
    "UnityEngine.Rendering.Universal": {"Unity.RenderPipelines.Universal.Runtime"},
    "UnityEngine.InputSystem":         {"Unity.InputSystem"},
    "UnityEngine.UI":                  {"UnityEngine.UI"},
    "TMPro":                           {"Unity.TextMeshPro"},
    "UnityEngine.TestTools":           {"UnityEngine.TestRunner"},
    "NUnit.Framework":                 {"UnityEngine.TestRunner"},
}

# Types that live in the render-pipeline *core* assembly rather than URP. Referencing
# URP alone leaves these unresolved, which is precisely how Volume failed in CI.
CORE_RP_TYPES = {"Volume", "VolumeProfile", "VolumeComponent", "VolumeParameter"}
CORE_RP_ASSEMBLY = "Unity.RenderPipelines.Core.Runtime"

# Types gated behind a built-in module. Absent from the manifest, Unity forwards the type
# to a module that was never included and compilation fails.
TYPE_REQUIRES_MODULE = {
    "ParticleSystem":    "com.unity.modules.particlesystem",
    "TrailRenderer":     "com.unity.modules.particlesystem",
    "Terrain":           "com.unity.modules.terrain",
    "TerrainData":       "com.unity.modules.terrain",
    "TerrainCollider":   "com.unity.modules.terrainphysics",
    "WheelCollider":     "com.unity.modules.vehicles",
    "Cloth":             "com.unity.modules.cloth",
    "NavMeshAgent":      "com.unity.modules.ai",
    "VideoPlayer":       "com.unity.modules.video",
    "Animator":          "com.unity.modules.animation",
    "AudioSource":       "com.unity.modules.audio",
    "AudioClip":         "com.unity.modules.audio",
    "AndroidJavaObject": "com.unity.modules.androidjni",
    "AndroidJavaClass":  "com.unity.modules.androidjni",
    "Rigidbody":         "com.unity.modules.physics",
    "UIDocument":        "com.unity.modules.uielements",
    "VisualElement":     "com.unity.modules.uielements",
    "Canvas":            "com.unity.modules.ui",
}

COMMENT = re.compile(r"^\s*(//|/\*|\*)")


def strip_comments(source: str) -> str:
    """
    Drop comments AND string literals, so a type named only in prose or in a path is
    ignored.

    String literals matter as much as comments here. A file-path constant like
    "Assets/_Project/Art/Surfaces/Terrain" contains the word Terrain, and matching it
    reported that the file "uses Terrain, which needs com.unity.modules.terrain" - a
    module the project has no reason to ship. A guard that cries wolf about a folder name
    is a guard people start ignoring.

    Interpolated strings are deliberately kept: code inside $"...{Foo()}..." really does
    reference Foo, and blanking those would hide genuine usage.
    """
    source = re.sub(r"/\*.*?\*/", "", source, flags=re.S)
    source = re.sub(r'(?<![$@])"(?:[^"\\\n]|\\.)*"', '""', source)
    return "\n".join(l for l in source.splitlines() if not COMMENT.match(l))


def owning_asmdef(path: Path):
    """Nearest .asmdef at or above a file - the assembly Unity will compile it into."""
    for parent in [path.parent, *path.parents]:
        if parent < SCRIPTS.parent:
            break
        found = list(parent.glob("*.asmdef"))
        if found:
            return found[0]
    return None


def main() -> int:
    manifest = json.loads(MANIFEST.read_text())
    modules = set(manifest.get("dependencies", {}))

    asmdefs = {}
    for path in SCRIPTS.rglob("*.asmdef"):
        asmdefs[path] = json.loads(path.read_text())

    failures = []
    checked = 0

    for source_path in SCRIPTS.rglob("*.cs"):
        body = strip_comments(source_path.read_text(encoding="utf-8", errors="replace"))
        rel = source_path.relative_to(ROOT)
        checked += 1

        owner = owning_asmdef(source_path)
        refs = set(asmdefs.get(owner, {}).get("references", [])) if owner else set()
        owner_name = asmdefs.get(owner, {}).get("name", "<none>") if owner else "<none>"

        # Editor-only assemblies get UnityEditor for free; skip namespace rules there.
        for namespace, required in NAMESPACE_REQUIRES.items():
            if not re.search(rf"\b{re.escape(namespace)}\b", body):
                continue
            missing = required - refs
            if missing:
                failures.append(
                    f"{rel}: uses {namespace} but {owner_name} does not reference "
                    f"{', '.join(sorted(missing))}"
                )

        if any(re.search(rf"\b{t}\b", body) for t in CORE_RP_TYPES):
            if CORE_RP_ASSEMBLY not in refs:
                failures.append(
                    f"{rel}: uses a render-pipeline core type (Volume/VolumeProfile) but "
                    f"{owner_name} does not reference {CORE_RP_ASSEMBLY}"
                )

        for type_name, module in TYPE_REQUIRES_MODULE.items():
            if re.search(rf"\b{type_name}\b", body) and module not in modules:
                failures.append(
                    f"{rel}: uses {type_name}, which needs {module} in Packages/manifest.json"
                )

    if failures:
        for line in sorted(set(failures)):
            print(line, file=sys.stderr)
        print(f"\n{len(set(failures))} assembly/module reference problem(s).", file=sys.stderr)
        return 1

    print(f"    ok  assembly + module references ({checked} files, {len(asmdefs)} assemblies)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
