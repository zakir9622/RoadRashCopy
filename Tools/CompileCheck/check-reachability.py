#!/usr/bin/env python3
"""Fails when a component exists but nothing can ever reach it.

This project's most expensive defects were never compile errors. Twelve systems -
the police AI, the pause menu, the results screen, the HUD, the ragdoll, the weapon
pickup, the haptics layer - were fully written, compiled cleanly, and were referenced
by no other script, no scene and no scene generator. They could not run. Nothing said
so, because unreachable code is indistinguishable from working code until someone
looks for the feature and finds it missing.

A MonoBehaviour reaches the game one of three ways:
  * another script names it (AddComponent, GetComponent, a field, FindObjectOfType)
  * a scene instantiates it, which shows up as its .meta GUID in the .unity YAML
  * a scene generator creates it, which is the first case

so a type matching none of the three is dead by construction, and this says so.

Also checks that the input backend matches what the code actually calls - a project
configured for the old Input Manager while its scripts read Keyboard.current gets
null devices at runtime and no input at all, with nothing logged.

Usage:  python3 Tools/CompileCheck/check-reachability.py [project-root]
"""

import re
import sys
import pathlib

# Types that are entry points: nothing in the project references them because
# something outside it does. Each needs a reason, not just a name.
ALLOWED_UNREFERENCED = {
    "BuildScript": "CI entry point, invoked by -executeMethod from the workflow",
    "TestTrackGenerator": "editor menu item and BuildScript fallback",
    "MenuSceneGenerator": "editor menu item and BuildScript fallback",
    "UiSceneBuilder": "used by the generators above",
    "SceneNames": "constants",
}


def strip_code(src: str) -> str:
    """Remove comments and string literals so a name in prose is not a reference.

    Interpolated strings are left alone. A $"..." literal contains real executable code
    inside its braces - $"{DescribeDirectory(path)}" is a genuine call - so blanking it
    loses references and reports live methods as dead. Erring toward keeping text means
    a name mentioned inside an interpolated literal counts as a reference, which is the
    safe direction for a check that fails builds.
    """
    src = re.sub(r"/\*.*?\*/", "", src, flags=re.S)
    src = re.sub(r"//.*", "", src)
    src = re.sub(r'(?<![$@])"(?:[^"\\\n]|\\.)*"', '""', src)
    return src


def main() -> int:
    root = pathlib.Path(sys.argv[1] if len(sys.argv) > 1 else ".")
    scripts = sorted(root.glob("Assets/_Project/Scripts/**/*.cs"))
    tests = sorted(root.glob("Assets/_Project/Tests/**/*.cs"))

    if not scripts:
        print("check-reachability: no scripts found", file=sys.stderr)
        return 2

    scene_text = "\n".join(
        p.read_text(errors="ignore") for p in root.glob("Assets/**/*.unity")
    )

    # Declared MonoBehaviours, and the stripped body of every file.
    bodies = {p: strip_code(p.read_text(errors="ignore")) for p in scripts}
    test_bodies = [strip_code(p.read_text(errors="ignore")) for p in tests]

    components = {}
    for path, body in bodies.items():
        for match in re.finditer(
            r"\b(?:public|internal)\s+(?:sealed\s+|abstract\s+|partial\s+)*"
            r"class\s+(\w+)\s*:\s*([^{]+)",
            body,
        ):
            name, bases = match.group(1), match.group(2)
            if "MonoBehaviour" in bases or "UIScreen" in bases:
                # An abstract base is reached through its subclasses.
                if re.search(r"\babstract\s+(?:sealed\s+)?class\s+%s\b" % re.escape(name), body):
                    continue
                components[name] = path

    failures = []
    for name, path in sorted(components.items()):
        if name in ALLOWED_UNREFERENCED:
            continue

        pattern = re.compile(r"\b%s\b" % re.escape(name))
        referenced = any(
            pattern.search(body) for other, body in bodies.items() if other != path
        )
        if referenced:
            continue

        # A scene can instantiate it directly; scenes name scripts by .meta GUID.
        meta = pathlib.Path(str(path) + ".meta")
        if meta.exists() and scene_text:
            guid = re.search(r"^guid:\s*(\w+)", meta.read_text(), re.M)
            if guid and guid.group(1) in scene_text:
                continue

        tested = any(pattern.search(body) for body in test_bodies)
        rel = str(path).replace(str(root) + "/", "")
        failures.append(
            f"{rel}: {name} is referenced by no script, no scene and no generator"
            + (" (tests reference it, but tests do not ship)" if tested else "")
        )

    failures.extend(find_uncalled_private_methods(root, bodies))

    input_problem = check_input_backend(root, bodies)
    if input_problem:
        failures.append(input_problem)

    if failures:
        print()
        for line in failures:
            print(line)
        print(f"\n{len(failures)} unreachable component(s) or configuration problem(s).")
        return 1

    print(f"    ok  reachability ({len(components)} components, all reachable)")
    return 0


