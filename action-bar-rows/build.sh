#!/bin/bash
# Build Action Bar Rows for Kingmaker and Wrath of the Righteous (separate code for each game in src/<game>/) and optionally install it.
# Options and settings: see ../build-mod.sh.
MOD_DLL=ActionBarRows
GAMES="kingmaker wotr"
. "$(dirname "$0")/../build-mod.sh"
