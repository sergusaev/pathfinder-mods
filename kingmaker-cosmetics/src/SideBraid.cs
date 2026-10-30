using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.CharGen;
using Kingmaker.Blueprints.Classes;
using Kingmaker.Blueprints.Root;
using Kingmaker.ResourceLinks;
using Kingmaker.Visual.CharacterSystem;
using UnityEngine;

namespace KingmakerCosmetics
{
    // A female elf look made after a portrait, from meshes and textures the mod carries (made offline from the game's
    // own, see assets/): long hair falling back with thin strands over the forehead and a braid from the parting
    // down past the ear, as a new hair style; and the first elf face with smaller eyes, eyeliner and its own brows,
    // as a new face with its brows. The hair is the long elf hair with its mesh and textures swapped, the face the
    // first elf face likewise: they keep the materials, bones and colours of the originals. The same face is made
    // for half-elf women on the first half-elf face: its skull, hoop, ears and neck stay, the elf face set in.
    static class SideBraid
    {
        // Asset ids. They go into DollData.EquipmentEntityIds of saves, so they never change.
        internal static readonly string HairId = Styles.MadeId("EE_HairLong_F_EL", "side braid");
        internal static readonly string FaceId = Styles.MadeId("EE_Head_Face01_F_EL", "eyeliner face");
        internal static readonly string BrowsId = Styles.MadeId("EE_Eyebrows_Face01_F_EL", "eyeliner face brows");
        internal static readonly string HalfElfFaceId = Styles.MadeId("EE_Head_Face01_F_HE", "eyeliner face");
        internal static readonly string HalfElfBrowsId = Styles.MadeId("EE_Eyebrows_Face01_F_HE", "eyeliner face brows");

        internal static void Build()
        {
            var elf = BlueprintRoot.Instance.Progression.CharacterRaces.FirstOrDefault(r => r != null && r.RaceId == Race.Elf);
            if (elf == null)
            {
                Main.Log.Error("ElfRace not found, no side braid");
                return;
            }
            try
            {
                Hair(elf.FemaleOptions);
            }
            catch (Exception ex)
            {
                Main.Log.Error($"side braid not made: {ex}");
            }
            try
            {
                Face(elf.FemaleOptions, "EL", "LinerFace", FaceId, BrowsId);
            }
            catch (Exception ex)
            {
                Main.Log.Error($"eyeliner face not made: {ex}");
            }
            var halfElf = BlueprintRoot.Instance.Progression.CharacterRaces.FirstOrDefault(r => r != null && r.RaceId == Race.HalfElf);
            if (halfElf == null)
            {
                Main.Log.Error("HalfElfRace not found, no half-elf eyeliner face");
                return;
            }
            try
            {
                Face(halfElf.FemaleOptions, "HE", "LinerFaceHE", HalfElfFaceId, HalfElfBrowsId);
            }
            catch (Exception ex)
            {
                Main.Log.Error($"half-elf eyeliner face not made: {ex}");
            }
        }

        // EE_HairLong_F_EL: not among the elf styles of character creation, so it is taken by its asset id.
        const string LongHairId = "a402292bdc74f8c4887f15d76d0378dd";

        static void Hair(CustomizationOptions options)
        {
            var link = Link(options.Hair, "EE_HairLong_F_EL")
                ?? (ResourcesLibrary.LibraryObject.ResourceNamesByAssetId.ContainsKey(LongHairId) ? new EquipmentEntityLink { AssetId = LongHairId } : null);
            var original = link == null ? null : link.Load(true);
            if (original == null)
            {
                Main.Log.Error("no long elf hair, no side braid");
                return;
            }
            var copy = Styles.Copy(link.AssetId, original, "_SideBraid");
            if (!Swap(copy, original, "SideBraid.mesh", m => m.name.StartsWith("H1_HairLong", StringComparison.OrdinalIgnoreCase)))
                return;
            Paint(copy, new Dictionary<string, string>
            {
                { "H1_HairLong_White_F_EL", "SideBraid.png" },
                { "HD_Head_HairLongWhite_F_EL", "SideBraidGrowth.png" },
            });
            Styles.Register(HairId, copy);
            options.Hair = options.Hair.Concat(new[] { new EquipmentEntityLink { AssetId = HairId } }).ToArray();
            Main.Log.Log($"{copy.name}: female elf hair style {options.Hair.Length}");
        }

