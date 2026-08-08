#!/usr/bin/env python3
"""Ensures every asset has a committed .meta, with a stable GUID.

Unity identifies assets by the GUID inside their .meta file, and scenes, prefabs and
serialized references store that GUID rather than a path. A script with no committed
.meta gets a brand-new random GUID on every fresh import - so CI mints different
identities for the same file on every run.

Fifteen scripts were in that state, including every screen added in the UI refactor.
It happened to work only because those components are added by generators via
AddComponent inside one editor session, where the GUID never has to survive. The
moment any of them is referenced from a committed scene or prefab, that reference
breaks on the next clean build, and the failure reads as "the script is missing" with
nothing pointing at why.

GUIDs are derived from the asset path, so they are deterministic and identical on
every machine, and a regenerated file is byte-identical rather than a spurious diff.

Usage:
    python3 Tools/CompileCheck/check-meta-files.py          # report, exit 1 if missing
    python3 Tools/CompileCheck/check-meta-files.py --write  # create the missing ones
"""

import argparse
import hashlib
import pathlib
import sys

# Unity does not create .meta files for these, and neither should we.
IGNORED_SUFFIXES = {".meta"}
IGNORED_NAMES = {".DS_Store", ".fetched.json", "Thumbs.db", "desktop.ini"}

# Directories whose contents are fetched or generated rather than committed. They get
# their .meta files from Unity at import time on whatever machine fetched them, and
# committing GUIDs for files that are not themselves committed would be meaningless.
IGNORED_DIRS = {"Art"}


def guid_for(relative_path: str) -> str:
    """A stable 32-hex-character GUID derived from the asset path.

    Unity only requires that a GUID is 32 hex characters and unique within the project.
    Hashing the path gives both, and makes the value reproducible - regenerating a
    missing .meta restores exactly the identity every existing reference expects,
    instead of silently renaming the asset from Unity's point of view.
    """
    return hashlib.md5(relative_path.encode("utf-8")).hexdigest()


def needs_meta(path: pathlib.Path) -> bool:
    if path.suffix in IGNORED_SUFFIXES or path.name in IGNORED_NAMES:
        return False
    if path.name.startswith("."):
        return False
    return True


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--write", action="store_true", help="create missing .meta files")
    parser.add_argument("root", nargs="?", default=".")
    args = parser.parse_args()

    root = pathlib.Path(args.root).resolve()
    assets = root / "Assets"
    if not assets.is_dir():
        print("check-meta-files: no Assets directory", file=sys.stderr)
        return 2

    missing, seen_guids, collisions = [], {}, []

    for path in sorted(assets.rglob("*")):
        relative = path.relative_to(root)

        if any(part in IGNORED_DIRS for part in relative.parts):
            continue
        if path.is_dir():
            # Unity gives folders .meta files too; a folder without one is re-identified
            # on every import exactly as a file is.
            meta = path.with_name(path.name + ".meta")
            if not meta.exists():
                missing.append((path, relative, True))
            continue
        if not needs_meta(path):
            continue

        meta = pathlib.Path(str(path) + ".meta")
        if not meta.exists():
            missing.append((path, relative, False))

    # Existing GUIDs must not collide with anything we generate.
    for meta in assets.rglob("*.meta"):
        text = meta.read_text(errors="ignore")
        for line in text.splitlines():
            if line.startswith("guid:"):
                guid = line.split(":", 1)[1].strip()
                if guid in seen_guids:
                    collisions.append((guid, seen_guids[guid], meta))
                seen_guids[guid] = meta
                break

    if collisions:
        for guid, first, second in collisions:
            print(f"DUPLICATE GUID {guid}: {first} and {second}", file=sys.stderr)
        return 1

    if not missing:
        print(f"    ok  meta files ({len(seen_guids)} assets, all identified)")
        return 0

    if not args.write:
        print()
        for _, relative, is_dir in missing:
            kind = "folder" if is_dir else "asset"
            print(f"{relative}: {kind} has no .meta, so its GUID is regenerated on every "
                  "import and any stored reference to it breaks")
        print(f"\n{len(missing)} asset(s) without a .meta. "
              "Run with --write to generate them.")
        return 1

    written = 0
    for path, relative, is_dir in missing:
        guid = guid_for(str(relative).replace("\\", "/"))
        if guid in seen_guids:
            print(f"REFUSED {relative}: derived GUID {guid} already used by "
                  f"{seen_guids[guid]}", file=sys.stderr)
            return 1
        seen_guids[guid] = relative

        meta = path.with_name(path.name + ".meta") if is_dir \
            else pathlib.Path(str(path) + ".meta")

        # Matches the format Unity already wrote for this project: version, GUID, and
        # nothing else. Unity fills in the importer block on first import and keeps
        # the GUID, which is the part that has to stay stable.
        meta.write_bytes(f"fileFormatVersion: 2\nguid: {guid}".encode("utf-8"))
        print(f"  + {meta.relative_to(root)}")
        written += 1

    print(f"\nWrote {written} .meta file(s)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
