#!/bin/bash
# Package one mod as a release zip and optionally publish it on GitHub.
#
# Usage: ./release.sh <mod folder> [--publish] [--notes FILE]
#   <mod folder>  e.g. kingmaker-gamepad-camera-rotation
#   --publish     create the tag and the GitHub release (needs gh, a clean tree and HEAD pushed)
#   --notes FILE  release notes in Markdown; without it a short default text is used
#
# The zip goes to dist/<ModId>-<Version>.zip and holds one folder <ModId>/ with Info.json and the DLL,
# ready to unpack into the game's mods folder. The tag is the folder name without the game prefix
# plus the version from Info.json: kingmaker-gamepad-camera-rotation 1.1.0 -> gamepad-camera-rotation-v1.1.0.
# A mod built for both games (GAMES= in its build.sh) gets one zip per game, dist/<ModId>-<Version>-<game>.zip,
# both attached to the same release.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")" && pwd)"
REPO_URL="https://github.com/sergusaev/pathfinder-mods"

MOD="${1:-}"
[ -n "$MOD" ] && [ -f "$ROOT/${MOD%/}/Info.json" ] || { echo "usage: ./release.sh <mod folder> [--publish] [--notes FILE]"; exit 1; }
MOD="${MOD%/}"
shift

PUBLISH=0; NOTES=""
while [ $# -gt 0 ]; do
  case "$1" in
    --publish) PUBLISH=1 ;;
    --notes) NOTES="$2"; shift ;;
    *) echo "unknown option: $1"; exit 1 ;;
  esac
  shift
done

# tr -d '\r': on Windows the files may be checked out with CRLF.
info() { tr -d '\r' < "$ROOT/$MOD/Info.json" | sed -n "s/^[[:space:]]*\"$1\"[[:space:]]*:[[:space:]]*\"\([^\"]*\)\".*/\1/p" | head -1; }
setting() { tr -d '\r' < "$ROOT/$MOD/build.sh" | sed -n "s/^$1=//p" | tr -d '"' | head -1; }
ID="$(info Id)"; VERSION="$(info Version)"; NAME="$(info DisplayName)"; DLL="$(info AssemblyName)"
GAMES="$(setting GAMES)"
MULTI=1
[ -n "$GAMES" ] || { GAMES="$(setting GAME)"; MULTI=0; }
TAG="${MOD#kingmaker-}"; TAG="${TAG#wotr-}-v$VERSION"
game_name() {
  case "$1" in
    kingmaker) echo "Pathfinder: Kingmaker" ;;
    wotr) echo "Pathfinder: Wrath of the Righteous" ;;
  esac
}

if [ "$PUBLISH" = 1 ]; then
  [ -z "$(git -C "$ROOT" status --porcelain)" ] || { echo "commit your changes first"; exit 1; }
  git -C "$ROOT" fetch -q origin
  [ "$(git -C "$ROOT" rev-parse HEAD)" = "$(git -C "$ROOT" rev-parse origin/main)" ] || { echo "push main first"; exit 1; }
  ! gh release view "$TAG" -R "${REPO_URL#https://github.com/}" >/dev/null 2>&1 || { echo "$TAG is already released, bump Version in $MOD/Info.json"; exit 1; }
fi

# "bash": the execute bit is lost on Windows checkouts.
bash "$ROOT/$MOD/build.sh"

STAGE="$(mktemp -d)"
trap 'rm -rf "$STAGE"' EXIT
mkdir -p "$ROOT/dist"
ZIPS=()
for GAME in $GAMES; do
  SUB=""; SUFFIX=""
  [ "$MULTI" = 1 ] && { SUB="/$GAME"; SUFFIX="-$GAME"; }
  rm -rf "$STAGE/pack"
  mkdir -p "$STAGE/pack/$ID"
  cp "$ROOT/$MOD/build$SUB/$DLL" "$ROOT/$MOD/Info.json" "$STAGE/pack/$ID/"
  ZIP="$ROOT/dist/$ID-$VERSION$SUFFIX.zip"
  rm -f "$ZIP"
  # zip is missing in Git Bash on Windows; bsdtar (macOS tar, Windows tar.exe) writes zip archives too.
  if command -v zip >/dev/null; then
    (cd "$STAGE/pack" && zip -q -r -X "$ZIP" "$ID")
  elif tar --version 2>/dev/null | grep -q bsdtar; then
    (cd "$STAGE/pack" && tar -a -cf "$ZIP" "$ID")
  elif [ -x /c/Windows/System32/tar.exe ]; then
    (cd "$STAGE/pack" && /c/Windows/System32/tar.exe -a -cf "$ZIP" "$ID")
  else
    echo "need zip or bsdtar to pack"; exit 1
  fi
  echo "packed: $ZIP"
  ZIPS+=("$ZIP")
done

[ "$PUBLISH" = 1 ] || exit 0

if [ -z "$NOTES" ]; then
  NOTES="$STAGE/notes.md"
  {
    if [ "$MULTI" = 1 ]; then
      echo "$NAME $VERSION for $(for g in $GAMES; do game_name "$g"; done | paste -sd '|' - | sed 's/|/ and /g')."
      echo
      for g in $GAMES; do
        MODS=mods; [ "$g" = kingmaker ] && MODS=Mods
        echo "- $(game_name "$g"): \`$ID-$VERSION-$g.zip\`, unpack into \`<game>/$MODS/\` so the result is \`$MODS/$ID/Info.json\`."
      done
      echo "- When updating, delete \`*.cache\` in the mod folder."
    else
      MODS=mods; [ "$GAMES" = kingmaker ] && MODS=Mods
      echo "$NAME $VERSION for $(game_name "$GAMES")."
      echo
      echo "- Unpack into \`<game>/$MODS/\` so the result is \`$MODS/$ID/Info.json\`. When updating, delete \`*.cache\` in that folder."
    fi
  } > "$NOTES"
  cat >> "$NOTES" <<EOF

Details: [README]($REPO_URL/blob/main/$MOD/README.md) ([русский]($REPO_URL/blob/main/$MOD/README.ru.md)). Steam Deck setup: [guide]($REPO_URL/blob/main/docs/steam-deck.md) ([русский]($REPO_URL/blob/main/docs/steam-deck.ru.md)).
EOF
fi
gh release create "$TAG" "${ZIPS[@]}" -R "${REPO_URL#https://github.com/}" --target main --title "$NAME $VERSION" --notes-file "$NOTES"
