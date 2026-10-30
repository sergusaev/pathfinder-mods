#!/bin/bash
# Build Kingmaker Cosmetics and optionally install it. Options and settings: see ../build-mod.sh.
MOD_DLL=KingmakerCosmetics
GAME=kingmaker
# The side braid hair and the eyeliner face, made offline from the game's meshes and textures (see src/SideBraid.cs).
RESOURCES=()
for f in SideBraid.mesh SideBraid.png SideBraidGrowth.png LinerFace.mesh LinerFaceBW.png LinerFaceColorOver.png LinerBrows.png; do RESOURCES+=("assets/$f,KingmakerCosmetics.$f"); done
# Female elf faces with detailed eyes (see src/Eyes.cs), made by sharp_eyes.py of the hair stand.
# (listed from the mod folder, so the build works from any folder; paths in RESOURCES are relative to it)
for f in "$(dirname "$0")"/assets/eyes/*.png; do f="$(basename "$f")"; RESOURCES+=("assets/eyes/$f,KingmakerCosmetics.Eyes.$f"); done
. "$(dirname "$0")/../build-mod.sh"
