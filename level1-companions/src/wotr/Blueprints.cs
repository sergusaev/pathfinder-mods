using System;
using System.Collections.Generic;
using HarmonyLib;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Classes;
using Kingmaker.Blueprints.JsonSystem;

namespace Level1Companions
{
    // Companions get preset levels from AddClassLevels only up to ClassLevelLimit; the rest is left to the player.
    // Lowering the limit of every story companion to 1 makes the player build them from level 2.
    // MythicLevelLimit is not touched: mythic levels come from the story.
    // WotR loads blueprints on demand, so the companions are a fixed list of the recruitable blueprints (the 39 of the
    // lvl1companions mod plus the act 1 companions). A respec may rebuild a companion from another blueprint
    // (ReplaceUnitBlueprintForRespec), which gets the same limit.
    [HarmonyPatch(typeof(BlueprintsCache), nameof(BlueprintsCache.Init))]
    static class BlueprintsCachePatch
    {
        static readonly string[] Companions =
        {
            // Act 1 companions: their limit may already be at most 1, then they are only logged.
            "397b090721c41044ea3220445300e1b8", // Camelia_Companion
            "474cee514f4044ad824f08cf7a8a446c", // DLC1_Camelia_Companion
            "cb29621d99b902e4da6f5d232352fbda", // Lann_Companion
            "986c5a1f8c7a44b28932fa03492463b7", // DLC1_Lann_Companion
            "54be53f0b35bf3c4592a97ae335fe765", // Seelah_Companion
            "dd66011f238344ffa9c3203497359dec", // DLC1_Seelah_Companion
            "ae766624c03058440a036de90a7f2009", // Wenduag_Companion
            "3eb598ce726a4182bf5aa7eb6b7fa116", // DLC1_Wenduag_Companion
            // The list of lvl1companions.
            "2779754eecffd044fbd4842dba55312c", // Ember_Companion
            "1cbbbb892f93c3d439f8417ad7cbb6aa", // SosielVaenic_Companion
            "a352873d37ec6c54c9fa8f6da3a6b3e1", // Arueshalae_Companion
            "d841124e77c597446ba68b70ac24695a", // Arueshalae_Companion_thirdchapter
            "e3bc95db7e2181d41847b3a1d858258d", // EvilArueshalae_Companion
            "551bd7f67b0747999c09ad202049b3aa", // DLC1_Arueshalae_Companion
            "22dd2b7cdde746b68b6bfee942c057fa", // DLC1_EvilArueshalae_Companion
            "ca6a513a1cfc4a8abd579ed0670e8d3e", // DLC1_SosielVaenic_Companion
            "7c561e32d451465f9ba49f75be0829ab", // DLC1_Ember_Companion
            "42f0d5ec3dc844feb44b04507a7c1bfc", // Ulbrig_Companion
            "27e297e9abdb4914857256d534db6dce", // DLC1_Ulbrig_Companion
            "766435873b1361c4287c351de194e5f9", // Woljif_Companion
            "0b8de3c5ae40457cb1cde03aa8249e5b", // DLC1_Woljif_Companion
            "096fc4a96d675bb45a0396bcaa7aa993", // Daeran_Companion
            "02f5e3523b0e4f35bb2e6304819f7c0e", // DLC1_Daeran_Companion
            "0d37024170b172346b3769df92a971f5", // Regill_Companion
            "a1c3429385ab481b9f0ee2fd5af63de8", // DLC1_Regill_Companion
            "1b893f7cf2b150e4f8bc2b3c389ba71d", // Nenio_Companion
            "5642e394eac9447e819646723ffa25a3", // DLC1_Nenio_Companion
            "0bcf3c125a28d164191e874e3c0c52de", // Staunton_Companion
            "6ed29d95acaa439488ed315d4c10c325", // DLC1_Staunton_Companion
            "7ece3afabe2b6f343b17d1eaa409d273", // Ciar_Companion
            "6925dc8244ff439785cebf2886343188", // DLC1_Ciar_Companion
            "6b1f599497f5cfa42853d095bda6dafd", // Delamere_Companion
            "9b949097e0a04039b1f5d1a5b4942094", // DLC1_Delamere_Companion
            "e551850403d61eb48bb2de010d12c894", // Kestoglyr_Companion
            "c869855405814944be57503757bb6e28", // DLC1_Kestoglyr_Companion
            "e46927657a79db64ea30758db3f42bb9", // Galfrey_Companion
            "e324e93488344bdb9b48e79259e06513", // DLC1_Galfrey_Companion
            "61857fa27d0140e09bb82e81d7a5094a", // DLC1_LichGalfrey_Companion
            "d58b81fd7ec14784fa05bc29fb6c7ae0", // LichGalfrey_Companion
            "07d1bfddf0da488caa4216c9fe136893", // DLC5_Rekarth_companion
            "561036c882a640089b1d42f03ebe3a6c", // Sendri_companion
            "902da4e4180747769380d6e4b0f911c2", // DLC5_Sendri_companion
            "942862a48f1644ae85ac5e3c9deb720c", // Penta_companion
            "f72bb7c48bb3e45458f866045448fb58", // Greybor_Companion
            "b43aa85d138e4ec0bfcaffa340b28ea8", // DLC1_Greybor_Companion
            "0bb1c03b9f7bbcf42bb74478af2c6258", // Trever_Companion
            "fda67ccfd9da45c3b5c48adb63ac075e", // DLC1_Trever_Companion
        };

        static bool s_Done;

        static void Postfix()
        {
            if (s_Done) return;
            s_Done = true;
            try
            {
                foreach (string guid in Companions)
                {
                    BlueprintUnit unit = ResourcesLibrary.TryGetBlueprint<BlueprintUnit>(BlueprintGuid.Parse(guid));
                    if (unit == null)
                    {
                        Main.Log.Log($"skip (not found): {guid}");
                        continue;
                    }
                    Patch(unit, "");
                    BlueprintUnit replacement = unit.GetComponent<ReplaceUnitBlueprintForRespec>()?.Blueprint;
                    if (replacement != null && replacement != unit)
                        Patch(replacement, $" (respec blueprint of {unit.name})");
                }
                Main.Log.Log($"Companions patched: {Main.Patched.Count}");
            }
            catch (Exception e)
            {
                Main.Log.Error(e.ToString());
            }
        }

        static void Patch(BlueprintUnit unit, string note)
        {
            if (Main.Patched.ContainsKey(unit)) return;
            ClassLevelLimit limit = unit.GetComponent<ClassLevelLimit>();
            MythicLevelLimit mythic = unit.GetComponent<MythicLevelLimit>();
            string info = $"{unit.name} {unit.AssetGuid}{note} limit {limit?.LevelLimit.ToString() ?? "none"}, mythic limit {mythic?.LevelLimit.ToString() ?? "none"}";
            if (limit == null || limit.LevelLimit <= Main.Limit)
            {
                Main.Log.Log($"skip ({(limit == null ? "no level limit" : "limit already low")}): {info}");
                return;
            }
            Main.Patched[unit] = limit.LevelLimit;
            limit.LevelLimit = Main.Limit;
            Main.Log.Log($"patched: {info} -> {Main.Limit}");
        }
    }
}
