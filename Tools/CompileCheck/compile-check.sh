#!/usr/bin/env bash
#
# License-free C# verification for Highway Renegade.
#
# Why this exists: building a Unity project needs an activated licence, so on a machine
# or CI runner without one the project's C# can sit unverified indefinitely - which is
# exactly how this codebase accumulated 40+ compile errors nobody had seen. Roslyn does
# not need a licence. This script compiles every gameplay assembly against real Unity
# reference assemblies (from NuGet) plus faithful stubs for the packages that are not
# publicly downloadable, following the same dependency order Unity uses.
#
# It is a compile gate, not a substitute for a Unity build. See README.md for exactly
# what it does and does not prove.
#
# Usage:  Tools/CompileCheck/compile-check.sh [--tests]
set -uo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
HERE="$ROOT/Tools/CompileCheck"
WORK="${COMPILE_CHECK_WORK:-$HERE/.work}"
REFS="$WORK/refs"
RUN_TESTS=0
[[ "${1:-}" == "--tests" ]] && RUN_TESTS=1

UNITY_MODULES_VERSION="2021.3.33"
NUNIT_VERSION="3.14.0"
NETFX_REF_VERSION="1.0.3"

SRC="$ROOT/Assets/_Project/Scripts"
TESTS="$ROOT/Assets/_Project/Tests"

mkdir -p "$REFS"

log() { printf '\033[1;36m==>\033[0m %s\n' "$*"; }
err() { printf '\033[1;31mERROR:\033[0m %s\n' "$*" >&2; }
ok()  { printf '\033[1;32m    ok\033[0m %s\n' "$*"; }

# ---------------------------------------------------------------------------
# 1. Reference assemblies
# ---------------------------------------------------------------------------
fetch_nupkg() {
  local id="$1" version="$2" dest="$3"
  local lower; lower="$(echo "$id" | tr '[:upper:]' '[:lower:]')"
  # Also runs on a cache hit: a cache saved before this fix existed would otherwise
  # restore the unreadable files and skip straight past the chmod below.
  if [[ -d "$dest" ]]; then chmod -R u+rwX "$dest"; return 0; fi
  log "Fetching $id $version from NuGet"
  local tmp="$WORK/$lower.nupkg"
  curl -sSfL -o "$tmp" \
    "https://api.nuget.org/v3-flatcontainer/$lower/$version/$lower.$version.nupkg" || {
      err "could not download $id $version"; return 1; }
  mkdir -p "$dest"
  unzip -oq "$tmp" -d "$dest"
  rm -f "$tmp"
  # NuGet packages carry Unix permission bits, and UnityEngine.Modules ships its DLLs
  # unreadable by the owner. That goes unnoticed when running as root and fails every
  # reference with "Access to the path is denied" on a normal CI runner.
  chmod -R u+rwX "$dest"
}

fetch_nupkg "UnityEngine.Modules" "$UNITY_MODULES_VERSION" "$REFS/unity" || exit 1
fetch_nupkg "NUnit"               "$NUNIT_VERSION"         "$REFS/nunit" || exit 1
fetch_nupkg "Microsoft.NETFramework.ReferenceAssemblies.net48" \
                                  "$NETFX_REF_VERSION"     "$REFS/netfx" || exit 1

UNITY_LIB="$REFS/unity/lib/net45"
NUNIT_DLL="$(find "$REFS/nunit" -name 'nunit.framework.dll' -path '*netstandard2.0*' | head -1)"
[[ -z "$NUNIT_DLL" ]] && NUNIT_DLL="$(find "$REFS/nunit" -name 'nunit.framework.dll' | head -1)"

UNITY_REFS=()
while IFS= read -r dll; do UNITY_REFS+=("-r:$dll"); done < <(find "$UNITY_LIB" -name '*.dll')

# The Unity reference assemblies on NuGet are net45-flavoured and bind against
# mscorlib 2.0.0.0, so the BCL has to be the .NET Framework reference set - a
# netstandard facade alone leaves every System type unresolvable through them.
# Facades/ carries netstandard.dll, which the NuGet build of nunit.framework needs.
# The EnterpriseServices thunk/wrapper pair are native images, not assemblies, and
# Roslyn errors out if they are passed as references.
NETFX_LIB="$(dirname "$(find "$REFS/netfx" -name 'mscorlib.dll' | head -1)")"
if [[ -z "$NETFX_LIB" || ! -d "$NETFX_LIB" ]]; then
  err "could not locate the net48 reference assemblies"; exit 1
fi
BCL_REFS=()
while IFS= read -r dll; do BCL_REFS+=("-r:$dll"); done < <(
  find "$NETFX_LIB" -maxdepth 2 -name '*.dll' \
    ! -name 'System.EnterpriseServices.Thunk.dll' \
    ! -name 'System.EnterpriseServices.Wrapper.dll')

