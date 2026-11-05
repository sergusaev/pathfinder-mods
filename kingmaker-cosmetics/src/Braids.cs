using System;
using System.Collections.Generic;
using System.Linq;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.CharGen;
using Kingmaker.Blueprints.Root;
using Kingmaker.ResourceLinks;
using Kingmaker.Visual.CharacterSystem;
using UnityEngine;

namespace KingmakerCosmetics
{
    // Braids for human women: the elf French braid (humans have none) fitted to the human skull (the stand's
    // fb_hm_fit.py: moved by the difference of the skulls, its front edge sunk into the skin as on the elf), its
    // painting on the head moved onto the human head layout, and the colours of the human classic ponytail.
    // The braids themselves take the detail colour (Details): the hair texture gets a second colour mask, which the
    // game paints with the entity's second colour list, the detail colours.
    static class Braids
    {
        // The asset id goes into saves and never changes.
        internal static readonly string HairId = Styles.MadeId("EE_Hair_FrenchBraid_F_EL", "human braids");

        static EquipmentEntity s_Hair;
        static CharacterColorsProfile s_Colors;

        internal static void Build()
        {
            var races = BlueprintRoot.Instance.Progression.CharacterRaces;
            var elf = races.FirstOrDefault(r => r != null && r.RaceId == Race.Elf);
            var human = races.FirstOrDefault(r => r != null && r.RaceId == Race.Human);
            if (elf == null || human == null)
            {
                Main.Log.Error("ElfRace or HumanRace not found, no human braids");
                return;
            }
            try
            {
                Hair(elf.FemaleOptions, human.FemaleOptions);
            }
            catch (Exception ex)
            {
                Main.Log.Error($"human braids not made: {ex}");
            }
        }

        static void Hair(CustomizationOptions elf, CustomizationOptions options)
        {
            var link = SideBraid.Link(elf.Hair, "EE_Hair_FrenchBraid_F_EL");
            var original = link == null ? null : link.Load(true);
            var donorLink = SideBraid.Link(options.Hair, "EE_Hair_PonytailClassic_F_HM");
            var donor = donorLink == null ? null : donorLink.Load(true);
            if (original == null || donor == null)
            {
                Main.Log.Error($"no elf French braid ({original != null}) or human classic ponytail ({donor != null}), no human braids");
                return;
            }
            var copy = Styles.Copy(link.AssetId, original, "_BraidsHM");
            if (!SideBraid.Swap(copy, original, "BraidsHM.mesh", m => m.name.StartsWith("H1_FrenchBraid", StringComparison.OrdinalIgnoreCase)))
                return;
            SideBraid.Paint(copy, new Dictionary<string, string> { { "HD_Head_FrenchBraidBW_F_EL", "BraidsGrowthHM.png" } }, Eyes.Fit);
            // The second colour mask on the hair texture (not on its tape).
            var thread = SideBraid.ReadTexture("BraidsThreadHM.png", null);
            int masked = 0;
            foreach (var part in copy.BodyParts.Where(p => p != null && p.Textures != null))
                part.Textures = part.Textures.Select(d =>
                {
                    if (d == null || d.GetMainTextureName() != "H1_FrenchBraid_BW_F_EL")
                        return d;
                    var ramps = d.GetRampMaskDescriptions();
                    var made = new CharacterTextureDescription(d.Channel, d.GetSourceTexture() as Texture2D);
                    // [0] painted with the first colour, [1] with the second: the first must be there.
                    made.GetRampMaskDescriptions().AddRange(ramps.Take(1));
                    if (made.GetRampMaskDescriptions().Count == 0)
                        made.GetRampMaskDescriptions().Add(new RampMaskDescription { Mask = Texture2D.whiteTexture });
                    made.GetRampMaskDescriptions().Add(new RampMaskDescription { Mask = thread });
                    masked++;
                    return made;
                }).ToList();
            if (masked == 0)
            {
                Main.Log.Error($"{copy.name}: no H1_FrenchBraid_BW_F_EL to mask, no human braids");
                return;
            }
            // Colours: the ponytail's hair colours (the very list, so the colours added to it come in the same order),
            // the second ones the detail colours (Details).
            Styles.Keep(donorLink.AssetId, donor);
            s_Colors = ScriptableObject.CreateInstance<CharacterColorsProfile>();
            s_Colors.name = "KingmakerCosmetics_BraidsHM_Colors";
            s_Colors.hideFlags = HideFlags.DontUnloadUnusedAsset;
            s_Colors.PrimaryRamps = donor.ColorsProfile != null ? donor.ColorsProfile.PrimaryRamps
                : (List<Texture2D>)HarmonyLib.AccessTools.Field(typeof(EquipmentEntity), "m_PrimaryRamps").GetValue(donor);
            s_Colors.SecondaryRamps = Details.Palette();
            copy.ColorsProfile = s_Colors;
            Styles.Register(HairId, copy);
            s_Hair = copy;
            Details.Add(HairId, copy);
            options.Hair = options.Hair.Concat(new[] { new EquipmentEntityLink { AssetId = HairId } }).ToArray();
            Main.Log.Log($"{copy.name}: female human hair style {options.Hair.Length}, hair colours of {donor.name} ({s_Colors.PrimaryRamps.Count}), {masked} texture(s) with the braid mask");
        }
    }
}
