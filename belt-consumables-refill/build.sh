#!/bin/bash
# Build Belt Consumables Refill for Kingmaker and Wrath of the Righteous and optionally install it.
# Options and settings: see ../build-mod.sh (./build.sh --game wotr --install builds and installs one game only).
MOD_DLL=BeltConsumablesRefill
GAMES="kingmaker wotr"
. "$(dirname "$0")/../build-mod.sh"
