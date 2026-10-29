using System;
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
    // Elf ponytails on human hair, as new female human styles: the bob with the tail of the elf bob with a ponytail,
    // and the human high ponytail with its tail and band swapped for the lush elf tail. The tail is a part of its own
    // with the elf textures; elf and human hair have the same bones and bind poses. Pieces of a mesh are its parts
    // not joined by any triangle; those whose middle is behind the head bone by more than Behind are the tail.
    static class ElfStyles
    {
        // Asset ids. They go into DollData.EquipmentEntityIds of saves, so they never change.
        internal static readonly string KareId = Styles.MadeId(Bob.Id, "elf kare ponytail");
        internal static readonly string LushId = Styles.MadeId("EE_Hair_PonyTailUpper_F_HM", "elf lush ponytail");

        const float Behind = 0.025f;
        // Vertices this close are one point when telling the pieces apart.
        const float Same = 0.0001f;
        // The tail put onto the bob stands off it by at least Gap where the elf one stood off the elf bob less.
        const float Gap = 0.004f;

        internal static void Build()
        {
            var races = BlueprintRoot.Instance.Progression.CharacterRaces.Where(r => r != null).ToList();
            var human = races.FirstOrDefault(r => r.RaceId == Race.Human);
            var elf = races.FirstOrDefault(r => r.RaceId == Race.Elf);
            if (human == null || elf == null)
            {
                Main.Log.Error("HumanRace or ElfRace not found, no elf ponytails");
                return;
            }
            var made = new List<string>();
            try
            {
                if (Kare(elf))
                    made.Add(KareId);
            }
            catch (Exception ex)
            {
                Main.Log.Error($"bob with an elf ponytail not made: {ex}");
            }
            try
            {
                if (Lush(human, elf))
                    made.Add(LushId);
            }
            catch (Exception ex)
            {
                Main.Log.Error($"lush ponytail not made: {ex}");
            }
            var hair = human.FemaleOptions.Hair.ToList();
            hair.AddRange(made.Select(id => new EquipmentEntityLink { AssetId = id }));
            human.FemaleOptions.Hair = hair.ToArray();
        }

        // The bob with the tail of the elf bob with a ponytail, moved from the elf bob onto it.
        static bool Kare(BlueprintRace elf)
        {
            var bob = ResourcesLibrary.TryGetResource<EquipmentEntity>(Bob.Id, true);
            var bobPart = bob == null ? null : SharedHair.HairPart(Bob.Id);
            EquipmentEntity source;
            int index;
            var elfPart = Find(elf, "EE_Hair_KarePonyTail_F_EL", out source, out index);
            if (bobPart == null || elfPart == null)
            {
                Main.Log.Error("no bob or elf bob with a ponytail, no bob with an elf ponytail");
                return false;
            }
            var to = new Hair(bobPart.SkinnedRenderer);
            var from = new Hair(elfPart.SkinnedRenderer);
            if (!to.Ready || !from.Ready || !to.SameBones(from))
                return false;
            var tail = from.Tail();
            var points = tail.SelectMany(from.Corners).Distinct().ToArray();
            var near = points.OrderBy(v => from.Points[v].y).Take(10).ToArray();
            var anchor = near.Aggregate(Vector3.zero, (s, v) => s + from.Points[v]) / near.Length;
            var way = anchor.normalized;
            // The tail goes as far out as the bob is thicker than the elf bob where it is tied, then farther out until
            // its upper half is no deeper in the bob than it was in the elf hair.
            var elfBase = from.Triangles(t => !tail.Contains(t));
            var all = to.Triangles(t => true);
            var shift = way * ((Ray(way, to.Points, all) ?? 0) - (Ray(way, from.Points, elfBase) ?? 0));
            float middle = Median(points.Select(v => from.Points[v].x + shift.x));
            float need = 0;
            int n = 0;
            foreach (var v in points)
            {
                var p = from.Points[v] + shift;
                if (p.x >= middle || n++ % 2 != 0)
                    continue;
                float r = p.magnitude;
                var d = p / r;
                float? r0 = Ray(d, from.Points, elfBase), r1 = Ray(d, to.Points, all);
                if (!r1.HasValue)
                    continue;
                float want = r1.Value + Mathf.Max(Gap, r0.HasValue ? r - r0.Value : Gap);
                if (r < want)
                    need = Mathf.Max(need, (want - r) / Mathf.Max(Vector3.Dot(d, way), 0.3f));
            }
            var offset = shift + way * need;
            var copy = Styles.Copy(Bob.Id, bob, "_ElfPonyTail");
            AddPart(copy, source, index, from.Write(offset, tail));
            Styles.Keep(LinkOf(elf, "EE_Hair_KarePonyTail_F_EL").AssetId, source);
            Styles.Register(KareId, copy);
            Main.Log.Log($"{copy.name}: elf tail of {tail.Count} triangles moved {Mm(shift)} onto the bob and {Mathf.RoundToInt(need * 1000)} mm out");
            return true;
        }

        // The human high ponytail without its tail and band, with the lush elf tail tied where its band was.
        static bool Lush(BlueprintRace human, BlueprintRace elf)
        {
            EquipmentEntity source, original;
            int index, own;
            var elfPart = Find(elf, "EE_Hair_PonyTailLush_F_EL", out source, out index);
            var part = Find(human, "EE_Hair_PonyTailUpper_F_HM", out original, out own);
            if (part == null || elfPart == null)
            {
                Main.Log.Error("no human high ponytail or lush elf ponytail, no lush ponytail");
                return false;
            }
            var to = new Hair(part.SkinnedRenderer);
            var from = new Hair(elfPart.SkinnedRenderer);
            if (!to.Ready || !from.Ready || !to.SameBones(from))
                return false;
            // The human band: the smallest piece of its tail; the elf band: the piece of the elf tail nearest it.
            var ownTail = to.Tail();
            var band = to.Pieces(ownTail).OrderBy(p => p.Count).First();
            var bandMiddle = to.Middle(band);
            var tail = from.Tail();
            var elfBand = from.Pieces(tail).OrderBy(p => (from.Middle(p) - bandMiddle).sqrMagnitude).First();
            var offset = bandMiddle - from.Middle(elfBand);
            var copy = Styles.Copy(LinkOf(human, "EE_Hair_PonyTailUpper_F_HM").AssetId, original, "_ElfPonyTail");
            var cap = to.Write(Vector3.zero, new HashSet<int>(Enumerable.Range(0, to.Count).Where(t => !ownTail.Contains(t))));
            foreach (int i in Twins(original, own))
                Styles.ReplaceMesh(copy, i, original.BodyParts[i], i == own ? cap : UnityEngine.Object.Instantiate(cap));
            AddPart(copy, source, index, from.Write(offset, tail));
            Styles.Keep(LinkOf(elf, "EE_Hair_PonyTailLush_F_EL").AssetId, source);
            Styles.Register(LushId, copy);
            Main.Log.Log($"{copy.name}: own tail of {ownTail.Count} triangles taken off, elf tail of {tail.Count} triangles moved {Mm(offset)}");
            return true;
        }

        static EquipmentEntityLink LinkOf(BlueprintRace race, string name)
        {
            string found;
            return race.FemaleOptions.Hair.FirstOrDefault(l => l != null && l.AssetId != null
                && ResourcesLibrary.LibraryObject.ResourceNamesByAssetId.TryGetValue(l.AssetId, out found) && found == name);
        }

        // The hair part of a female style of the race, by the entity's name.
        static BodyPart Find(BlueprintRace race, string name, out EquipmentEntity ee, out int index)
        {
            ee = null;
            index = -1;
            var link = LinkOf(race, name);
            ee = link == null ? null : link.Load(true);
            if (ee == null)
                return null;
            for (int i = 0; i < ee.BodyParts.Count; i++)
            {
                var part = ee.BodyParts[i];
                if (part != null && (part.Type & BodyPartType.Hair) != 0 && part.SkinnedRenderer != null && part.SkinnedRenderer.sharedMesh != null)
                {
                    index = i;
                    return part;
                }
            }
            return null;
        }

        // The parts of an entity with the same mesh as its part at index: hair has two parts on one mesh.
        static List<int> Twins(EquipmentEntity ee, int index)
        {
            var mesh = ee.BodyParts[index].SkinnedRenderer.sharedMesh;
            return Enumerable.Range(0, ee.BodyParts.Count).Where(i => ee.BodyParts[i] != null && ee.BodyParts[i].SkinnedRenderer != null
                && ee.BodyParts[i].SkinnedRenderer.sharedMesh == mesh).ToList();
        }

        // The elf parts go after the copy's own, with their materials and textures and the mesh given. A doll keeps
        // one part of each type, the last: the tail is the second hair (Hair2), or it would take the place of the hair.
        static void AddPart(EquipmentEntity copy, EquipmentEntity source, int index, Mesh mesh)
        {
            foreach (int i in Twins(source, index))
            {
                var part = source.BodyParts[i];
                copy.BodyParts.Add(new BodyPart
                {
                    Type = BodyPartType.Hair2,
                    RendererPrefab = part.RendererPrefab,
                    Material = part.Material,
                    Textures = part.Textures == null ? null : part.Textures.ToList(),
                });
                Styles.ReplaceMesh(copy, copy.BodyParts.Count - 1, part, i == index ? mesh : UnityEngine.Object.Instantiate(mesh));
            }
        }

        static string Mm(Vector3 v)
        {
            return $"({Mathf.RoundToInt(v.x * 1000)}, {Mathf.RoundToInt(v.y * 1000)}, {Mathf.RoundToInt(v.z * 1000)}) mm";
        }

        static float Median(IEnumerable<float> values)
        {
            var sorted = values.OrderBy(x => x).ToArray();
            return sorted[sorted.Length / 2];
        }

        // How far from the head bone a ray from it along d leaves the triangles (the last hit), or null.
        static float? Ray(Vector3 d, Vector3[] points, List<int[]> triangles)
        {
            float? best = null;
            foreach (var t in triangles)
            {
                Vector3 a = points[t[0]], e1 = points[t[1]] - a, e2 = points[t[2]] - a;
                var p = Vector3.Cross(d, e2);
                float det = Vector3.Dot(e1, p);
                if (Mathf.Abs(det) < 1e-12f)
                    continue;
                var s = -a;
                float u = Vector3.Dot(s, p) / det;
                var q = Vector3.Cross(s, e1);
                float w = Vector3.Dot(d, q) / det, along = Vector3.Dot(e2, q) / det;
                if (u >= 0 && w >= 0 && u + w <= 1 && along > 0 && (!best.HasValue || along > best.Value))
                    best = along;
            }
            return best;
        }

        // A hair mesh in the space of the head bone: x down, y back from the head.
        class Hair
        {
            readonly SkinnedMeshRenderer m_Renderer;
            readonly Matrix4x4 m_Bind;
            internal readonly Vector3[] Points;
            // Triangles by submesh, and the piece of each triangle counted over all submeshes.
            readonly int[][] m_Submeshes;
            readonly int[] m_Pieces;
            internal readonly bool Ready;

            internal Hair(SkinnedMeshRenderer renderer)
            {
                m_Renderer = renderer;
                var mesh = renderer.sharedMesh;
                int head = Styles.BoneIndex(renderer, "Head_ADJ");
                if (!mesh.isReadable || head < 0)
                {
                    Main.Log.Error($"{mesh.name}: {(mesh.isReadable ? "no head bone" : "not readable")}");
                    return;
                }
                m_Bind = mesh.bindposes[head];
                Points = mesh.vertices.Select(v => m_Bind.MultiplyPoint3x4(v)).ToArray();
                m_Submeshes = Enumerable.Range(0, mesh.subMeshCount).Select(s => mesh.GetTriangles(s)).ToArray();
                m_Pieces = Join();
                Ready = true;
            }

            internal int Count { get { return m_Pieces.Length; } }

            internal bool SameBones(Hair other)
            {
                var a = m_Renderer.bones.Select(b => b == null ? null : b.name).ToArray();
                var b2 = other.m_Renderer.bones.Select(b => b == null ? null : b.name).ToArray();
                if (a.SequenceEqual(b2) && Enumerable.Range(0, 16).All(i => Mathf.Abs(m_Bind[i] - other.m_Bind[i]) < 1e-4f))
                    return true;
                Main.Log.Error($"{m_Renderer.sharedMesh.name} and {other.m_Renderer.sharedMesh.name}: bones or head bind pose differ");
                return false;
            }

            internal int[] Corners(int t)
            {
                foreach (var list in m_Submeshes)
                {
                    if (t < list.Length / 3)
                        return new[] { list[3 * t], list[3 * t + 1], list[3 * t + 2] };
                    t -= list.Length / 3;
                }
                return null;
            }

            // The piece of each triangle: triangles sharing a point are one piece.
            int[] Join()
            {
                var same = new Dictionary<Vector3Int, int>();
                var root = new int[Points.Length];
                for (int v = 0; v < Points.Length; v++)
                {
                    var key = Vector3Int.RoundToInt(Points[v] / Same);
                    int first;
                    if (!same.TryGetValue(key, out first))
                        same[key] = first = v;
                    root[v] = first;
                }
                Func<int, int> find = x =>
                {
                    while (root[x] != x)
                        x = root[x] = root[root[x]];
                    return x;
                };
                int count = m_Submeshes.Sum(l => l.Length / 3);
                for (int t = 0; t < count; t++)
                {
                    var c = Corners(t);
                    int a = find(c[0]);
                    root[find(c[1])] = a;
                    root[find(c[2])] = a;
                }
                return Enumerable.Range(0, count).Select(t => find(Corners(t)[0])).ToArray();
            }

            // The triangles of the pieces whose middle is behind the head.
            internal HashSet<int> Tail()
            {
                return new HashSet<int>(Pieces(null).Where(p => Middle(p).y > Behind).SelectMany(p => p));
            }

            // The pieces, as their triangles, of all triangles or of those given.
            internal List<List<int>> Pieces(HashSet<int> among)
            {
                return Enumerable.Range(0, Count).Where(t => among == null || among.Contains(t))
                    .GroupBy(t => m_Pieces[t]).Select(g => g.ToList()).ToList();
            }

            // The middle of a piece's vertices.
            internal Vector3 Middle(List<int> triangles)
            {
                var points = triangles.SelectMany(Corners).Distinct().ToList();
                return points.Aggregate(Vector3.zero, (s, v) => s + Points[v]) / points.Count;
            }

            internal List<int[]> Triangles(Func<int, bool> which)
            {
                return Enumerable.Range(0, Count).Where(which).Select(Corners).ToList();
            }

            // A new mesh of the triangles kept and only their vertices, moved by offset in head bone space. The doll
            // takes from each mesh only the vertices its triangles span, and the uv and weights of all of them: vertices
            // left unused would set the two apart.
            internal Mesh Write(Vector3 offset, HashSet<int> keep)
            {
                var source = m_Renderer.sharedMesh;
                var move = m_Bind.inverse.MultiplyVector(offset);
                var used = new Dictionary<int, int>();
                var order = new List<int>();
                var lists = new List<int[]>();
                int first = 0;
                foreach (var list in m_Submeshes)
                {
                    var kept = new List<int>();
                    for (int t = 0; t < list.Length / 3; t++)
                        if (keep.Contains(first + t))
                            for (int c = 0; c < 3; c++)
                            {
                                int v = list[3 * t + c], at;
                                if (!used.TryGetValue(v, out at))
                                {
                                    used[v] = at = order.Count;
                                    order.Add(v);
                                }
                                kept.Add(at);
                            }
                    lists.Add(kept.ToArray());
                    first += list.Length / 3;
                }
                int count = source.vertexCount;
                Func<Vector3[], Vector3[]> pick3 = a => order.Select(v => a[v]).ToArray();
                var mesh = new Mesh();
                var vertices = source.vertices;
                mesh.vertices = order.Select(v => vertices[v] + move).ToArray();
                var normals = source.normals;
                if (normals.Length == count)
                    mesh.normals = pick3(normals);
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
                mesh.subMeshCount = lists.Count;
                for (int s = 0; s < lists.Count; s++)
                    mesh.SetTriangles(lists[s], s);
                mesh.RecalculateBounds();
                return mesh;
            }
        }
    }
}
