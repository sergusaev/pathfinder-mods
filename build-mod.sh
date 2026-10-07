#!/bin/bash
# Shared build and install logic, sourced by each mod's build.sh after it sets:
#   MOD_DLL   assembly and Mods folder name, e.g. GamepadCameraRotation
#   GAME      kingmaker | wotr
#
# Usage: ./build.sh [--fetch] [--install]
#   --fetch    copy the game's managed assemblies and UMM into ./refs (done automatically when refs is empty)
#   --install  copy Info.json and the DLL into the game's mods folder (the game must be closed)
#
# Where the game is:
#   DECK=user@host     the game is on another machine reachable over SSH, e.g. DECK=deck@steamdeck.local
#   KINGMAKER_DIR      game folder of Pathfinder: Kingmaker (on DECK if set, else on this machine)
#   WOTR_DIR           game folder of Pathfinder: Wrath of the Righteous
# Defaults are the Steam library under the home folder of the game machine (SteamOS / Linux layout).
# Personal values can be kept in local.env next to this script; it is not tracked by git.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# Mono for Windows does not add itself to PATH.
command -v mcs >/dev/null || [ ! -d "/c/Program Files/Mono/bin" ] || PATH="/c/Program Files/Mono/bin:$PATH"
# mcs.exe on Windows needs Windows paths, also inside the response file.
winpath() { if command -v cygpath >/dev/null; then cygpath -m "$1"; else printf '%s\n' "$1"; fi; }
HERE="$(cd "$(dirname "$0")" && pwd)"
[ -f "$ROOT/local.env" ] && . "$ROOT/local.env"

DECK="${DECK:-}"
STEAM_COMMON="/home/deck/.local/share/Steam/steamapps/common"
[ -z "$DECK" ] && STEAM_COMMON="$HOME/.local/share/Steam/steamapps/common"

case "$GAME" in
  kingmaker)
    GAME_DIR="${KINGMAKER_DIR:-$STEAM_COMMON/Pathfinder Kingmaker}"
    DATA="Kingmaker_Data"; MODS="Mods"; PROCESS="Kingmaker.exe"
    # Kingmaker ships no Harmony; mods compile against the one UMM loads.
    HARMONY_FROM_UMM=1 ;;
  wotr)
    GAME_DIR="${WOTR_DIR:-$STEAM_COMMON/Pathfinder Second Adventure}"
    DATA="Wrath_Data"; MODS="mods"; PROCESS="Wrath.exe"
    # WotR ships Harmony in Managed; reference it so the mod never requires a newer one than is loaded.
    HARMONY_FROM_UMM=0 ;;
  *) echo "GAME must be kingmaker or wotr"; exit 1 ;;
esac

MANAGED="$GAME_DIR/$DATA/Managed"
MOD_DIR="$GAME_DIR/$MODS/$MOD_DLL"
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

on_game() {
  if [ -n "$DECK" ]; then ssh "$DECK" "$1"; else bash -c "$1"; fi
}

if [ "$FETCH" = 1 ] || [ ! -f "$REFS/Assembly-CSharp.dll" ]; then
  mkdir -p "$REFS/umm"
  on_game "cd '$MANAGED' && tar cf - *.dll" | tar -C "$REFS" -xf -
  on_game "cd '$MANAGED/UnityModManager' && tar cf - UnityModManager.dll 0Harmony.dll" | tar -C "$REFS/umm" -xf -
fi

mkdir -p "$OUT"
{
  for dll in "$REFS"/*.dll; do
    [ "$HARMONY_FROM_UMM" = 1 ] && [[ "$(basename "$dll")" == 0Harmony* ]] && continue
    echo "-r:$(winpath "$dll")"
  done
  [ "$HARMONY_FROM_UMM" = 1 ] && echo "-r:$(winpath "$REFS/umm/0Harmony.dll")"
  echo "-r:$(winpath "$REFS/umm/UnityModManager.dll")"
} > "$OUT/refs.rsp"

SOURCES=()
for f in "$HERE"/src/*.cs; do SOURCES+=("$(winpath "$f")"); done
mcs -target:library -nostdlib -out:"$(winpath "$OUT/$MOD_DLL.dll")" @"$(winpath "$OUT/refs.rsp")" "${SOURCES[@]}"
cp "$HERE/Info.json" "$OUT/Info.json"
echo "built: $OUT/$MOD_DLL.dll"

if [ "$INSTALL" = 1 ]; then
  if on_game "pgrep -f '[${PROCESS:0:1}]${PROCESS:1}' >/dev/null 2>&1"; then
    echo "$PROCESS is running, close the game before installing"; exit 1
  fi
  on_game "mkdir -p '$MOD_DIR' && rm -f '$MOD_DIR'/*.cache"
  if [ -n "$DECK" ]; then
    scp -q "$OUT/Info.json" "$OUT/$MOD_DLL.dll" "$DECK:$MOD_DIR/"
  else
    cp "$OUT/Info.json" "$OUT/$MOD_DLL.dll" "$MOD_DIR/"
  fi
  echo "installed to ${DECK:+$DECK:}$MOD_DIR"
fi
