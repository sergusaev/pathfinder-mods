using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using HarmonyLib;
using Kingmaker.Blueprints;
using Kingmaker.Visual.CharacterSystem;
using UnityEngine;

namespace KingmakerCosmetics
{
    // The library of the mod's entities: made in the game from Kingmaker ones, put into ResourcesLibrary under their
    // own ids and kept there for good, together with the Kingmaker entities they share materials and textures with.
    static class Styles
    {
        // Ids of the mod's own entities, made or to be made.
        internal static readonly HashSet<string> OwnIds = new HashSet<string>();

        // Ids and entities kept loaded for good: ours and the Kingmaker ones they share materials and textures with.
        internal static readonly HashSet<string> KeptIds = new HashSet<string>();
        internal static readonly HashSet<EquipmentEntity> KeptEntities = new HashSet<EquipmentEntity>();

        static readonly IDictionary s_Loaded = (IDictionary)AccessTools.Field(typeof(ResourcesLibrary), "s_LoadedResources").GetValue(null);
        static readonly Type s_LoadedResource = typeof(ResourcesLibrary).GetNestedType("LoadedResource", BindingFlags.NonPublic);
        static readonly FieldInfo s_RequestCounter = AccessTools.Field(s_LoadedResource, "RequestCounter");
        static readonly FieldInfo s_Resource = AccessTools.Field(s_LoadedResource, "Resource");

        // Entities made only when something is about to load them (Ensure): there are hundreds, most never worn.
        static readonly Dictionary<string, Func<EquipmentEntity>> s_Pending = new Dictionary<string, Func<EquipmentEntity>>();

        static GameObject s_Holder;

        internal static void Build()
        {
            Bob.Build();
            ElfStyles.Build();
            // Before the hair is fitted: hair is cut for the ears as they will be.
            ElfEars.Build();
            // After the ears: the face is a copy of an elf face and keeps their ears.
            SideBraid.Build();
            // After the side braid face: it is made from the first face as the game has it.
            Lashes.Build();
            // The game faces, after the side braid face took its own textures from the first one.
            Eyes.Build();
            SharedHair.Build();
            HairColors.Build();
        }

        internal static void Later(string id, Func<EquipmentEntity> make)
        {
            s_Pending[id] = make;
            OwnIds.Add(id);
        }

        internal static void Ensure(string id)
        {
            Func<EquipmentEntity> make;
            if (id == null || !s_Pending.TryGetValue(id, out make))
                return;
            s_Pending.Remove(id);
            try
            {
                var ee = make();
                if (ee != null)
                    Register(id, ee);
                else
                    Main.Log.Error($"{id} not made");
            }
            catch (Exception ex)
            {
                Main.Log.Error($"{id} not made: {ex}");
            }
        }

        // Entities to make soon, one an update: the styles next to the one on the doll, made before they are chosen.
        static readonly Queue<string> s_Soon = new Queue<string>();

        internal static void Soon(string id)
        {
            if (id != null && s_Pending.ContainsKey(id) && !s_Soon.Contains(id))
                s_Soon.Enqueue(id);
        }

        internal static void Tick()
        {
            while (s_Soon.Count > 0)
            {
                string id = s_Soon.Dequeue();
                if (s_Pending.ContainsKey(id))
                {
                    Ensure(id);
                    return;
                }
            }
        }

        // A stable id for an entity made from another: saves keep it.
        internal static string MadeId(string from, string what)
        {
            using (var md5 = MD5.Create())
            {
                var hash = md5.ComputeHash(Encoding.UTF8.GetBytes("KingmakerCosmetics|" + from + "|" + what));
                return string.Concat(hash.Select(b => b.ToString("x2")).ToArray());
            }
        }

        // A copy of a Kingmaker entity, with the original kept loaded: the copy uses its materials and textures.
        internal static EquipmentEntity Copy(string sourceId, EquipmentEntity original, string suffix)
        {
            var copy = UnityEngine.Object.Instantiate(original);
            copy.name = original.name + suffix;
            copy.hideFlags = HideFlags.DontUnloadUnusedAsset;
            if (!OwnIds.Contains(sourceId))
                Keep(sourceId, original);
            return copy;
        }

        // The copy's part gets its own renderer prefab with the mesh given. The copy's parts are new objects whose
        // SkinnedRenderer is cached on first use, so the original's part is read.
        // keep: the renderer's bones the mesh still has, in their order (all when null).
        internal static void ReplaceMesh(EquipmentEntity copy, int index, BodyPart originalPart, Mesh mesh, int[] keep = null)
        {
            if (s_Holder == null)
            {
                s_Holder = new GameObject("KingmakerCosmetics");
                s_Holder.transform.position = new Vector3(0, -10000, 0);
                UnityEngine.Object.DontDestroyOnLoad(s_Holder);
            }
            var prefab = UnityEngine.Object.Instantiate(originalPart.RendererPrefab, s_Holder.transform, false);
            // The name stays: Character finds bones by the names of the renderer's transforms.
            prefab.name = originalPart.RendererPrefab.name;
            foreach (var r in prefab.GetComponentsInChildren<Renderer>(true))
                r.enabled = false;
            mesh.name = originalPart.SkinnedRenderer.sharedMesh.name + "_" + copy.name;
            mesh.hideFlags = HideFlags.DontUnloadUnusedAsset;
            var renderer = prefab.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (keep != null)
            {
                var bones = renderer.bones;
                renderer.bones = keep.Select(b => bones[b]).ToArray();
            }
            renderer.sharedMesh = mesh;
            copy.BodyParts[index].RendererPrefab = prefab;
        }

