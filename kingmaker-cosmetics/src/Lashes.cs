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
    // Female elf faces without their eyelashes: the lashes are flat cards in the head mesh, cut out of the face
    // texture, and look jagged up close. They are the small pieces of the mesh (joined by no triangle to the rest) in
    // front of the head; the head mesh is rebuilt without them, for every face using it. The heads keep their ids.
    static class Lashes
    {
        // A lash card has a few dozen triangles at most; the face halves have hundreds.
        const int Small = 30;
        // In front of the head bone's plane by more than this (mesh space, +z forward): by the eyes, not at the neck.
        const float Front = 0.03f;
        // At the eyes: above the mouth (the teeth are small pieces in front too) and off the middle of the face.
        const float EyesFrom = 1.55f, Aside = 0.02f;

        internal static void Build()
        {
            var elf = BlueprintRoot.Instance.Progression.CharacterRaces.FirstOrDefault(r => r != null && r.RaceId == Race.Elf);
            if (elf == null)
                return;
            var done = new HashSet<SkinnedMeshRenderer>();
            foreach (var link in elf.FemaleOptions.Heads)
            {
                if (link == null || string.IsNullOrEmpty(link.AssetId) || Styles.OwnIds.Contains(link.AssetId)
                    || !ResourcesLibrary.LibraryObject.ResourceNamesByAssetId.ContainsKey(link.AssetId))
                    continue;
                var ee = link.Load(true);
                if (ee == null)
                    continue;
                foreach (var part in ee.BodyParts.Where(p => p != null && p.Type == BodyPartType.Head && p.SkinnedRenderer != null))
                {
                    var renderer = part.SkinnedRenderer;
                    var mesh = renderer.sharedMesh;
                    if (mesh == null || !mesh.isReadable || done.Contains(renderer))
                        continue;
                    done.Add(renderer);
                    int before = mesh.triangles.Length / 3;
                    var without = Without(mesh);
                    if (without == null)
                        continue;
                    Styles.Keep(link.AssetId, ee);
                    renderer.sharedMesh = without;
                    Main.Log.Log($"{ee.name}: {mesh.name} without lashes, {before} -> {without.triangles.Length / 3} triangles");
                }
            }
        }

        // The mesh without the small pieces in front, only with the vertices still used (the doll takes the vertex
        // range its triangles span, and the uv and weights of all vertices); null when there are none to take off.
        static Mesh Without(Mesh source)
        {
            var vertices = source.vertices;
            var lists = Enumerable.Range(0, source.subMeshCount).Select(s => source.GetTriangles(s)).ToArray();
            var all = lists.SelectMany(l => l).ToArray();
            var root = Enumerable.Range(0, vertices.Length).ToArray();
            System.Func<int, int> find = x =>
            {
                while (root[x] != x)
                    x = root[x] = root[root[x]];
                return x;
            };
            for (int t = 0; t < all.Length; t += 3)
            {
                int a = find(all[t]);
                root[find(all[t + 1])] = a;
                root[find(all[t + 2])] = a;
            }
            var pieces = Enumerable.Range(0, all.Length / 3).GroupBy(t => find(all[3 * t])).ToList();
            var drop = new HashSet<int>();
            foreach (var piece in pieces)
            {
                if (piece.Count() > Small)
                    continue;
                var middle = piece.SelectMany(t => new[] { all[3 * t], all[3 * t + 1], all[3 * t + 2] }).Distinct()
                    .Aggregate(new Vector4(), (s, v) => s + new Vector4(vertices[v].x, vertices[v].y, vertices[v].z, 1));
                middle /= middle.w;
                if (middle.z > Front && middle.y > EyesFrom && Mathf.Abs(middle.x) > Aside)
                    drop.Add(piece.Key);
            }
            if (drop.Count == 0)
                return null;
            var used = new Dictionary<int, int>();
            var order = new List<int>();
            var kept = lists.Select(list =>
            {
                var k = new List<int>();
                for (int t = 0; t < list.Length; t += 3)
                {
                    if (drop.Contains(find(list[t])))
                        continue;
                    for (int c = 0; c < 3; c++)
                    {
                        int v = list[t + c], at;
                        if (!used.TryGetValue(v, out at))
                        {
                            used[v] = at = order.Count;
                            order.Add(v);
                        }
                        k.Add(at);
                    }
                }
                return k.ToArray();
            }).ToArray();
            int count = vertices.Length;
            var mesh = new Mesh { name = source.name + "_NoLashes", hideFlags = HideFlags.DontUnloadUnusedAsset };
            mesh.vertices = order.Select(v => vertices[v]).ToArray();
            var normals = source.normals;
            if (normals.Length == count)
                mesh.normals = order.Select(v => normals[v]).ToArray();
            var tangents = source.tangents;
            if (tangents.Length == count)
                mesh.tangents = order.Select(v => tangents[v]).ToArray();
            var colors = source.colors32;
            if (colors.Length == count)
                mesh.colors32 = order.Select(v => colors[v]).ToArray();
            for (int channel = 0; channel < 4; channel++)
            {
                var uvs = new List<Vector2>();
                source.GetUVs(channel, uvs);
                if (uvs.Count == count)
                    mesh.SetUVs(channel, order.Select(v => uvs[v]).ToList());
            }
            var weights = source.boneWeights;
            mesh.boneWeights = order.Select(v => weights[v]).ToArray();
            mesh.bindposes = source.bindposes;
            mesh.subMeshCount = kept.Length;
            for (int s = 0; s < kept.Length; s++)
                mesh.SetTriangles(kept[s], s);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
