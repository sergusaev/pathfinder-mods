#!/bin/bash
# Build Gamepad Camera Rotation against the game's assemblies and optionally install it.
# Usage: ./build.sh [--fetch] [--install]
#   --fetch    copy Kingmaker_Data/Managed/*.dll and UMM assemblies from the deck into ./refs
#   --install  copy Info.json and the DLL into the game's Mods folder on the deck
set -euo pipefail

DECK="${DECK:-deck@steamdeck.local}"
GAME="/home/deck/.local/share/Steam/steamapps/common/Pathfinder Kingmaker"
MOD_DIR="$GAME/Mods/GamepadCameraRotation"
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
  ssh "$DECK" "cd '$GAME/Kingmaker_Data/Managed' && tar cf - *.dll" | tar -C "$REFS" -xf -
  scp -q "$DECK:$GAME/Kingmaker_Data/Managed/UnityModManager/UnityModManager.dll" \
         "$DECK:$GAME/Kingmaker_Data/Managed/UnityModManager/0Harmony.dll" "$REFS/umm/"
fi

mkdir -p "$OUT"
{
  ls "$REFS"/*.dll | grep -v "/0Harmony" | sed 's/^/-r:/'
  echo "-r:$REFS/umm/0Harmony.dll"
  echo "-r:$REFS/umm/UnityModManager.dll"
} > "$OUT/refs.rsp"

mcs -target:library -nostdlib -out:"$OUT/GamepadCameraRotation.dll" @"$OUT/refs.rsp" "$HERE"/src/*.cs
cp "$HERE/Info.json" "$OUT/Info.json"
rm -rf "$OUT/Compass" && cp -R "$HERE/Compass" "$OUT/Compass"
echo "built: $OUT/GamepadCameraRotation.dll"

if [ "$INSTALL" = 1 ]; then
  if ssh "$DECK" "pgrep -f '[K]ingmaker.exe' >/dev/null"; then
    echo "Kingmaker is running, close it before installing"; exit 1
  fi
  ssh "$DECK" "mkdir -p '$MOD_DIR' && rm -f '$MOD_DIR'/*.cache"
  scp -q "$OUT/Info.json" "$OUT/GamepadCameraRotation.dll" "$DECK:$MOD_DIR/"
  scp -qr "$OUT/Compass" "$DECK:$MOD_DIR/"
  echo "installed to $DECK:$MOD_DIR"
fi
