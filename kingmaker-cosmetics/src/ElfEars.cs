using System.Collections.Generic;
using System.Linq;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Classes;
using Kingmaker.Blueprints.Root;
using Kingmaker.ResourceLinks;
using Kingmaker.Visual.CharacterSystem;
using UnityEngine;

namespace KingmakerCosmetics
{
    // Elf ears, at times higher than the head, are made as long as halfway between elf and half-elf ears, and turned
    // a little away from the head: shortened, they lay against it. The heads keep their ids (saves do not change);
    // they are kept loaded for good, so the shorter ears are not loaded anew from their bundle.
    static class ElfEars
    {
        // Ear vertices this close to the head are where the ear grows from.
        const float Root = 0.006f;
        // How far the ear is turned out, in degrees, reached over the part of the ear nearest the head (Bend of its
        // length).
        const float Out = 15f, Bend = 0.3f;

        internal static void Build()
        {
            var races = BlueprintRoot.Instance.Progression.CharacterRaces.Where(r => r != null).ToList();
            var elf = races.FirstOrDefault(r => r.RaceId == Race.Elf);
            var halfElf = races.FirstOrDefault(r => r.RaceId == Race.HalfElf);
            if (elf == null || halfElf == null)
                return;
            var done = new HashSet<SkinnedMeshRenderer>();
            foreach (var female in new[] { false, true })
            {
                string gender = female ? "female" : "male";
                float half = (female ? halfElf.FemaleOptions : halfElf.MaleOptions).Heads
                    .Select(l => Length(Load(l))).FirstOrDefault(l => l > 0);
                if (half <= 0)
                {
                    Main.Log.Log($"no half-elf {gender} ears, elf ears left as they are");
                    continue;
                }
                foreach (var link in (female ? elf.FemaleOptions : elf.MaleOptions).Heads)
                {
                    var ee = Load(link);
                    if (ee == null || !Ears(ee).Any())
                        continue;
                    Styles.Keep(link.AssetId, ee);
                    // Faces sharing one ear mesh have it shortened once.
                    if (Ears(ee).Any(p => done.Contains(p.SkinnedRenderer)))
                        continue;
                    float own = Length(ee);
                    if (own <= half)
                        continue;
                    float wanted = (own + half) / 2;
                    foreach (var part in Ears(ee))
                    {
                        var head = HeadMesh(ee);
                        var renderer = part.SkinnedRenderer;
                        var mesh = UnityEngine.Object.Instantiate(renderer.sharedMesh);
                        mesh.name = renderer.sharedMesh.name + "_Shorter";
                        mesh.hideFlags = HideFlags.DontUnloadUnusedAsset;
                        var vertices = mesh.vertices;
                        var normals = mesh.normals;
                        foreach (var side in Sides(vertices))
                        {
                            Vector3 root, tip;
                            if (!Shape(vertices, side, head, out root, out tip))
                                continue;
                            var axis = (tip - root).normalized;
                            float k = wanted / own;
                            // Away from the head: the side of the mesh's x axis the ear is on.
                            var outward = new Vector3(Mathf.Sign(tip.x), 0, 0);
                            var turn = Quaternion.FromToRotation(axis, Vector3.RotateTowards(axis, outward, Out * Mathf.Deg2Rad, 0));
                            foreach (var v in side)
                            {
                                float along = Vector3.Dot(vertices[v] - root, axis);
                                if (along <= 0)
                                    continue;
                                var p = vertices[v] - axis * along * (1 - k);
                                var r = Quaternion.Slerp(Quaternion.identity, turn, Mathf.SmoothStep(0, 1, along * k / (wanted * Bend)));
                                vertices[v] = root + r * (p - root);
                                if (normals.Length == vertices.Length)
                                    normals[v] = r * normals[v];
                            }
                        }
                        mesh.vertices = vertices;
                        if (normals.Length == vertices.Length)
                            mesh.normals = normals;
                        mesh.RecalculateBounds();
                        renderer.sharedMesh = mesh;
                        done.Add(renderer);
                    }
                    Main.Log.Log($"{ee.name}: ears {Mathf.RoundToInt(own * 1000)} mm -> {Mathf.RoundToInt(wanted * 1000)} mm (half-elf {Mathf.RoundToInt(half * 1000)} mm), turned out {Out} degrees");
                }
            }
        }

        static EquipmentEntity Load(EquipmentEntityLink link)
        {
            if (link == null || string.IsNullOrEmpty(link.AssetId) || !ResourcesLibrary.LibraryObject.ResourceNamesByAssetId.ContainsKey(link.AssetId))
                return null;
            return link.Load(true);
        }

        static IEnumerable<BodyPart> Ears(EquipmentEntity ee)
        {
            return ee == null ? Enumerable.Empty<BodyPart>() : ee.BodyParts.Where(p => p != null && (p.Type & BodyPartType.Ears) != 0
                && p.SkinnedRenderer != null && p.SkinnedRenderer.sharedMesh != null && p.SkinnedRenderer.sharedMesh.isReadable);
        }

        static Vector3[] HeadMesh(EquipmentEntity ee)
        {
            var part = ee.BodyParts.FirstOrDefault(p => p != null && p.Type == BodyPartType.Head && p.SkinnedRenderer != null
                && p.SkinnedRenderer.sharedMesh != null && p.SkinnedRenderer.sharedMesh.isReadable);
            return part == null ? new Vector3[0] : part.SkinnedRenderer.sharedMesh.vertices;
        }

        // The left and the right ear: the sides of the mesh's x axis.
        static IEnumerable<List<int>> Sides(Vector3[] vertices)
        {
            yield return Enumerable.Range(0, vertices.Length).Where(v => vertices[v].x > 0).ToList();
            yield return Enumerable.Range(0, vertices.Length).Where(v => vertices[v].x <= 0).ToList();
        }

        // Where the ear grows from the head and its farthest point.
        static bool Shape(Vector3[] vertices, List<int> side, Vector3[] head, out Vector3 root, out Vector3 tip)
        {
            root = tip = Vector3.zero;
            if (side.Count < 3)
                return false;
            var near = side.Where(v => head.Any(h => (h - vertices[v]).sqrMagnitude < Root * Root)).ToList();
            if (near.Count == 0)
            {
                // No head mesh to tell by: the ear points nearest the middle of the head.
                float least = side.Min(v => Mathf.Abs(vertices[v].x));
                near = side.Where(v => Mathf.Abs(vertices[v].x) < least + Root).ToList();
            }
            var sum = Vector3.zero;
            foreach (var v in near)
                sum += vertices[v];
            var r = sum / near.Count;
            root = r;
            tip = vertices[side.OrderByDescending(v => (vertices[v] - r).sqrMagnitude).First()];
            return (tip - root).sqrMagnitude > 1e-6f;
        }

        // How far the ears reach from where they grow, both together on average.
        static float Length(EquipmentEntity ee)
        {
            if (ee == null)
                return 0;
            var head = HeadMesh(ee);
            var lengths = new List<float>();
            foreach (var part in Ears(ee))
            {
                var vertices = part.SkinnedRenderer.sharedMesh.vertices;
                foreach (var side in Sides(vertices))
                {
                    Vector3 root, tip;
                    if (Shape(vertices, side, head, out root, out tip))
                        lengths.Add((tip - root).magnitude);
                }
            }
            return lengths.Count == 0 ? 0 : lengths.Average();
        }
    }
}
