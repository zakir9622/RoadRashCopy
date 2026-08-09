#!/usr/bin/env python3
"""
Validates the field names the scene generators assign by string.

Why this exists: the compile harness cannot compile HighwayRenegade.Editor, because
UnityEditor has no publicly downloadable reference assembly and a stub broad enough to
cover it would risk validating broken code. That leaves the scene generators - roughly
900 lines that build the entire playable scene, the main menu and the garage -
completely unverified.

The dominant failure mode in that code is not a type error at all: the field is named by
a string. A renamed or mistyped field still compiles, resolves to null, and throws at
generation time - which is exactly when a build is trying to produce the scene.

WHY THIS CHECK WAS REWRITTEN
----------------------------
The first version collected every [SerializeField] name in the project into one flat set
and asked only "does this string exist somewhere". That is name-aware but not type-aware,
and the bug lives in exactly that gap:

    PoliceAI._target        was a plain private field - NOT serialized
    RivalAIController._target   IS [SerializeField]
    ChaseCamera._target         IS [SerializeField]

so FindProperty("_target") against a PoliceAI matched somebody else's field, passed the
check, and threw a NullReferenceException six minutes into every Android build.

This version resolves the *type* being wired and checks the field against that type and
its base classes. When the type cannot be inferred (a helper that takes a MonoBehaviour,
say) it falls back to the old global check and reports the site, so the blind spot stays
measured rather than forgotten.

Exits non-zero if any name does not resolve to a real [SerializeField] field on the type
being assigned.
"""
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
SCRIPTS = ROOT / "Assets" / "_Project" / "Scripts"
EDITOR = SCRIPTS / "Editor"

# Types too general to carry a specific field. Seeing one of these means the call site
# is generic on purpose, so fall back to the project-wide name check.
OPAQUE_TYPES = {
    "MonoBehaviour", "Component", "Behaviour", "Object", "UnityEngine.Object",
    "GameObject", "ScriptableObject", "var",
}

CLASS_RE = re.compile(
    r"^\s*(?:\[[^\]]*\]\s*)*"
    r"(?:public|internal|private|protected|sealed|abstract|static|partial|\s)*"
    r"class\s+(\w+)\s*(?::\s*([^{\n]+))?",
    re.MULTILINE,
)

SERIALIZED_RE = re.compile(
    r"\[SerializeField\][^\n]*?(?:\n\s*)?(?:public|private|protected|internal|\s)*"
    r"[\w<>\[\].,\s]*?\s(_\w+)\s*[;=]"
)

METHOD_RE = re.compile(
    r"^\s+(?:\[[^\]]*\]\s*)*"
    r"(?:public|private|protected|internal|static|sealed|override|virtual|async|new|\s)+"
    r"([\w<>\[\].]+)\s+(\w+)\s*\(([^)]*)\)\s*$"
)

# SerializedWiring.SetRef(target, "_field", ...) and friends.
WIRING_RE = re.compile(r"SerializedWiring\.Set\w+\(\s*([\w.]+)\s*,\s*\"(\w+)\"")
# Legacy: soVar.FindProperty("_field")
FIND_RE = re.compile(r"(\w+)\.FindProperty\(\"(\w+)\"\)")
SO_DECL_RE = re.compile(r"(?:var|SerializedObject)\s+(\w+)\s*=\s*new\s+SerializedObject\(\s*([\w.]+)\s*\)")


def strip_generic(type_name: str) -> str:
    return type_name.split("<")[0].split(".")[-1].strip()


def build_type_table():
    """{TypeName: (base_names, {serialized field names declared on it})}."""
    table = {}
    for path in SCRIPTS.rglob("*.cs"):
        source = path.read_text(encoding="utf-8", errors="replace")

        spans = []
        for match in CLASS_RE.finditer(source):
            bases = []
            if match.group(2):
                bases = [strip_generic(b) for b in match.group(2).split(",")]
            spans.append([match.start(), match.group(1), bases])

        for index, span in enumerate(spans):
            end = spans[index + 1][0] if index + 1 < len(spans) else len(source)
            span.append(end)

        for start, name, bases, end in spans:
            fields = set(SERIALIZED_RE.findall(source[start:end]))
            known_bases, known_fields = table.get(name, ([], set()))
            table[name] = (known_bases or bases, known_fields | fields)

    return table