DOTNET_SEARCH=(/usr/lib/dotnet /usr/share/dotnet)
[[ -n "${DOTNET_ROOT:-}" ]] && DOTNET_SEARCH+=("$DOTNET_ROOT")
[[ -n "${HOME:-}" ]] && DOTNET_SEARCH+=("$HOME/.dotnet")
CSC="$(find "${DOTNET_SEARCH[@]}" -name 'csc.dll' -path '*Roslyn*' 2>/dev/null | head -1)"
if [[ -z "$CSC" ]]; then
  err "Roslyn csc.dll not found - install the .NET SDK (dotnet-sdk-8.0)"; exit 1
fi

# ---------------------------------------------------------------------------
# 2. Compile settings
# ---------------------------------------------------------------------------
# Defines Unity always sets. UNITY_EDITOR is deliberately absent here: it is toggled
# per pass below, because the editor and the Android player compile genuinely
# different branches of this codebase (native JNI haptics vs. the managed fallback,
# editor-only quit handling), and shipping a release means both have to be valid.
DEFINES=( -d:UNITY_2021_3_OR_NEWER -d:UNITY_2022_1_OR_NEWER -d:UNITY_6000_0_OR_NEWER
          -d:UNITY_ANDROID -d:UNITY_INCLUDE_TESTS )

# Warnings that only make sense inside a real Unity compile:
#   CS0649/CS0414 - [SerializeField] members are assigned by the Unity serialiser.
#   CS1701/CS1702 - assembly version unification across the net45 Unity references.
NOWARN="-nowarn:CS0649,CS0414,CS1701,CS1702,CS8032"

COMMON_BASE=( -nologo -nostdlib+ -langversion:9.0 -target:library -optimize- -debug-
              "${DEFINES[@]}" "$NOWARN" )

FAILED=0
PASSES_DONE=0

