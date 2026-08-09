#!/usr/bin/env python3
"""
Fails when a binary asset is really an unresolved Git LFS pointer.

This bug class is completely invisible without a check. A pointer file exists at the
right path with the right name and extension; Unity imports it and produces a broken
texture; nothing errors. The only symptom is that the artwork does not appear, and the
obvious suspects - the .uss rule, the import settings, the material - are all correct.

It has already happened here. Assets/_Project/UI/Textures/MainMenuBackground.png and
HudReference.png were both committed as 131-byte pointers whose objects were never
pushed. Both CI jobs already pass `lfs: true` and still got stubs, and MainMenu.uss's
`background-image: url('Textures/MainMenuBackground.png')` therefore resolved to a text
file - so the title screen had no background and no way to say so.

Exits non-zero if any file under Assets/ is a pointer.
"""
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
ASSETS = ROOT / "Assets"

MAGIC = b"version https://git-lfs.github.com/spec/v1"

# A pointer file is ~130 bytes. Anything substantially larger is real content, and
# reading only the head keeps this cheap over a tree full of textures.
MAX_POINTER_BYTES = 512


def main() -> int:
    if not ASSETS.is_dir():
        print("    ok  lfs pointers (no Assets directory)")
        return 0

    offenders = []
    scanned = 0

    for path in ASSETS.rglob("*"):
        if not path.is_file() or path.suffix == ".meta":
            continue

        try:
            size = path.stat().st_size
        except OSError:
            continue

        if size > MAX_POINTER_BYTES:
            continue

        scanned += 1
        try:
            with path.open("rb") as handle:
                head = handle.read(len(MAGIC))
        except OSError:
            continue

        if head == MAGIC:
            offenders.append(f"{path.relative_to(ROOT)} ({size} bytes)")

    if offenders:
        print("Unresolved Git LFS pointers - these are text stubs, not assets:",
              file=sys.stderr)
        for offender in offenders:
            print(f"  {offender}", file=sys.stderr)
        print("\nEither run `git lfs pull` and commit the real content, or remove the "
              "file and fetch it through Tools/Assets/fetch-assets.py instead.",
              file=sys.stderr)
        return 1

    print(f"    ok  lfs pointers ({scanned} small file(s) checked, none are stubs)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
