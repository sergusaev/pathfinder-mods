#!/bin/bash
# Build Scrolls for Kingmaker and Wrath of the Righteous and optionally install it. Options and settings: see ../build-mod.sh.
MOD_DLL=LearnScrolls
GAMES="kingmaker wotr"
RESOURCES=()
for name in scroll_km wotr_card_big wotr_card_small km_learn km_window party_km mark_like_km mark_dislike_km mark_like_wotr mark_dislike_wotr; do RESOURCES+=("art/$name.png,LearnScrolls.$name.png"); done
for icon in learn window; do
  for state in normal hover down; do RESOURCES+=("art/wotr_${icon}_${state}.png,LearnScrolls.wotr_${icon}_${state}.png"); done
done
. "$(dirname "$0")/../build-mod.sh"
