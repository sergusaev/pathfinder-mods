using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Root;
using Kingmaker.Visual.CharacterSystem;
using UnityEngine;

namespace KingmakerCosmetics
{
    // Every race gets the hair colours of all races: a list of hair colours (of hair, beards and eyebrows) gets the
    // colours of the others after its own, in one order, so a colour chosen keeps its number in saves. The colours are
    // copied: the hair they come from is unloaded with its bundle. A colour looking the same as one already there is
    // not added.
    static class HairColors
    {
        static readonly List<Texture2D> s_All = new List<Texture2D>();
        static readonly HashSet<string> s_Names = new HashSet<string>();
        static readonly Dictionary<List<Texture2D>, List<Texture2D>> s_Extended = new Dictionary<List<Texture2D>, List<Texture2D>>();
        static bool s_Ready;

        internal static void Build()
        {
            int copied = 0, kept = 0, same = 0;
            foreach (var race in BlueprintRoot.Instance.Progression.CharacterRaces.Where(r => r != null))
                foreach (var options in new[] { race.MaleOptions, race.FemaleOptions })
                    foreach (var link in options.Hair.Concat(options.Beards))
                    {
                        if (link == null || string.IsNullOrEmpty(link.AssetId) || !ResourcesLibrary.LibraryObject.ResourceNamesByAssetId.ContainsKey(link.AssetId))
                            continue;
                        var ee = ResourcesLibrary.TryGetResource<EquipmentEntity>(link.AssetId, true);
                        if (ee == null)
                            continue;
                        foreach (var ramp in ee.PrimaryRamps)
                        {
                            if (ramp == null || !s_Names.Add(ramp.name))
                                continue;
                            if (s_All.Any(r => Same(r, ramp)))
                            {
                                same++;
                                continue;
                            }
                            var copy = Copy(ramp);
                            if (copy != ramp) copied++; else kept++;
                            s_Looks[copy] = Look(ramp);
                            s_All.Add(copy);
                        }
                    }
            s_Ready = true;
            Main.Log.Log($"hair colours: {s_All.Count} in all ({copied} copied, {kept} kept as they are, {same} looking the same as others left out)");
        }

        static readonly Dictionary<Texture2D, Color32[]> s_Looks = new Dictionary<Texture2D, Color32[]>();

        // The colours of the ramp along its length, read back from the graphics card.
        static Color32[] Look(Texture2D ramp)
        {
            Color32[] look;
            if (s_Looks.TryGetValue(ramp, out look))
                return look;
            const int Width = 32, Height = 4;
            var target = RenderTexture.GetTemporary(Width, Height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            var active = RenderTexture.active;
            Graphics.Blit(ramp, target);
            RenderTexture.active = target;
            var small = new Texture2D(Width, Height, TextureFormat.RGBA32, false, true);
            small.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            RenderTexture.active = active;
            RenderTexture.ReleaseTemporary(target);
            look = small.GetPixels32().Skip(Width * (Height / 2)).Take(Width).ToArray();
            UnityEngine.Object.Destroy(small);
            s_Looks[ramp] = look;
            return look;
        }

        // Ramps no one would tell apart.
        static bool Same(Texture2D a, Texture2D b)
        {
            const int Close = 12;
            Color32[] x = Look(a), y = Look(b);
            for (int i = 0; i < x.Length && i < y.Length; i++)
                if (Math.Abs(x[i].r - y[i].r) > Close || Math.Abs(x[i].g - y[i].g) > Close || Math.Abs(x[i].b - y[i].b) > Close)
                    return false;
            return true;
        }

        static Texture2D Copy(Texture2D ramp)
        {
            try
            {
                var copy = new Texture2D(ramp.width, ramp.height, ramp.format, false, false)
                {
                    name = ramp.name,
                    wrapMode = ramp.wrapMode,
                    filterMode = ramp.filterMode,
                    hideFlags = HideFlags.DontUnloadUnusedAsset,
                };
                Graphics.CopyTexture(ramp, 0, 0, copy, 0, 0);
                return copy;
            }
            catch (System.Exception ex)
            {
                Main.Log.Log($"hair colour {ramp.name} not copied ({ex.Message}), kept as it is");
                return ramp;
            }
        }

        // The list with the colours it lacks after its own, if it is a list of hair colours.
        internal static List<Texture2D> Extend(List<Texture2D> ramps)
        {
            if (!s_Ready || ramps == null || ramps.Count == 0)
                return ramps;
            List<Texture2D> extended;
            if (s_Extended.TryGetValue(ramps, out extended) && extended.Count >= ramps.Count)
                return extended;
            var names = new HashSet<string>(ramps.Where(r => r != null).Select(r => r.name));
            if (!names.Any(s_Names.Contains))
                return ramps;
            extended = new List<Texture2D>(ramps);
            extended.AddRange(s_All.Where(r => !names.Contains(r.name) && !ramps.Any(o => o != null && Same(o, r))));
            s_Extended[ramps] = extended;
            return extended;
        }
    }

    [HarmonyPatch(typeof(EquipmentEntity), nameof(EquipmentEntity.PrimaryRamps), MethodType.Getter)]
    static class AllHairColors
    {
        static void Postfix(ref List<Texture2D> __result)
        {
            __result = HairColors.Extend(__result);
        }
    }
}
