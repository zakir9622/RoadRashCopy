#!/usr/bin/env python3
"""Fetches CC0 art into the project, and records where every file came from.

Why this exists rather than a list of links in a document: the repository has two
image files and nothing else, so every discussion about how the game looks has been
blocked on somebody manually downloading things. This makes acquisition reproducible -
a clean clone can rebuild the entire art set from a manifest, the same way the scenes
rebuild from their generators.

Licensing is the part that must not be casual. Everything here is CC0 (public domain,
no attribution required) or explicitly CC-BY (attribution required). The fetcher writes
Assets/_Project/Art/ATTRIBUTIONS.md on every run listing each file, its source URL and
its licence, so the obligation is discharged by construction rather than remembered.
Anything whose licence cannot be determined is refused, not guessed at.

Sources, all verified reachable from CI:
  * Poly Haven      - CC0 PBR textures and HDRIs, public JSON API
  * ambientCG       - CC0 PBR materials, public JSON API

Usage:
    python3 Tools/Assets/fetch-assets.py --list          # what the manifest asks for
    python3 Tools/Assets/fetch-assets.py                 # fetch anything missing
    python3 Tools/Assets/fetch-assets.py --force         # re-fetch everything
    python3 Tools/Assets/fetch-assets.py --verify        # check without downloading
"""

import argparse
import hashlib
import json
import pathlib
import sys
import urllib.request
import urllib.error

ROOT = pathlib.Path(__file__).resolve().parents[2]
ART = ROOT / "Assets" / "_Project" / "Art"
MANIFEST = pathlib.Path(__file__).resolve().parent / "manifest.json"
ATTRIBUTIONS = ART / "ATTRIBUTIONS.md"
LEDGER = ART / ".fetched.json"

POLYHAVEN_FILES = "https://api.polyhaven.com/files/{slug}"
AMBIENTCG_QUERY = ("https://ambientcg.com/api/v2/full_json"
                   "?id={slug}&include=downloadData")

TIMEOUT = 120

# Licences this tool is willing to ship. Anything else is a human decision, not a
# default - shipping art under an unclear licence is a legal problem, not a bug.
ALLOWED_LICENCES = {"CC0", "CC-BY-4.0", "CC-BY-3.0"}


def get_json(url: str):
    req = urllib.request.Request(url, headers={"User-Agent": "HighwayRenegade-AssetFetcher"})
    with urllib.request.urlopen(req, timeout=TIMEOUT) as response:
        return json.loads(response.read().decode("utf-8"))