# csc_build <name> <srcdir> [upstream-name-or-dll-path ...]
#
# An assembly whose only errors were documented reference gaps still fails codegen, so
# it produces no .dll. Its consumers then compile its sources inline instead - the code
# is still checked, it just is not packaged separately.
csc_build() {
  local name="$1" src="$2"; shift 2
  SRCDIR["$name"]="$src"

  local -a files=() extrarefs=()
  while IFS= read -r f; do files+=("$f"); done < <(find "$src" -name '*.cs')

  local up
  for up in "$@"; do
    case "$up" in
      /*) extrarefs+=("-r:$up") ;;
      *)  if [[ -n "${BUILT[$up]:-}" ]]; then
            extrarefs+=("-r:$OUT/$up.dll")
          else
            while IFS= read -r f; do files+=("$f"); done < <(find "${SRCDIR[$up]}" -name '*.cs')
          fi ;;
    esac
  done

  if [[ ${#files[@]} -eq 0 ]]; then
    err "$name: no source files found"; FAILED=1; return 1
  fi

  log "Compiling $name (${#files[@]} files)"
  local logf="$OUT/$name.log"
  dotnet "$CSC" "${COMMON[@]}" \
    "${BCL_REFS[@]}" "${UNITY_REFS[@]}" -r:"$STUBS_DLL" \
    "${extrarefs[@]}" \
    -out:"$OUT/$name.dll" "${files[@]}" > "$logf" 2>&1

  local all real gaps
  all="$(grep "error CS" "$logf" | sed "s|$ROOT/||" | sort -u)"
  if [[ -z "$all" ]]; then
    BUILT["$name"]=1; ok "$name"; return 0
  fi

  real="$(echo "$all" | grep -vFf "$GAPS" || true)"
  gaps="$(echo "$all" | grep -Ff "$GAPS" || true)"

  if [[ -n "$real" ]]; then
    FAILED=1
    printf '\033[1;31m--- %s FAILED ---\033[0m\n' "$name"
    echo "$real"
    return 1
  fi

  ok "$(printf '%s \033[2m(%s documented reference-gap line(s))\033[0m' \
        "$name" "$(echo "$gaps" | wc -l)")"
  return 0
}

# ---------------------------------------------------------------------------
# 3. Two passes: the Android player, then the editor
# ---------------------------------------------------------------------------
for CONFIG in player editor; do
  COMMON=( "${COMMON_BASE[@]}" )
  [[ "$CONFIG" == "editor" ]] && COMMON+=( -d:UNITY_EDITOR )

  OUT="$WORK/out-$CONFIG"
  mkdir -p "$OUT"
  STUBS_DLL="$OUT/HighwayRenegade.Stubs.dll"

  # Strip comments and blank lines: an empty pattern makes grep -F match every line,
  # which would silently suppress every real error in the build.
  GAPS="$OUT/.gaps"
  grep -vE '^[[:space:]]*(#|$)' "$HERE/reference-gaps.txt" > "$GAPS"
  [[ -s "$GAPS" ]] || echo '__no_gaps_configured__' > "$GAPS"

  unset BUILT SRCDIR
  declare -A BUILT=() SRCDIR=()

  printf '\n\033[1;35m### %s configuration\033[0m\n' "$CONFIG"

  # shellcheck disable=SC2046
  dotnet "$CSC" "${COMMON[@]}" "${BCL_REFS[@]}" "${UNITY_REFS[@]}" \
    -r:"$NUNIT_DLL" -out:"$STUBS_DLL" $(find "$HERE/Stubs" -name '*.cs') \
    > "$OUT/stubs.log" 2>&1
  if grep -q "error CS" "$OUT/stubs.log"; then
    err "the stubs themselves do not compile - fix Tools/CompileCheck/Stubs"
    grep "error CS" "$OUT/stubs.log" | sort -u
    exit 1
  fi
  ok "stubs"

  # Mirrors the asmdef reference graph in Assets/_Project.
  csc_build HighwayRenegade.Core        "$SRC/Core"
  csc_build HighwayRenegade.Platform    "$SRC/Platform"    HighwayRenegade.Core
  csc_build HighwayRenegade.Performance "$SRC/Performance" HighwayRenegade.Core
  csc_build HighwayRenegade.Gameplay    "$SRC/Gameplay"    HighwayRenegade.Core \
    HighwayRenegade.Performance HighwayRenegade.Platform

  # The Editor assembly compiles only in the editor pass - UNITY_EDITOR and the
  # UnityEditor stubs are both required. Three Editor-assembly failures reached CI
  # before this was covered, every one a type-resolution error that a stub compile
  # catches. See Stubs/UnityEditorApi.cs for the fidelity caveat.
  if [[ "$CONFIG" == "editor" ]]; then
    csc_build HighwayRenegade.Editor "$SRC/Editor" HighwayRenegade.Core \
      HighwayRenegade.Performance HighwayRenegade.Platform HighwayRenegade.Gameplay
  fi

  if [[ $RUN_TESTS -eq 1 ]]; then
    # EditMode tests are editor-only, exactly as their asmdef declares
    # ("includePlatforms": ["Editor"]), and they reference HighwayRenegade.Editor so
    # they can run the scene generators. Compiling them in the player pass would fail
    # on an assembly that legitimately does not exist there.
    if [[ "$CONFIG" == "editor" ]]; then
      csc_build HighwayRenegade.Tests.EditMode "$TESTS/EditMode" HighwayRenegade.Core \
        HighwayRenegade.Performance HighwayRenegade.Platform HighwayRenegade.Gameplay \
        HighwayRenegade.Editor "$NUNIT_DLL"
    fi

    csc_build HighwayRenegade.Tests.PlayMode "$TESTS/PlayMode" HighwayRenegade.Core \
      HighwayRenegade.Performance HighwayRenegade.Platform HighwayRenegade.Gameplay "$NUNIT_DLL"
  fi

  PASSES_DONE=$((PASSES_DONE + 1))
done

# The Editor assembly is not compiled here (see README), so its string-keyed
# serialized references get a static check of their own.
echo
python3 "$HERE/check-serialized-refs.py" || FAILED=1

# This harness references every Unity module at once, so it cannot see per-assembly
# reference errors that Unity fails on. Checked statically instead.
python3 "$HERE/check-assembly-refs.py" || FAILED=1

# Code that compiles is not the same as code that runs. Twelve systems here were fully
# written and reachable from nothing - no script, no scene, no generator - so they were
# dead in the shipped build with no error anywhere to say so.
python3 "$HERE/check-reachability.py" || FAILED=1

# Unity identifies assets by the GUID in their .meta, and stored references use that
# GUID rather than a path. An asset with no committed .meta is re-identified on every
# clean import, so any reference to it breaks and reports as "script missing".
python3 "$HERE/check-meta-files.py" || FAILED=1

# A Git LFS pointer that was never resolved is a text file wearing a .png extension.
# Unity imports it as a broken texture and reports nothing, so the artwork is simply
# absent with no error to explain it. Two of these shipped in this repository.
python3 "$HERE/check-lfs-pointers.py" || FAILED=1

# Known Play Store release blockers - colour space, back button, package id, versionCode,
# stripping - each of which shipped broken once and was invisible until read by hand.
python3 "$HERE/check-release-readiness.py" || FAILED=1

# Temporary debug instrumentation left in a runtime assembly. 41 [Bisect] logs once
# reached the shipping APK; this makes that a failure rather than a review hope.
python3 "$HERE/check-no-debug-instrumentation.py" || FAILED=1

echo
if [[ $PASSES_DONE -ne 2 ]]; then
  err "only $PASSES_DONE of 2 configurations completed - the harness itself failed"
  exit 1
fi
if [[ $FAILED -eq 0 ]]; then
  printf '\033[1;32mAll assemblies compiled clean (player + editor).\033[0m\n'
else
  printf '\033[1;31mCompilation failed - see above.\033[0m\n'
fi
exit $FAILED
