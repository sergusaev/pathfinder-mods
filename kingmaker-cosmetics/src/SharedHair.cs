using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.CharGen;
using Kingmaker.Blueprints.Classes;
using Kingmaker.Blueprints.Root;
using Kingmaker.ResourceLinks;
using Kingmaker.Visual.CharacterSystem;
using UnityEngine;

namespace KingmakerCosmetics
{
    // Races whose heads are one head (the same mesh of the first head option, bound to the head bone at the same place)
    // get the hair of each other, of the same gender, after their own, as it is. A style a race already has under its
    // own name (EE_Hair_<style>_<gender>_<race>) is not added again. Hair is not made over for other heads: refitted to
    // them it looked bad.
    static class SharedHair
    {
        internal static void Build()
        {
            var watch = Stopwatch.StartNew();
            var races = BlueprintRoot.Instance.Progression.CharacterRaces.Where(r => r != null).ToList();
            foreach (var female in new[] { false, true })
            {
                var heads = races.ToDictionary(r => r, r => HeadRenderer(r, female));
                var keys = races.ToDictionary(r => r, r => HeadKey(r, female, heads[r]));
                // Human hair was seen sitting right on aasimars and the other way round, whatever their meshes say.
                var human = races.FirstOrDefault(r => r.RaceId == Race.Human);
                var aasimar = races.FirstOrDefault(r => r.RaceId == Race.Aasimar);
                if (human != null && aasimar != null && keys[human] != null && keys[aasimar] != null)
                    keys[aasimar] = keys[human];
                var own = races.ToDictionary(r => r, r => Options(r, female).Hair.Where(l => l != null && HairPart(l.AssetId) != null).ToList());

                var groups = races.GroupBy(r => keys[r]).Where(g => g.Key != null && g.Count() > 1);
                foreach (var group in groups)
                {
                    var pool = group.SelectMany(r => own[r]).ToList();
                    foreach (var race in group)
                    {
                        int before = Options(race, female).Hair.Length;
                        int count = Add(race, female, pool);
                        Main.Log.Log($"{race.name} {(female ? "female" : "male")}: one head with {string.Join(", ", group.Where(r => r != race).Select(r => r.name).ToArray())}, hair styles {before} -> {count}");
                    }
                }
            }
            Main.Log.Log($"hair shared in {watch.ElapsedMilliseconds} ms");
        }

        // The links of styles the race does not have yet go after its hair.
        static int Add(BlueprintRace race, bool female, List<EquipmentEntityLink> links)
        {
            var options = Options(race, female);
            var hair = options.Hair.ToList();
            var styles = new HashSet<string>(hair.Select(l => Style(l.AssetId)));
            var ids = new HashSet<string>(hair.Select(l => l.AssetId));
            foreach (var link in links)
                if (!ids.Contains(link.AssetId) && styles.Add(Style(link.AssetId)))
                {
                    ids.Add(link.AssetId);
                    hair.Add(link);
                }
            options.Hair = hair.ToArray();
            return hair.Count;
        }

        // The mesh of the first head option.
        static SkinnedMeshRenderer HeadRenderer(BlueprintRace race, bool female)
        {
            var heads = Options(race, female).Heads;
            var link = heads.FirstOrDefault(l => l != null && !string.IsNullOrEmpty(l.AssetId) && ResourcesLibrary.LibraryObject.ResourceNamesByAssetId.ContainsKey(l.AssetId));
            var ee = link == null ? null : link.Load(true);
            return ee == null ? null : ee.BodyParts
                .Where(p => p != null && (p.Type & BodyPartType.Head) != 0 && p.SkinnedRenderer != null && p.SkinnedRenderer.sharedMesh != null)
                .Select(p => p.SkinnedRenderer).OrderByDescending(r => r.sharedMesh.vertexCount).FirstOrDefault();
        }

        // The head mesh: vertices, size and the place of the head bone, to the millimetre.
        static string HeadKey(BlueprintRace race, bool female, SkinnedMeshRenderer renderer)
        {
            if (renderer == null)
            {
                Main.Log.Log($"{race.name} {(female ? "female" : "male")}: no head mesh, hair not shared");
                return null;
            }
            var mesh = renderer.sharedMesh;
            var bone = Styles.BonePosition(renderer, "Head_ADJ");
            string key = $"{mesh.vertexCount} {Mm(mesh.bounds.min)} {Mm(mesh.bounds.max)} {(bone.HasValue ? Mm(bone.Value) : "-")}";
            Main.Log.Log($"{race.name} {(female ? "female" : "male")}: head {mesh.name}, {key}");
            return key;
        }

        static string Mm(Vector3 v)
        {
            return $"{Mathf.RoundToInt(v.x * 1000)},{Mathf.RoundToInt(v.y * 1000)},{Mathf.RoundToInt(v.z * 1000)}";
        }

        static CustomizationOptions Options(BlueprintRace race, bool female)
        {
            return female ? race.FemaleOptions : race.MaleOptions;
        }

        // The hair part with a mesh of the entity, or null: no resource, not loaded, or only painted on the head.
        internal static BodyPart HairPart(string id)
        {
            // A link the library has no resource for would leave an empty entry behind Load.
            if (string.IsNullOrEmpty(id) || !Styles.OwnIds.Contains(id) && !ResourcesLibrary.LibraryObject.ResourceNamesByAssetId.ContainsKey(id))
                return null;
            var ee = ResourcesLibrary.TryGetResource<EquipmentEntity>(id, true);
            return ee == null ? null : ee.BodyParts.FirstOrDefault(p => p != null && (p.Type & BodyPartType.Hair) != 0 && p.SkinnedRenderer != null);
        }

        static readonly Regex s_StyleName = new Regex(@"^EE_Hair_(.+)_[A-Za-z]+_[A-Za-z]+$");

        static string Style(string id)
        {
            string name;
            if (Styles.OwnIds.Contains(id) || !ResourcesLibrary.LibraryObject.ResourceNamesByAssetId.TryGetValue(id, out name))
                return id;
            var match = s_StyleName.Match(name);
            return match.Success ? match.Groups[1].Value : name;
        }
    }
}