        // Mesh space position of a bone of the renderer: the inverse of its bind pose applied to the origin.
        internal static Vector3? BonePosition(SkinnedMeshRenderer renderer, string bone)
        {
            int i = BoneIndex(renderer, bone);
            return i < 0 ? (Vector3?)null : renderer.sharedMesh.bindposes[i].inverse.MultiplyPoint3x4(Vector3.zero);
        }

        internal static int BoneIndex(SkinnedMeshRenderer renderer, string bone)
        {
            var bones = renderer.bones;
            int count = Math.Min(bones.Length, renderer.sharedMesh.bindposes.Length);
            for (int i = 0; i < count; i++)
                if (bones[i] != null && string.Equals(bones[i].name, bone, StringComparison.OrdinalIgnoreCase))
                    return i;
            return -1;
        }

        // The entity goes into the resources library under its id, as if loaded from a bundle: links, the doll room
        // and DollData of saves find it through ResourcesLibrary like any other.
        internal static void Register(string id, EquipmentEntity ee)
        {
            // The constructor taking the resource left it unset when called through reflection; the field is set here.
            var entry = Activator.CreateInstance(s_LoadedResource, true);
            s_Resource.SetValue(entry, ee);
            s_Loaded[id] = entry;
            OwnIds.Add(id);
            Keep(id, ee);
        }

        internal static void Keep(string id, EquipmentEntity ee)
        {
            KeptIds.Add(id);
            KeptEntities.Add(ee);
            s_Entries[id] = s_Loaded[id];
            s_Made[id] = ee;
        }

        // The library entries of kept ids, put back whenever the library has dropped or replaced them.
        static readonly Dictionary<string, object> s_Entries = new Dictionary<string, object>();

        // CleanupLoadedCache unloads every entry nobody asked for since the last cleanup.
        internal static void MarkKeptRequested()
        {
            foreach (var pair in s_Entries)
                s_RequestCounter.SetValue(pair.Value, 1);
        }

        internal static void Restore(string where)
        {
            foreach (var pair in s_Entries)
            {
                if (s_Loaded.Contains(pair.Key) && s_Loaded[pair.Key] == pair.Value)
                    continue;
                Main.Log.Log($"{where}: library entry {pair.Key} put back");
                s_Loaded[pair.Key] = pair.Value;
            }
        }

        // What the library entry of a kept id holds: the entity made, another object, nothing, or a destroyed one.
        internal static string Describe(string id)
        {
            object entry = s_Loaded.Contains(id) ? s_Loaded[id] : null;
            if (entry == null)
                return "no entry";
            object resource = s_Resource.GetValue(entry);
            object made;
            s_Made.TryGetValue(id, out made);
            string madeState = made == null ? "none made" : (UnityEngine.Object)made == null ? "made one destroyed" : "made one alive";
            if (ReferenceEquals(resource, null))
                return $"entry {(ReferenceEquals(entry, s_Entries[id]) ? "ours" : "other")}, resource null, {madeState}";
            return $"entry {(ReferenceEquals(entry, s_Entries[id]) ? "ours" : "other")}, resource {resource.GetType().Name}"
                + $"{((UnityEngine.Object)resource == null ? " destroyed" : " alive")}, same as made {ReferenceEquals(resource, made)}, {madeState}";
        }

        static readonly Dictionary<string, object> s_Made = new Dictionary<string, object>();

        internal static bool IsKept(object entry)
        {
            return s_Entries.ContainsValue(entry);
        }
    }

    [HarmonyPatch(typeof(ResourcesLibrary), nameof(ResourcesLibrary.CleanupLoadedCache))]
    static class KeepOnCleanup
    {
        static void Prefix()
        {
            Styles.MarkKeptRequested();
        }

        static void Postfix()
        {
            Styles.Restore("cache cleanup");
        }
    }

    // Whatever drops a kept entry, its entity and the bundle objects it shares are not destroyed; the stack says who.
    [HarmonyPatch]
    static class KeepOnEntryUnload
    {
        static bool s_Logged;

        static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(ResourcesLibrary).GetNestedType("LoadedResource", BindingFlags.NonPublic), "Unload");
        }

        static bool Prefix(object __instance)
        {
            if (!Styles.IsKept(__instance))
                return true;
            if (!s_Logged)
            {
                s_Logged = true;
                Main.Log.Log("kept entry not unloaded, asked by: " + Environment.StackTrace);
            }
            return false;
        }
    }


    // The character creation doll room unloads entities no longer on the doll; for a kept one it is told "done",
    // so it drops it from its lists and loads it again from the library when it is chosen again.
    [HarmonyPatch(typeof(ResourcesLibrary), nameof(ResourcesLibrary.TryUnloadResource))]
    static class KeepOnUnload
    {
        static bool Prefix(string assetId, ref bool __result)
        {
            if (assetId == null || !Styles.KeptIds.Contains(assetId))
                return true;
            __result = true;
            return false;
        }
    }

    [HarmonyPatch(typeof(EquipmentEntity), nameof(EquipmentEntity.UnloadInnerAssetsExceptGiven))]
    static class KeepInnerAssets
    {
        static bool Prefix(EquipmentEntity __instance)
        {
            return !Styles.KeptEntities.Contains(__instance);
        }
    }
}
