#!/bin/bash
# Build Time Control and Deadlines for Kingmaker and optionally install it.
# Options and settings: see ../build-mod.sh.
MOD_DLL=TimeControl
GAME=kingmaker
RESOURCES=("art/card_time.png,TimeControl.card_time.png" "art/card_time_small.png,TimeControl.card_time_small.png")
. "$(dirname "$0")/../build-mod.sh"
