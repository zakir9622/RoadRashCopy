#!/usr/bin/env python3
"""
Validates SerializedObject.FindProperty("_field") names in editor scripts.

Why this exists: the compile harness cannot compile HighwayRenegade.Editor, because
UnityEditor has no publicly downloadable reference assembly and a stub broad enough to
cover it would risk validating broken code. That leaves the scene generators - roughly
900 lines that build the entire playable scene, the main menu and the garage -
completely unverified.

The dominant failure mode in that code is not a type error at all: FindProperty takes a
string. A renamed or mistyped field name still compiles, returns null, and throws a
NullReferenceException at generation time - which is exactly when a build is trying to
produce the scene. This is a cheap static check for that one bug class.

Exits non-zero if any name does not resolve to a real [SerializeField] field.
"""
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
SCRIPTS = ROOT / "Assets" / "_Project" / "Scripts"
EDITOR = SCRIPTS / "Editor"

FIELD_PATTERNS = (
    re.compile(r"\[SerializeField\][^\n]*?\s(_\w+)\s*[;=]"),
    re.compile(r"\[SerializeField\]\s*private\s+[\w<>\[\].]+\s+(_\w+)"),
)


def serialized_fields() -> set:
    """Every [SerializeField]-backed private field name in the project."""
    names = set()
    for path in SCRIPTS.rglob("*.cs"):
        source = path.read_text(encoding="utf-8", errors="replace")
        for pattern in FIELD_PATTERNS:
            names.update(pattern.findall(source))
    return names


def main() -> int:
    known = serialized_fields()
    if not known:
        print("ERROR: found no [SerializeField] fields at all - the scan is broken.",
              file=sys.stderr)
        return 1

    failures = []
    checked = 0

    for path in sorted(EDITOR.rglob("*.cs")):
        for line_number, line in enumerate(
            path.read_text(encoding="utf-8", errors="replace").splitlines(), start=1
        ):
            for name in re.findall(r'FindProperty\("(\w+)"\)', line):
                checked += 1
                if name not in known:
                    failures.append(
                        f"{path.relative_to(ROOT)}:{line_number}: "
                        f'FindProperty("{name}") does not match any [SerializeField] field'
                    )

    if failures:
        print("\n".join(failures), file=sys.stderr)
        print(f"\n{len(failures)} of {checked} FindProperty name(s) unresolved.", file=sys.stderr)
        return 1

    print(f"    ok  serialized references ({checked} FindProperty names resolve)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
