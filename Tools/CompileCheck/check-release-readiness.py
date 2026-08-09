#!/usr/bin/env python3
"""Fails the build if a known Play Store release blocker has regressed.

Every check here corresponds to a defect that actually shipped and was invisible until
someone read the settings by hand: a package that could be uploaded exactly once, a
game rendering in the wrong colour space, an app that closed on the back button. These
are cheap, deterministic, licence-free assertions - the natural place to stop them
coming back.

Two sources of truth:
  - ProjectSettings.asset for values Unity reads directly (colour space, predictive back,
    product name).
  - BuildScript.cs for the Android values it sets imperatively at build time (application
    id, versionCode, managed stripping). We assert the *call* is present rather than the
    resulting value, because the value only exists inside a build.
"""

import pathlib
import re
import sys


def main() -> int:
    root = pathlib.Path(sys.argv[1] if len(sys.argv) > 1 else ".")
    settings = (root / "ProjectSettings" / "ProjectSettings.asset").read_text(errors="ignore")
    build = (root / "Assets/_Project/Scripts/Editor/BuildScript.cs").read_text(errors="ignore")

    failures = []

    def want(condition, message):
        if not condition:
            failures.append(message)

    # Colour space: 0 = Gamma, 1 = Linear. A PBR/URP project must be Linear.
    m = re.search(r"^\s*m_ActiveColorSpace:\s*(\d+)", settings, re.M)
    want(m and m.group(1) == "1",
         "ProjectSettings m_ActiveColorSpace is not Linear (1). PBR lighting renders wrong in Gamma.")

    # Predictive back must be OFF, or the hardware back button is not delivered as Escape
    # on API 35 and closes the app mid-race instead of pausing.
    m = re.search(r"^\s*androidPredictiveBackSupport:\s*(\d+)", settings, re.M)
    want(m and m.group(1) == "0",
         "androidPredictiveBackSupport is not 0. Back button will close the app instead of reaching the game.")

    # Product name off the "Road Rash" trademark.
    m = re.search(r"^\s*productName:\s*(.+)$", settings, re.M)
    name = m.group(1).strip() if m else ""
    want("roadrash" not in name.lower().replace(" ", ""),
         f"productName '{name}' contains the Road Rash trademark. Rename it.")

    # BuildScript must set these three explicitly - each was a release blocker when absent.
    want("SetApplicationIdentifier" in build,
         "BuildScript does not set an application id. Package name would be synthesised and non-deterministic.")
    want("bundleVersionCode" in build,
         "BuildScript does not set bundleVersionCode. A pinned versionCode allows exactly one Play upload ever.")
    want("SetManagedStrippingLevel" in build,
         "BuildScript does not set a managed stripping level. Default High can strip JsonUtility save fields on device.")

    if failures:
        print()
        for line in failures:
            print(f"    release-readiness: {line}")
        print(f"\n{len(failures)} release-readiness problem(s).")
        return 1

    print("    ok  release readiness (colour space, back button, package id, versionCode, stripping)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
