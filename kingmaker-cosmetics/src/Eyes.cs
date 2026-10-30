using System;
using System.Collections.Generic;
using System.Linq;
using Kingmaker;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Root;
using Kingmaker.UI.SettingsUI;
using Kingmaker.Visual.CharacterSystem;
using UnityEngine;

namespace KingmakerCosmetics
{
    // Female elf faces with detailed eyes: their face textures (the grey one and the colour over it, 256 in the game)
    // are swapped for ones four times as large with the iris, pupil, highlight and lid line drawn anew (made offline
    // from the game's own, see sharp_eyes.py of the hair stand). The doll packs all head textures into one place of
    // its atlas, sized by the face texture, so the hair and brow overlays of 256 just stretch over it. With a smaller
    // character atlas in the settings the textures are made smaller too, or the atlas would shrink all the others.
    static class Eyes
    {
        // The textures of the faces: name in the game -> file the mod carries.
        static readonly string[] Names =
        {
            "HD_Head_Face01BW_F_EL", "HD_Head_Face01ColorOver_F_EL",
            "HD_Head_Face02BW_F_EL", "HD_Head_Face02ColorOver_F_EL",
            "HD_Head_Face03BW_F_EL", "HD_Head_Face03ColorOver_F_EL",
            "HD_Head_Face04BW_F_EL", "HD_Head_Face04ColorOver_F_EL",
            "HD_Head_Face05BW_F_EL", "HD_Head_Face05ColorOver_F_EL",
        };

        internal static void Build()
        {
            var elf = BlueprintRoot.Instance.Progression.CharacterRaces.FirstOrDefault(r => r != null && r.RaceId == Race.Elf);
            if (elf == null)
                return;
            var files = Names.ToDictionary(n => n, n => "Eyes." + n + ".png");
            var made = new Dictionary<string, Texture2D>();
            foreach (var link in elf.FemaleOptions.Heads)
            {
                if (link == null || string.IsNullOrEmpty(link.AssetId) || Styles.OwnIds.Contains(link.AssetId)
                    || !ResourcesLibrary.LibraryObject.ResourceNamesByAssetId.ContainsKey(link.AssetId))
                    continue;
                var ee = link.Load(true);
                if (ee == null)
                    continue;
                int swapped = SideBraid.Paint(ee, files, made, Fit);
                if (swapped == 0)
                    continue;
                Styles.Keep(link.AssetId, ee);
                Main.Log.Log($"{ee.name}: {swapped} face textures with detailed eyes, {made.Values.First().width} px");
            }
        }

        // The face textures as large as a quarter of the character atlas the settings give (2048 high, 1024 medium,
        // 512 low; medium or low texture quality halve or quarter it, as Character.MergeOverlays does), at most
        // their own size and at least the game's 256.
        internal static Texture2D Fit(Texture2D texture)
        {
            int atlas = 2048;
            try
            {
                var settings = Game.Instance?.SettingsManager?.GraphicsSettingsController;
                if (settings != null)
                {
                    switch (settings.CharacterAtlasResolution.CurrentValue)
                    {
                        case CharacterAtlasResolution.Medium: atlas = 1024; break;
                        case CharacterAtlasResolution.Low: atlas = 512; break;
                    }
                    switch (settings.Textures.CurrentValue)
                    {
                        case TextureQuality.Low: atlas = 512; break;
                        case TextureQuality.Medium: atlas = 1024; break;
                    }
                }
            }
            catch (Exception ex)
            {
                Main.Log.Log($"no graphics settings yet, character atlas taken as 2048: {ex.Message}");
            }
            int size = Mathf.Max(256, atlas / 2);
            while (texture.width > size)
                texture = Half(texture);
            return texture;
        }

        // Halved step by step, so every pixel counts (a bilinear blit straight to a quarter skips most of them).
        static Texture2D Half(Texture2D texture)
        {
            int w = texture.width / 2, h = texture.height / 2;
            var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var active = RenderTexture.active;
            Graphics.Blit(texture, rt);
            RenderTexture.active = rt;
            var half = new Texture2D(w, h, TextureFormat.RGBA32, true);
            half.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            half.Apply(true);
            RenderTexture.active = active;
            RenderTexture.ReleaseTemporary(rt);
            half.name = texture.name;
            half.hideFlags = texture.hideFlags;
            half.wrapMode = texture.wrapMode;
            half.filterMode = texture.filterMode;
            half.anisoLevel = texture.anisoLevel;
            UnityEngine.Object.Destroy(texture);
            return half;
        }
    }
}
