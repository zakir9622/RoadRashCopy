#!/usr/bin/env python3
"""Fails the build if temporary debug instrumentation is left in shipping code.

A CI-hang investigation once left 41 [Bisect] Debug.Log calls across the runtime
assemblies - including three interpolated strings plus a captured stack trace on every
UI transition. They compiled fine, passed every other check, and would have shipped in
the APK. This makes that a build failure instead of a code-review hope.

Scope is deliberately the runtime Scripts tree only. Editor code and tests do not ship,
and a marker there (a generator logging its progress, a test's diagnostic) is legitimate.
"""

import pathlib
import re
import sys

# Markers that mean "this was meant to be removed". [Bisect] is the specific tag used by
# the bisection sweeps; the others are the usual "I'll take this out later" words that,
# in a runtime log, usually mean nobody did.
FORBIDDEN = re.compile(r"\[Bisect\]|\bTEMPORARY\b|bisection instrumentation")


def main() -> int:
    root = pathlib.Path(sys.argv[1] if len(sys.argv) > 1 else ".")
    scripts = root / "Assets/_Project/Scripts"

    hits = []
    for path in scripts.rglob("*.cs"):
        rel = path.relative_to(root)
        # Editor code and anything under a Tests folder does not ship.
        parts = set(rel.parts)
        if "Editor" in parts or "Tests" in parts:
            continue

        for i, line in enumerate(path.read_text(errors="ignore").splitlines(), 1):
            if FORBIDDEN.search(line):
                hits.append(f"{rel}:{i}: {line.strip()[:100]}")

    if hits:
        print()
        for h in hits:
            print(f"    debug-instrumentation: {h}")
        print(f"\n{len(hits)} debug-instrumentation marker(s) left in runtime code.")
        return 1

    print("    ok  no debug instrumentation in runtime code")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
