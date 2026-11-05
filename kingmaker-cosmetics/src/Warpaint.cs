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
    // A ready head of a human woman after a portrait (made offline on the hair stand, hm_eye.py, hm_warpaint.py,
    // hm_decal_mod.py): the fourth human face with detailed eyes (iris, pupil, white, a shadow on the upper lid) and a
    // line of lashes along the upper lid drawn into her brows (so it takes the colour of the brows, that is of the
    // hair). The war paint once part of it is a face decoration of its own now (Decorations.cs).
    // The head's texture is shared by both halves of the face, so the paint is a mesh of its own: the head triangles
    // of the right side cut along the paint's outline and lifted 0.4 mm, a body part of type Glasses on the head's
    // bones (the doll gives each body part type its own place in the atlas), its grey shade repainted by a palette.
    static class Warpaint
    {
        // Asset ids: they go into saves and never change.
        internal static readonly string FaceId = Styles.MadeId("EE_Head_Face04_F_HM", "warpaint face");
        internal static readonly string BrowsId = Styles.MadeId("EE_Eyebrows_Face04_F_HM", "warpaint face brows");

        internal static void Build()
        {
            var human = BlueprintRoot.Instance.Progression.CharacterRaces.FirstOrDefault(r => r != null && r.RaceId == Race.Human);
            if (human == null)
            {
                Main.Log.Error("HumanRace not found, no war paint face");
                return;
            }
            try
            {
                Face(human.FemaleOptions);
            }
            catch (Exception ex)
            {
                Main.Log.Error($"war paint face not made: {ex}");
            }
        }

        static void Face(CustomizationOptions options)
        {
            var link = SideBraid.Link(options.Heads, "EE_Head_Face04_F_HM");
            int at = link == null ? -1 : Array.IndexOf(options.Heads, link);
            var browsLink = at >= 0 && at < options.Eyebrows.Length ? options.Eyebrows[at] : null;
            var original = link == null ? null : link.Load(true);
            var browsEntity = browsLink == null ? null : browsLink.Load(true);
            if (original == null || browsEntity == null || options.Eyebrows.Length != options.Heads.Length)
            {
                Main.Log.Error("no fourth human face with its brows, no war paint face");
                return;
            }
            int hd = original.BodyParts.FindIndex(p => p != null && p.SkinnedRenderer != null && p.SkinnedRenderer.sharedMesh != null
                && p.SkinnedRenderer.sharedMesh.name.StartsWith("HD_", StringComparison.OrdinalIgnoreCase));
            if (hd < 0)
            {
                Main.Log.Error($"{original.name}: no face mesh, no war paint face");
                return;
            }
            var face = original.BodyParts[hd];
            var mesh = SideBraid.ReadMesh("Warpaint.mesh", face.SkinnedRenderer.sharedMesh);
            if (mesh == null)
                return;
            var copy = Styles.Copy(link.AssetId, original, "_Warpaint");
            var made = new Dictionary<string, Texture2D>();
            var files = new Dictionary<string, string>
            {
                { "HD_Head_Face04BW_F_HM", "WarpaintFaceBW.png" },
                { "HD_Head_Face04ColorOver_F_HM", "WarpaintFaceColorOver.png" },
            };
            if (SideBraid.Paint(copy, files, made, Eyes.Fit) != 2)
                Main.Log.Error($"{copy.name}: face textures swapped: {string.Join(", ", made.Keys.ToArray())}");
            Decorations.Build(link.AssetId, original, face, mesh);
            var browsCopy = Styles.Copy(browsLink.AssetId, browsEntity, "_Warpaint");
            var browsMade = new Dictionary<string, Texture2D>();
            if (SideBraid.Paint(browsCopy, new Dictionary<string, string> { { "HD_Head_Face04EyebrowsBW_F_HM", "WarpaintBrows.png" } }, browsMade, Eyes.Fit) != 1)
                Main.Log.Error($"{browsCopy.name}: brows with lashes not swapped");
            Styles.Register(FaceId, copy);
            Styles.Register(BrowsId, browsCopy);
            // Heads and brows go in pairs, one index for both.
            options.Heads = options.Heads.Concat(new[] { new EquipmentEntityLink { AssetId = FaceId } }).ToArray();
            options.Eyebrows = options.Eyebrows.Concat(new[] { new EquipmentEntityLink { AssetId = BrowsId } }).ToArray();
            Main.Log.Log($"{copy.name}: female human face {options.Heads.Length}");
            Decorations.Face04Ids.Add(link.AssetId);
            Decorations.Face04Ids.Add(FaceId);
            Decorations.Offer(options);
        }
    }
}