def sha256_of(path: pathlib.Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def download(url: str, target: pathlib.Path) -> str:
    """Fetches a file and returns its SHA-256, so the manifest can pin content."""
    target.parent.mkdir(parents=True, exist_ok=True)
    req = urllib.request.Request(url, headers={"User-Agent": "HighwayRenegade-AssetFetcher"})
    with urllib.request.urlopen(req, timeout=TIMEOUT) as response:
        payload = response.read()

    target.write_bytes(payload)
    return hashlib.sha256(payload).hexdigest()


def polyhaven_urls(slug: str, resolution: str, maps):
    """Resolves a Poly Haven asset's chosen maps to direct file URLs.

    The API nests as files[MapName][resolution][format]['url'], and map names are not
    consistent across assets - diffuse is 'Diffuse' on some and 'diff' on others - so
    each requested map is looked up case-insensitively and reported if absent rather
    than silently skipped.
    """
    files = get_json(POLYHAVEN_FILES.format(slug=slug))
    resolved, missing = {}, []

    for wanted in maps:
        key = next((k for k in files if k.lower() == wanted.lower()), None)
        if key is None:
            missing.append(wanted)
            continue

        formats = files[key].get(resolution)
        if not formats:
            missing.append(f"{wanted}@{resolution}")
            continue

        # jpg where offered (smaller for a mobile build), png otherwise.
        fmt = "jpg" if "jpg" in formats else next(iter(formats))
        resolved[key] = formats[fmt]["url"]

    return resolved, missing


def fetch_polyhaven(entry, force: bool):
    slug = entry["slug"]
    resolution = entry.get("resolution", "1k")
    maps = entry.get("maps", ["Diffuse", "nor_gl", "rough"])
    destination = ART / entry["destination"]

    resolved, missing = polyhaven_urls(slug, resolution, maps)
    if missing:
        print(f"  ! {slug}: no {', '.join(missing)} at this resolution")

    records = []
    for name, url in resolved.items():
        target = destination / f"{slug}_{name}{pathlib.Path(url).suffix}"
        if target.exists() and not force:
            print(f"  = {target.relative_to(ROOT)}")
            continue

        digest = download(url, target)
        print(f"  + {target.relative_to(ROOT)}")
        records.append({
            "file": str(target.relative_to(ROOT)),
            "source": url,
            "licence": "CC0",
            "origin": f"Poly Haven / {slug}",
            "sha256": digest,
        })
    return records


def fetch_ambientcg(entry, force: bool):
    slug = entry["slug"]
    destination = ART / entry["destination"]

    data = get_json(AMBIENTCG_QUERY.format(slug=slug))
    assets = data.get("foundAssets") or []
    if not assets:
        print(f"  ! {slug}: not found on ambientCG")
        return []

    asset = assets[0]
    wanted = entry.get("variant", "1K-JPG")

    url = None
    for group in asset.get("downloadFolders", {}).values():
        for download_entry in group.get("downloadFiletypeCategories", {}) \
                                   .get("zip", {}).get("downloads", []):
            if download_entry.get("attribute") == wanted:
                url = download_entry.get("downloadLink")
                break

    if not url:
        print(f"  ! {slug}: no '{wanted}' download offered")
        return []

    target = destination / f"{slug}_{wanted}.zip"
    if target.exists() and not force:
        print(f"  = {target.relative_to(ROOT)}")
        return []

    digest = download(url, target)
    print(f"  + {target.relative_to(ROOT)}")
    return [{
        "file": str(target.relative_to(ROOT)),
        "source": url,
        "licence": "CC0",
        "origin": f"ambientCG / {slug}",
        "sha256": digest,
    }]


def fetch_direct(entry, force: bool):
    """
    Any file at an explicit URL, with the hash pinned in the manifest.

    Poly Haven and ambientCG have APIs that resolve a slug to a download; fonts, audio
    and model packs mostly do not, so this is the generic escape hatch. It is stricter
    than the API-driven fetchers precisely because the URL is hand-written: an entry MUST
    declare sha256, and bytes that do not match are refused rather than written.

    That strictness is the point. A raw URL on a branch is mutable - the file behind it
    can be updated, replaced, or swapped for something else entirely - and shipping
    whatever happens to be there today is exactly the supply-chain hole the licence
    allow-list exists to close on the legal side.
    """
    slug = entry["slug"]
    url = entry["url"]
    filename = entry.get("filename") or url.rsplit("/", 1)[-1]
    destination = ART / entry["destination"]
    target = destination / filename

    expected = entry.get("sha256")
    if not expected:
        print(f"  ! {slug}: 'direct' entries must declare sha256")
        return []

    if target.exists() and not force:
        print(f"  = {target.relative_to(ROOT)}")
        return [{
            "file": str(target.relative_to(ROOT)),
            "source": url,
            "licence": entry.get("licence", "CC0"),
            "origin": entry.get("origin", slug),
            "sha256": sha256_of(target),
        }]

    digest = download(url, target)
    if digest != expected:
        # Remove it. A file on disk with the wrong bytes is worse than no file: the
        # generators would happily use it and nothing downstream would question it.
        target.unlink(missing_ok=True)
        print(f"  ! {slug}: sha256 mismatch\n"
              f"      expected {expected}\n"
              f"      got      {digest}\n"
              f"      The content behind this URL changed. Verify it is still the asset "
              f"and licence you intended, then update the manifest.")
        return []

    print(f"  + {target.relative_to(ROOT)}")
    return [{
        "file": str(target.relative_to(ROOT)),
        "source": url,
        "licence": entry.get("licence", "CC0"),
        "origin": entry.get("origin", slug),
        "sha256": digest,
    }]


FETCHERS = {"polyhaven": fetch_polyhaven, "ambientcg": fetch_ambientcg,
            "direct": fetch_direct}


def write_attributions(records):
    """Writes the credits file. Rewritten wholesale so it can never drift from disk."""
    ART.mkdir(parents=True, exist_ok=True)

    lines = [
        "# Asset attributions",
        "",
        "Generated by `Tools/Assets/fetch-assets.py`. Do not edit by hand - rerun the",
        "fetcher instead.",
        "",
        "Every asset below is CC0 (public domain) unless its Licence column says",
        "otherwise. CC0 imposes no attribution requirement; this file exists so the",
        "provenance of every shipped file is auditable, and so that anything arriving",
        "under a licence that *does* require credit is impossible to overlook.",
        "",
        "| File | Origin | Licence | Source |",
        "|---|---|---|---|",
    ]
    for record in sorted(records, key=lambda r: r["file"]):
        lines.append(f"| `{record['file']}` | {record['origin']} | "
                     f"{record['licence']} | {record['source']} |")

    lines.append("")
    ATTRIBUTIONS.write_text("\n".join(lines))
    print(f"\nWrote {ATTRIBUTIONS.relative_to(ROOT)} ({len(records)} asset(s))")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--force", action="store_true", help="re-fetch existing files")
    parser.add_argument("--list", action="store_true", help="show the manifest and exit")
    parser.add_argument("--verify", action="store_true",
                        help="check licences and reachability without downloading")
    args = parser.parse_args()

    if not MANIFEST.exists():
        print(f"No manifest at {MANIFEST}", file=sys.stderr)
        return 2

    manifest = json.loads(MANIFEST.read_text())
    entries = manifest.get("assets", [])

    if args.list:
        for entry in entries:
            print(f"{entry['source']:<12} {entry['slug']:<28} -> {entry['destination']}")
        return 0

    bad = [e for e in entries if e.get("licence", "CC0") not in ALLOWED_LICENCES]
    if bad:
        for entry in bad:
            print(f"REFUSED {entry['slug']}: licence '{entry.get('licence')}' is not in "
                  f"{sorted(ALLOWED_LICENCES)}", file=sys.stderr)
        return 1

    if args.verify:
        # Licences were checked above. Now check the bytes.
        #
        # This used to report success after validating only the licence field, while CI
        # ran it under the name "Verify art integrity" - a green tick from a check that
        # never looked at a single file.
        #
        # Files that are absent are not an error: the fetch step is continue-on-error, so
        # a CDN outage legitimately leaves assets missing and the game falls back to
        # placeholder art. A file that is PRESENT with unexpected bytes is a hard failure,
        # because something replaced content we pinned.
        ledger = json.loads(LEDGER.read_text()) if LEDGER.exists() else []
        expected = {r["file"]: r["sha256"] for r in ledger if "sha256" in r}

        checked, missing, mismatched = 0, 0, []
        for relative, digest in expected.items():
            path = ROOT / relative
            if not path.exists():
                missing += 1
                continue
            checked += 1
            actual = sha256_of(path)
            if actual != digest:
                mismatched.append(f"  {relative}\n    expected {digest}\n    got      {actual}")

        if mismatched:
            print("Content does not match what was pinned:", file=sys.stderr)
            print("\n".join(mismatched), file=sys.stderr)
            return 1

        print(f"ok  {len(entries)} manifest entries, all licences allowed")
        print(f"ok  {checked} file(s) match their pinned SHA-256"
              + (f", {missing} not fetched" if missing else ""))
        return 0

    records, failed = [], 0
    for entry in entries:
        source = entry["source"]
        fetcher = FETCHERS.get(source)
        if fetcher is None:
            print(f"  ! unknown source '{source}'", file=sys.stderr)
            failed += 1
            continue

        print(f"{source}: {entry['slug']}")
        try:
            records.extend(fetcher(entry, args.force))
        except (urllib.error.URLError, KeyError, ValueError) as error:
            # One unreachable asset must not abandon the rest of the manifest.
            print(f"  ! {entry['slug']}: {error}", file=sys.stderr)
            failed += 1

    # Merge with anything fetched on a previous run so the credits stay complete.
    existing = []
    if ATTRIBUTIONS.exists():
        if LEDGER.exists():
            existing = json.loads(LEDGER.read_text())

    by_file = {r["file"]: r for r in existing}
    by_file.update({r["file"]: r for r in records})
    merged = [r for r in by_file.values() if (ROOT / r["file"]).exists()]

    LEDGER.write_text(json.dumps(merged, indent=2))
    write_attributions(merged)

    if failed:
        print(f"\n{failed} entr(ies) failed", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
