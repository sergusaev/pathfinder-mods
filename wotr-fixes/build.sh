#!/bin/bash
# Build WotR Fixes and optionally install it. Options and settings: see ../build-mod.sh.
MOD_DLL=WotrFixes
GAME=wotr
# Release tag: "fixes" alone is taken by Kingmaker Fixes.
TAG_NAME=wotr-fixes
. "$(dirname "$0")/../build-mod.sh"
