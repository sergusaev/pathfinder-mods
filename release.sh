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

info() { sed -n "s/^[[:space:]]*\"$1\"[[:space:]]*:[[:space:]]*\"\([^\"]*\)\".*/\1/p" "$ROOT/$MOD/Info.json" | head -1; }
ID="$(info Id)"; VERSION="$(info Version)"; NAME="$(info DisplayName)"; DLL="$(info AssemblyName)"
GAME="$(sed -n 's/^GAME=//p' "$ROOT/$MOD/build.sh")"
TAG="${MOD#kingmaker-}"; TAG="${TAG#wotr-}-v$VERSION"
case "$GAME" in
  kingmaker) GAME_NAME="Pathfinder: Kingmaker"; MODS="Mods" ;;
  wotr) GAME_NAME="Pathfinder: Wrath of the Righteous"; MODS="mods" ;;
esac

if [ "$PUBLISH" = 1 ]; then
  [ -z "$(git -C "$ROOT" status --porcelain)" ] || { echo "commit your changes first"; exit 1; }
  git -C "$ROOT" fetch -q origin
  [ "$(git -C "$ROOT" rev-parse HEAD)" = "$(git -C "$ROOT" rev-parse origin/main)" ] || { echo "push main first"; exit 1; }
  ! gh release view "$TAG" -R "${REPO_URL#https://github.com/}" >/dev/null 2>&1 || { echo "$TAG is already released, bump Version in $MOD/Info.json"; exit 1; }
fi

"$ROOT/$MOD/build.sh"

STAGE="$(mktemp -d)"
trap 'rm -rf "$STAGE"' EXIT
mkdir -p "$STAGE/$ID" "$ROOT/dist"
cp "$ROOT/$MOD/build/$DLL" "$ROOT/$MOD/Info.json" "$STAGE/$ID/"
ZIP="$ROOT/dist/$ID-$VERSION.zip"
rm -f "$ZIP"
# zip is missing in Git Bash on Windows; bsdtar (macOS tar, Windows tar.exe) writes zip archives too.
if command -v zip >/dev/null; then
  (cd "$STAGE" && zip -q -r -X "$ZIP" "$ID")
elif tar --version 2>/dev/null | grep -q bsdtar; then
  (cd "$STAGE" && tar -a -cf "$ZIP" "$ID")
elif [ -x /c/Windows/System32/tar.exe ]; then
  (cd "$STAGE" && /c/Windows/System32/tar.exe -a -cf "$ZIP" "$ID")
else
  echo "need zip or bsdtar to pack"; exit 1
fi
echo "packed: $ZIP"

[ "$PUBLISH" = 1 ] || exit 0

if [ -z "$NOTES" ]; then
  NOTES="$STAGE/notes.md"
  cat > "$NOTES" <<EOF
$NAME $VERSION for $GAME_NAME.

- Unpack into \`<game>/$MODS/\` so the result is \`$MODS/$ID/Info.json\`. When updating, delete \`*.cache\` in that folder.

Details: [README]($REPO_URL/blob/main/$MOD/README.md) ([русский]($REPO_URL/blob/main/$MOD/README.ru.md)). Steam Deck setup: [guide]($REPO_URL/blob/main/docs/steam-deck.md) ([русский]($REPO_URL/blob/main/docs/steam-deck.ru.md)).
EOF
fi
gh release create "$TAG" "$ZIP" -R "${REPO_URL#https://github.com/}" --target main --title "$NAME $VERSION" --notes-file "$NOTES"