def fields_of(type_name: str, table, seen=None) -> set:
    """Serialized fields on a type, including everything inherited."""
    seen = seen or set()
    if type_name in seen or type_name not in table:
        return set()
    seen.add(type_name)

    bases, fields = table[type_name]
    result = set(fields)
    for base in bases:
        result |= fields_of(base, table, seen)
    return result


def return_types(source: str) -> dict:
    """{methodName: returnType} so `var x = BuildBike(...)` can be resolved."""
    types = {}
    for line in source.splitlines():
        match = METHOD_RE.match(line)
        if match:
            types[match.group(2)] = strip_generic(match.group(1))
    return types


def locals_from(line: str, methods: dict) -> list:
    """(variable, type) pairs a single line introduces."""
    found = []

    # Any generic factory hands back its type argument: AddComponent<T>,
    # GetComponent<T>, UiSceneBuilder.AddScreen<T>, and anything shaped like them.
    for name, type_name in re.findall(
        r"(?:var\s+)?(\w+)\s*=\s*[\w.]*\.?\w+<(\w+)>\s*\(", line
    ):
        found.append((name, strip_generic(type_name)))

    # Explicit declaration: `BikeController police = ...`
    for type_name, name in re.findall(r"\b([A-Z][\w<>\[\].]*)\s+(\w+)\s*=", line):
        found.append((name, strip_generic(type_name)))

    # `var x = SomeLocalFactory(...)`
    for name, method in re.findall(r"var\s+(\w+)\s*=\s*(\w+)\s*\(", line):
        if method in methods:
            found.append((name, methods[method]))

    return found


def params_of(signature: str) -> list:
    pairs = []
    for part in signature.split(","):
        tokens = part.strip().split()
        if len(tokens) >= 2:
            pairs.append((tokens[-1], strip_generic(tokens[-2])))
    return pairs


def main() -> int:
    table = build_type_table()
    everything = set()
    for name in table:
        everything |= fields_of(name, table)

    if not everything:
        print("ERROR: found no [SerializeField] fields at all - the scan is broken.",
              file=sys.stderr)
        return 1

    failures, unresolved = [], []
    checked = 0

    for path in sorted(EDITOR.rglob("*.cs")):
        source = path.read_text(encoding="utf-8", errors="replace")
        methods = return_types(source)

        scope = {}          # variable -> type, reset at each method
        so_targets = {}     # SerializedObject variable -> underlying variable

        for line_number, line in enumerate(source.splitlines(), start=1):
            signature = METHOD_RE.match(line)
            if signature:
                scope = dict(params_of(signature.group(3)))
                so_targets = {}
                continue

            for name, type_name in locals_from(line, methods):
                scope[name] = type_name

            for so_var, target in SO_DECL_RE.findall(line):
                so_targets[so_var] = target.split(".")[0]

            sites = [(target, field) for target, field in WIRING_RE.findall(line)]
            sites += [(so_targets.get(so_var, so_var), field)
                      for so_var, field in FIND_RE.findall(line)]

            for target, field in sites:
                checked += 1
                type_name = scope.get(target.split(".")[0])
                location = f"{path.relative_to(ROOT)}:{line_number}"

                if type_name and type_name not in OPAQUE_TYPES and type_name in table:
                    if field not in fields_of(type_name, table):
                        failures.append(
                            f"{location}: {type_name} has no [SerializeField] field "
                            f"'{field}'. A private field without [SerializeField] is not "
                            f"serialized, so it cannot be wired and would be discarded "
                            f"when the scene is saved."
                        )
                    continue

                # Type not inferable - fall back to the project-wide name check.
                unresolved.append(f"{location}: '{field}' on {type_name or 'unknown type'}")
                if field not in everything:
                    failures.append(
                        f"{location}: '{field}' does not match any [SerializeField] "
                        f"field anywhere in the project"
                    )

    if failures:
        print("\n".join(failures), file=sys.stderr)
        print(f"\n{len(failures)} of {checked} serialized reference(s) unresolved.",
              file=sys.stderr)
        return 1

    print(f"    ok  serialized references ({checked} checked, "
          f"{checked - len(unresolved)} type-resolved)")
    for site in unresolved:
        print(f"        note  type not inferable, name-only check: {site}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
