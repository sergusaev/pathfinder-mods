using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Kingmaker.Visual.CharacterSystem;
using UnityEngine;

namespace KingmakerCosmetics
{
    // Face paint lies under all hair. The hair drawn on the head (the hairline of a style, the brows) is painted by the
    // game into the head's texture, while the paint is a mesh of its own over the skin with a texture of its own, so
    // the game would draw the paint over that hair. Here every time the doll merges its textures or repaints one of
    // them, the hair painting on the head of the entities above the paint is laid over the paint's texture as the game
    // lays it over the skin: the paint shows hair wherever the skin under it would.
    static class UnderHair
    {
        // The paint entities (Decorations).
        internal static readonly HashSet<EquipmentEntity> Paints = new HashSet<EquipmentEntity>();

        const BodyPartType HeadTypes = BodyPartType.Head | BodyPartType.HeadTop | BodyPartType.HoopHead;

        // Per paint: what lies over it on the last doll that wore it, and the colours it was last painted with.
        static readonly Dictionary<EquipmentEntity, List<KeyValuePair<EquipmentEntity, CharacterTextureDescription>>> s_Covers =
            new Dictionary<EquipmentEntity, List<KeyValuePair<EquipmentEntity, CharacterTextureDescription>>>();
        static readonly Dictionary<EquipmentEntity, KeyValuePair<int, int>> s_Colours = new Dictionary<EquipmentEntity, KeyValuePair<int, int>>();
        static bool s_Busy;
        static string s_Logged;

        // After the doll merged its textures (the paint and the hair repainted just before): what covers the paint.
        internal static void Merged(Character character)
        {
            var paint = character.EquipmentEntities.FirstOrDefault(e => e != null && Paints.Contains(e));
            if (paint == null)
                return;
            var covers = character.EquipmentEntities
                .Where(e => e != null && e != paint && e.Layer > paint.Layer && e.BodyParts != null)
                .OrderBy(e => e.Layer)
                .SelectMany(e => e.BodyParts
                    .Where(p => p != null && p.RendererPrefab == null && (p.Type & HeadTypes) != 0 && p.Textures != null)
                    .Select(p => p.Textures.FirstOrDefault(t => t != null && t.Channel == CharacterTextureChannel.Diffuse && t.GetSourceTexture() != null))
                    .Where(t => t != null)
                    .Select(t => new KeyValuePair<EquipmentEntity, CharacterTextureDescription>(e, t)))
                .ToList();
            s_Covers[paint] = covers;
            string state = $"{paint.name} under {string.Join(", ", covers.Select(c => c.Value.GetMainTextureName()).ToArray())}";
            if (state != s_Logged)
            {
                s_Logged = state;
                Main.Log.Log("paint under hair: " + state);
            }
            // Painted afresh and covered (painted before with the covers of the last doll).
            KeyValuePair<int, int> colours;
            if (s_Colours.TryGetValue(paint, out colours))
                paint.RepaintTextures(colours.Key, colours.Value);
            else
                Compose(paint);
            character.IsAtlasesDirty = true;
        }

        // After an entity repainted its textures: the paint itself, or hair lying over a paint.
        internal static void Repainted(EquipmentEntity ee, int primary, int secondary)
        {
            if (s_Busy || ee == null)
                return;
            if (Paints.Contains(ee))
            {
                s_Colours[ee] = new KeyValuePair<int, int>(primary, secondary);
                Compose(ee);
                return;
            }
            foreach (var pair in s_Covers.Where(p => p.Value.Any(c => c.Key == ee)).ToList())
            {
                KeyValuePair<int, int> colours;
                if (!s_Colours.TryGetValue(pair.Key, out colours))
                    continue;
                // The paint's own colour again, the hair over it in its new one.
                pair.Key.RepaintTextures(colours.Key, colours.Value);
            }
        }

        static void Compose(EquipmentEntity paint)
        {
            List<KeyValuePair<EquipmentEntity, CharacterTextureDescription>> covers;
            if (!s_Covers.TryGetValue(paint, out covers) || covers.Count == 0)
                return;
            var description = paint.BodyParts.Where(p => p != null && p.Textures != null).SelectMany(p => p.Textures)
                .FirstOrDefault(t => t != null && t.Channel == CharacterTextureChannel.Diffuse);
            var target = description == null ? null : description.GetActualTexture() as RenderTexture;
            if (target == null)
                return;
            s_Busy = true;
            var active = RenderTexture.active;
            var read = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
            var temp = RenderTexture.GetTemporary(target.descriptor);
            try
            {
                var colours = Read(target, read);
                foreach (var cover in covers)
                {
                    var source = cover.Value.GetActualTexture();
                    if (source == null)
                        continue;
                    Graphics.Blit(source, temp);
                    var over = Read(temp, read);
                    for (int i = 0; i < colours.Length; i++)
                    {
                        int a = over[i].a;
                        if (a == 0)
                            continue;
                        var c = colours[i];
                        colours[i] = new Color32((byte)((c.r * (255 - a) + over[i].r * a) / 255), (byte)((c.g * (255 - a) + over[i].g * a) / 255),
                            (byte)((c.b * (255 - a) + over[i].b * a) / 255), c.a);
                    }
                }
                read.SetPixels32(colours);
                read.Apply(false);
                Graphics.Blit(read, target);
            }
            catch (Exception ex)
            {
                Main.Log.Error($"paint under hair: {ex}");
            }
            finally
            {
                RenderTexture.active = active;
                RenderTexture.ReleaseTemporary(temp);
                UnityEngine.Object.Destroy(read);
                s_Busy = false;
            }
        }

        static Color32[] Read(RenderTexture texture, Texture2D into)
        {
            RenderTexture.active = texture;
            into.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0, false);
            into.Apply(false);
            return into.GetPixels32();
        }
    }

    [HarmonyPatch(typeof(Character), "MergeOverlays")]
    static class UnderHairMerged
    {
        static void Postfix(Character __instance)
        {
            try
            {
                UnderHair.Merged(__instance);
            }
            catch (Exception ex)
            {
                Main.Log.Error($"paint under hair: {ex}");
            }
        }
    }

    [HarmonyPatch(typeof(EquipmentEntity), nameof(EquipmentEntity.RepaintTextures))]
    static class UnderHairRepainted
    {
        static void Postfix(EquipmentEntity __instance, int primaryRampIndex, int secondaryRampIndex)
        {
            try
            {
                UnderHair.Repainted(__instance, primaryRampIndex, secondaryRampIndex);
            }
            catch (Exception ex)
            {
                Main.Log.Error($"paint under hair: {ex}");
            }
        }
    }
}
