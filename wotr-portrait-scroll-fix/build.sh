#!/bin/bash
# Build Portrait Scroll Fix (gamepad) against the game's assemblies and optionally install it.
# Usage: ./build.sh [--fetch] [--install]
#   --fetch    copy Wrath_Data/Managed/*.dll and UMM assemblies from the deck into ./refs
#   --install  copy Info.json and the DLL into the game's mods folder on the deck
set -euo pipefail

DECK="${DECK:-deck@steamdeck.local}"
GAME="/home/deck/.local/share/Steam/steamapps/common/Pathfinder Second Adventure"
MOD_DIR="$GAME/mods/PortraitScrollFix"
HERE="$(cd "$(dirname "$0")" && pwd)"
REFS="$HERE/refs"
OUT="$HERE/build"

FETCH=0; INSTALL=0
for a in "$@"; do
  case "$a" in
    --fetch) FETCH=1 ;;
    --install) INSTALL=1 ;;
    *) echo "unknown option: $a"; exit 1 ;;
  esac
done

if [ "$FETCH" = 1 ] || [ ! -f "$REFS/Assembly-CSharp.dll" ]; then
  mkdir -p "$REFS/umm"
  ssh "$DECK" "cd '$GAME/Wrath_Data/Managed' && tar cf - *.dll" | tar -C "$REFS" -xf -
  scp -q "$DECK:$GAME/Wrath_Data/Managed/UnityModManager/UnityModManager.dll" "$REFS/umm/"
fi

mkdir -p "$OUT"
{
  ls "$REFS"/*.dll | grep -v "/0Harmony.dll$" | sed 's/^/-r:/'
  echo "-r:$REFS/0Harmony.dll"
  echo "-r:$REFS/umm/UnityModManager.dll"
} > "$OUT/refs.rsp"

mcs -target:library -nostdlib -out:"$OUT/PortraitScrollFix.dll" @"$OUT/refs.rsp" "$HERE/src/Main.cs"
cp "$HERE/Info.json" "$OUT/Info.json"
echo "built: $OUT/PortraitScrollFix.dll"

if [ "$INSTALL" = 1 ]; then
  if ssh "$DECK" "pgrep -f '[W]rath.exe' >/dev/null"; then
    echo "Wrath is running, close it before installing"; exit 1
  fi
  ssh "$DECK" "mkdir -p '$MOD_DIR' && rm -f '$MOD_DIR'/*.cache"
  scp -q "$OUT/Info.json" "$OUT/PortraitScrollFix.dll" "$DECK:$MOD_DIR/"
  echo "installed to $DECK:$MOD_DIR"
fi