        // The face on the first female face of the race (EL, HE), from the files <files>.mesh, <files>BW.png,
        // <files>ColorOver.png and the brows <brows>.png (LinerFace: LinerBrows).
        static void Face(CustomizationOptions options, string race, string files, string faceId, string browsId)
        {
            string brows = files.Replace("LinerFace", "LinerBrows");
            var link = Link(options.Heads, "EE_Head_Face01_F_" + race);
            int at = link == null ? -1 : Array.IndexOf(options.Heads, link);
            var browsLink = at >= 0 && at < options.Eyebrows.Length ? options.Eyebrows[at] : null;
            var original = link == null ? null : link.Load(true);
            var browsEntity = browsLink == null ? null : browsLink.Load(true);
            if (original == null || browsEntity == null || options.Eyebrows.Length != options.Heads.Length)
            {
                Main.Log.Error($"no first {race} face with its brows, no eyeliner face");
                return;
            }
            var copy = Styles.Copy(link.AssetId, original, "_Eyeliner");
            if (!Swap(copy, original, files + ".mesh", m => m.name.StartsWith("HD_Head", StringComparison.OrdinalIgnoreCase)))
                return;
            Paint(copy, new Dictionary<string, string>
            {
                { "HD_Head_Face01BW_F_" + race, files + "BW.png" },
                { "HD_Head_Face01ColorOver_F_" + race, files + "ColorOver.png" },
            }, Eyes.Fit);
            var browsCopy = Styles.Copy(browsLink.AssetId, browsEntity, "_Eyeliner");
            Paint(browsCopy, new Dictionary<string, string> { { "HD_Head_Face01EyebrowsBW_F_" + race, brows + ".png" } });
            Styles.Register(faceId, copy);
            Styles.Register(browsId, browsCopy);
            // Heads and brows go in pairs, one index for both.
            options.Heads = options.Heads.Concat(new[] { new EquipmentEntityLink { AssetId = faceId } }).ToArray();
            options.Eyebrows = options.Eyebrows.Concat(new[] { new EquipmentEntityLink { AssetId = browsId } }).ToArray();
            Main.Log.Log($"{copy.name}: female {race} face {options.Heads.Length}");
        }

        static EquipmentEntityLink Link(EquipmentEntityLink[] links, string name)
        {
            string found;
            return links.FirstOrDefault(l => l != null && l.AssetId != null
                && ResourcesLibrary.LibraryObject.ResourceNamesByAssetId.TryGetValue(l.AssetId, out found) && found == name);
        }

        // The parts of the copy on the original's mesh picked get the mesh of the file instead.
        static bool Swap(EquipmentEntity copy, EquipmentEntity original, string file, Func<Mesh, bool> which)
        {
            Mesh mesh = null;
            int swapped = 0;
            for (int i = 0; i < original.BodyParts.Count; i++)
            {
                var part = original.BodyParts[i];
                var source = part == null || part.SkinnedRenderer == null ? null : part.SkinnedRenderer.sharedMesh;
                if (source == null || !which(source))
                    continue;
                if (mesh == null && (mesh = ReadMesh(file, source)) == null)
                    return false;
                Styles.ReplaceMesh(copy, i, part, swapped == 0 ? mesh : UnityEngine.Object.Instantiate(mesh));
                swapped++;
            }
            if (swapped == 0)
                Main.Log.Error($"{copy.name}: no mesh to swap for {file}");
            return swapped > 0;
        }

        // The textures of the copy named are swapped for those of the files; their colour masks stay.
        static void Paint(EquipmentEntity copy, Dictionary<string, string> files, Func<Texture2D, Texture2D> fit = null)
        {
            var made = new Dictionary<string, Texture2D>();
            Paint(copy, files, made, fit);
            foreach (var name in files.Keys.Where(n => !made.ContainsKey(n)))
                Main.Log.Error($"{copy.name}: no texture {name} to swap");
        }

        // The same for any entity, with the textures read so far (by name in the game) and what to do with each read;
        // the number of textures swapped.
        internal static int Paint(EquipmentEntity ee, Dictionary<string, string> files, Dictionary<string, Texture2D> made, Func<Texture2D, Texture2D> fit)
        {
            int swapped = 0;
            foreach (var part in ee.BodyParts.Where(p => p != null && p.Textures != null))
                part.Textures = part.Textures.Select(d =>
                {
                    string name = d == null ? null : d.GetMainTextureName(), file;
                    if (name == null || !files.TryGetValue(name, out file))
                        return d;
                    Texture2D texture;
                    if (!made.TryGetValue(name, out texture))
                    {
                        texture = ReadTexture(file, d.GetSourceTexture() as Texture2D);
                        made[name] = texture = fit == null ? texture : fit(texture);
                    }
                    var replaced = new CharacterTextureDescription(d.Channel, texture);
                    replaced.GetRampMaskDescriptions().AddRange(d.GetRampMaskDescriptions());
                    swapped++;
                    return replaced;
                }).ToList();
            return swapped;
        }

        static byte[] Resource(string file)
        {
            using (var stream = typeof(SideBraid).Assembly.GetManifestResourceStream("KingmakerCosmetics." + file))
            using (var memory = new MemoryStream())
            {
                stream.CopyTo(memory);
                return memory.ToArray();
            }
        }