# Unity calls these itself; nothing in the project references them by name.
UNITY_MESSAGES = {
    "Awake", "Start", "Update", "FixedUpdate", "LateUpdate", "OnEnable", "OnDisable",
    "OnDestroy", "OnGUI", "OnValidate", "Reset", "OnApplicationPause", "OnApplicationQuit",
    "OnApplicationFocus", "OnCollisionEnter", "OnCollisionStay", "OnCollisionExit",
    "OnTriggerEnter", "OnTriggerStay", "OnTriggerExit", "OnDrawGizmos",
    "OnDrawGizmosSelected", "OnBecameVisible", "OnBecameInvisible", "OnParticleCollision",
    "OnAudioFilterRead", "OnPreRender", "OnPostRender", "OnRenderObject", "OnWillRenderObject",
}


def find_uncalled_private_methods(root: pathlib.Path, bodies):
    """Private methods nothing calls.

    Wiring a component up inside a helper that is itself never invoked leaves the type
    "referenced" while still being unreachable, so the check above passes and the system
    is just as dead. csc does not warn about this - unused private methods are an IDE
    analyser rule, not a compiler one - so it needs saying here.

    Deliberately conservative: only private methods, only when the name appears exactly
    once in its own file (the declaration). Anything reached by reflection, by a Unity
    message, or from another file is left alone.
    """
    problems = []
    for path, body in bodies.items():
        for match in re.finditer(
            r"\bprivate\s+(?:static\s+)?(?:async\s+)?[\w<>\[\],\.\?]+\s+(\w+)\s*\(",
            body,
        ):
            name = match.group(1)
            if name in UNITY_MESSAGES:
                continue

            # A local function or a constructor-like name is out of scope.
            if re.search(r"\bclass\s+%s\b" % re.escape(name), body):
                continue

            # Count every mention of the bare name, not just call syntax. A handler
            # passed as a method group - OnClick("BtnBack", Back), crash.Crashed +=
            # OnCrashed - is a genuine use with no parentheses anywhere, and counting
            # only "name(" reports every event handler in the project as dead.
            occurrences = len(re.findall(r"\b%s\b" % re.escape(name), body))
            if occurrences > 1:
                continue

            # Might be referenced from elsewhere despite being private in a partial class.
            if any(re.search(r"\b%s\b" % re.escape(name), other)
                   for p, other in bodies.items() if p != path):
                continue

            rel = str(path).replace(str(root) + "/", "")
            problems.append(
                f"{rel}: private method {name}() is never called, so anything it wires up "
                "is unreachable"
            )
    return problems


def check_input_backend(root: pathlib.Path, bodies) -> str:
    """The new Input System's devices are null unless the backend is enabled for it.

    activeInputHandler: 0 = old Input Manager, 1 = new Input System, 2 = both. A
    project on 0 whose scripts call Keyboard.current or Touchscreen.current compiles
    perfectly and then receives no input whatsoever, silently.
    """
    settings = root / "ProjectSettings" / "ProjectSettings.asset"
    if not settings.exists():
        return ""

    match = re.search(r"^\s*activeInputHandler:\s*(\d+)", settings.read_text(errors="ignore"), re.M)
    if not match:
        return ""

    handler = int(match.group(1))
    uses_new = any(re.search(r"\bUnityEngine\.InputSystem\b", b) for b in bodies.values())
    uses_old = any(re.search(r"\bInput\.(gyro|GetKey|GetAxis|GetButton|touches)", b) for b in bodies.values())

    if uses_new and handler == 0:
        return ("ProjectSettings.asset: scripts use UnityEngine.InputSystem but "
                "activeInputHandler is 0 (old Input Manager only), so Keyboard.current, "
                "Gamepad.current and Touchscreen.current are all null at runtime")
    if uses_old and handler == 1:
        return ("ProjectSettings.asset: scripts use legacy Input but activeInputHandler "
                "is 1 (new Input System only), so those calls throw at runtime")
    return ""


if __name__ == "__main__":
    sys.exit(main())
