using System.Linq;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Classes;
using Kingmaker.ResourceLinks;
using Kingmaker.Visual.CharacterSystem;
using UnityEngine;

namespace KingmakerCosmetics
{
    // The longest female human hair shortened to a bob, shorter at the nape and longer toward the face.
    static class Bob
    {
        // Asset id of the bob. It goes into DollData.EquipmentEntityIds of saves, so it never changes.
        internal const string Id = "8250fb8c7c0e48ffa98d6fafa7d75d1c";

        // Where the bob ends at the nape and in front, from the neck bone (0) to the head bone (1).
        const float BackEnd = 0.85f;
        const float FrontEnd = 0.45f;

        internal static void Build()
        {
            var race = ResourcesLibrary.GetBlueprints<BlueprintRace>().FirstOrDefault(r => r.name == "HumanRace");
            if (race == null)
            {
                Main.Log.Error("HumanRace not found, no bob");
                return;
            }
            var options = race.FemaleOptions;
            EquipmentEntityLink source = null;
            float lowest = float.MaxValue;
            foreach (var link in options.Hair)
            {
                var part = SharedHair.HairPart(link.AssetId);
                if (part == null)
                    continue;
                var mesh = part.SkinnedRenderer.sharedMesh;
                if (mesh.isReadable && mesh.bounds.min.y < lowest)
                {
                    lowest = mesh.bounds.min.y;
                    source = link;
                }
            }
            if (source == null)
            {
                Main.Log.Error("no readable female human hair, no bob");
                return;
            }
            var original = source.Load(true);
            var copy = Styles.Copy(source.AssetId, original, "_Bob");
            for (int i = 0; i < original.BodyParts.Count; i++)
            {
                var part = original.BodyParts[i];
                if (part == null || (part.Type & BodyPartType.Hair) == 0 || part.SkinnedRenderer == null)
                    continue;
                var renderer = part.SkinnedRenderer;
                Vector3? head = Styles.BonePosition(renderer, "Head_ADJ"), neck = Styles.BonePosition(renderer, "Neck_ADJ");
                if (!head.HasValue || !neck.HasValue)
                {
                    Main.Log.Error($"{copy.name}: no head or neck bone, left as is");
                    continue;
                }
                float from = head.Value.y;
                float back = Mathf.Lerp(neck.Value.y, head.Value.y, BackEnd), front = Mathf.Lerp(neck.Value.y, head.Value.y, FrontEnd);
                float forward = Forward(renderer);
                Main.Log.Log($"{copy.name}: hair below {from:0.000} drawn up to end at {back:0.000} at the nape, {front:0.000} in front (forward {forward:+0;-0} z)");
                Styles.ReplaceMesh(copy, i, part, MeshShape.Shorten(renderer.sharedMesh, from, back, front, forward));
            }
            Styles.Register(Id, copy);
            var hair = options.Hair.ToList();
            hair.Add(new EquipmentEntityLink { AssetId = Id });
            options.Hair = hair.ToArray();
            Main.Log.Log($"bob made from {original.name}, female human hair style {options.Hair.Length}");
        }

        // The sign of the z axis the character faces in mesh space: from the foot bone to the toe bone.
        static float Forward(SkinnedMeshRenderer renderer)
        {
            Vector3? toe = null, foot = null;
            foreach (var name in new[] { "L_Toe", "L_toe", "L_Toe0", "L_foot_toe" })
                if ((toe = Styles.BonePosition(renderer, name)).HasValue)
                    break;
            foreach (var name in new[] { "L_Foot", "L_foot", "L_ankle" })
                if ((foot = Styles.BonePosition(renderer, name)).HasValue)
                    break;
            if (toe.HasValue && foot.HasValue && Mathf.Abs(toe.Value.z - foot.Value.z) > 0.01f)
                return Mathf.Sign(toe.Value.z - foot.Value.z);
            Main.Log.Log("bob: no foot and toe bones among " + string.Join(", ", renderer.bones.Where(b => b != null).Select(b => b.name).ToArray()) + "; +z taken as forward");
            return 1f;
        }
    }
}