        static Texture2D ReadTexture(string file, Texture2D like)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, true);
            texture.LoadImage(Resource(file));
            texture.name = (like != null ? like.name : "") + "_" + Path.GetFileNameWithoutExtension(file);
            texture.hideFlags = HideFlags.DontUnloadUnusedAsset;
            if (like != null)
            {
                texture.wrapMode = like.wrapMode;
                texture.filterMode = like.filterMode;
                texture.anisoLevel = like.anisoLevel;
            }
            return texture;
        }

        // A mesh of the mod's file format (written by export_mod.py of the hair stand): positions, normals, uv, four
        // bone weights each on the bones of the game mesh it was made from, triangles. The file names the first
        // vertices of that mesh as the stand saw them; the game mesh tells whether x or v are to be mirrored.
        static Mesh ReadMesh(string file, Mesh source)
        {
            using (var reader = new BinaryReader(new MemoryStream(Resource(file))))
            {
                if (new string(reader.ReadChars(4)) != "KMCM" || reader.ReadInt32() != 1)
                {
                    Main.Log.Error($"{file}: not a mesh of this mod");
                    return null;
                }
                int refs = reader.ReadInt32();
                var refPoints = new Vector3[refs];
                var refUv = new Vector2[refs];
                for (int i = 0; i < refs; i++)
                {
                    refPoints[i] = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                    refUv[i] = new Vector2(reader.ReadSingle(), reader.ReadSingle());
                }
                bool mirror = false, flipV = false;
                if (source.isReadable)
                {
                    var points = source.vertices;
                    var uv = source.uv;
                    Func<Func<int, bool>, bool> all = test => refs <= points.Length && refs <= uv.Length && Enumerable.Range(0, refs).All(test);
                    if (all(i => (points[i] - refPoints[i]).sqrMagnitude < 1e-8f)) { }
                    else if (all(i => (points[i] - Vector3.Scale(refPoints[i], new Vector3(-1, 1, 1))).sqrMagnitude < 1e-8f))
                        mirror = true;
                    else
                    {
                        Main.Log.Error($"{file}: made from another mesh than {source.name} ({(points.Length > 0 ? points[0].ToString("F4") : "empty")} for {refPoints[0]:F4})");
                        return null;
                    }
                    flipV = refs > 0 && refs <= uv.Length && (uv[0] - refUv[0]).sqrMagnitude > 1e-8f
                        && (uv[0] - new Vector2(refUv[0].x, 1 - refUv[0].y)).sqrMagnitude < 1e-8f;
                }
                else
                    Main.Log.Log($"{file}: {source.name} not readable, taken as the stand saw it");
                int n = reader.ReadInt32();
                var vertices = new Vector3[n];
                var normals = new Vector3[n];
                var uvs = new Vector2[n];
                var weights = new BoneWeight[n];
                var sign = new Vector3(mirror ? -1 : 1, 1, 1);
                for (int i = 0; i < n; i++)
                    vertices[i] = Vector3.Scale(new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()), sign);
                for (int i = 0; i < n; i++)
                    normals[i] = Vector3.Scale(new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()), sign);
                for (int i = 0; i < n; i++)
                {
                    float u = reader.ReadSingle(), v = reader.ReadSingle();
                    uvs[i] = new Vector2(u, flipV ? 1 - v : v);
                }
                for (int i = 0; i < n; i++)
                {
                    weights[i].boneIndex0 = reader.ReadUInt16();
                    weights[i].boneIndex1 = reader.ReadUInt16();
                    weights[i].boneIndex2 = reader.ReadUInt16();
                    weights[i].boneIndex3 = reader.ReadUInt16();
                }
                for (int i = 0; i < n; i++)
                {
                    weights[i].weight0 = reader.ReadSingle();
                    weights[i].weight1 = reader.ReadSingle();
                    weights[i].weight2 = reader.ReadSingle();
                    weights[i].weight3 = reader.ReadSingle();
                }
                int m = reader.ReadInt32();
                var triangles = new int[m * 3];
                for (int i = 0; i < m; i++)
                {
                    int a = reader.ReadInt32(), b = reader.ReadInt32(), c = reader.ReadInt32();
                    // Mirrored, a triangle would face the other way.
                    triangles[3 * i] = a;
                    triangles[3 * i + 1] = mirror ? c : b;
                    triangles[3 * i + 2] = mirror ? b : c;
                }
                int bones = source.bindposes.Length;
                if (weights.Any(w => w.boneIndex0 >= bones || w.boneIndex1 >= bones || w.boneIndex2 >= bones || w.boneIndex3 >= bones))
                {
                    Main.Log.Error($"{file}: bones beyond the {bones} of {source.name}");
                    return null;
                }
                var mesh = new Mesh();
                mesh.vertices = vertices;
                mesh.normals = normals;
                mesh.uv = uvs;
                mesh.boneWeights = weights;
                mesh.bindposes = source.bindposes;
                mesh.triangles = triangles;
                if (source.isReadable && source.tangents.Length > 0)
                    mesh.RecalculateTangents();
                mesh.RecalculateBounds();
                Main.Log.Log($"{file}: {n} vertices, {m} triangles on the bones of {source.name}{(mirror ? ", x mirrored" : "")}{(flipV ? ", v flipped" : "")}");
                return mesh;
            }
        }
    }
}
