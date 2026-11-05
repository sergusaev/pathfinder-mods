using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Kingmaker.Visual.CharacterSystem;
using UnityEngine;

namespace KingmakerCosmetics
{
    // Diagnostics: two seconds after a doll wearing a face decoration merged its textures, what it draws is logged
    // from the mod's update (nothing is hooked into the drawing): its entities and parts, the atlas rectangles and the
    // renderers. Body part types are written as numbers. Each different state is logged once.
    static class DollDump
    {
        static readonly FieldInfo s_Atlases = typeof(Character).GetField("m_Atlases", BindingFlags.Instance | BindingFlags.NonPublic);
        static readonly HashSet<string> s_Logged = new HashSet<string>();
        static readonly List<KeyValuePair<Character, float>> s_Due = new List<KeyValuePair<Character, float>>();

        internal static void Soon(Character character)
        {
            if (character != null && !s_Due.Any(d => d.Key == character))
                s_Due.Add(new KeyValuePair<Character, float>(character, Time.realtimeSinceStartup + 2f));
        }

        internal static void Tick()
        {
            for (int i = s_Due.Count - 1; i >= 0; i--)
            {
                if (Time.realtimeSinceStartup < s_Due[i].Value)
                    continue;
                var character = s_Due[i].Key;
                s_Due.RemoveAt(i);
                if (character != null)
                    Dump(character);
            }
        }

        static void Dump(Character character)
        {
            try
            {
                var sb = new StringBuilder();
                sb.Append("doll " + character.name + " (doll room " + character.IsInDollRoom + ")\n");
                foreach (var ee in character.EquipmentEntities.Where(e => e != null).OrderBy(e => e.Layer))
                {
                    sb.Append("  ee " + ee.name + " layer " + ee.Layer + " hides " + ((long)ee.HideBodyParts).ToString("X") + ":");
                    foreach (var p in ee.BodyParts.Where(p => p != null))
                    {
                        var mesh = p.SkinnedRenderer != null ? p.SkinnedRenderer.sharedMesh : null;
                        Texture src = null;
                        if (p.Textures != null)
                            foreach (var t in p.Textures)
                                if (t != null && t.Channel == CharacterTextureChannel.Diffuse) { src = t.GetSourceTexture(); break; }
                        sb.Append(" [" + ((long)p.Type).ToString("X") + " "
                            + (p.RendererPrefab == null ? "overlay" : mesh == null ? "NO MESH" : mesh.name + " " + mesh.vertexCount)
                            + " " + (src == null ? "no tex" : src.name + " " + src.width + "x" + src.height) + "]");
                    }
                    sb.Append('\n');
                }
                var atlases = s_Atlases == null ? null : s_Atlases.GetValue(character) as IEnumerable;
                if (atlases != null)
                    foreach (CharacterAtlas atlas in atlases)
                    {
                        var at = atlas.AtlasTexture;
                        sb.Append("  atlas " + (int)atlas.Channel + " " + (at == null ? "none" : at.width + "x" + at.height) + "\n");
                        foreach (var r in atlas.Rects)
                            sb.Append("    " + (r.Key == null ? "null" : r.Key.name + " " + r.Key.width + "x" + r.Key.height)
                                + " -> " + r.Value.x + "," + r.Value.y + " " + r.Value.width + "x" + r.Value.height + "\n");
                    }
                foreach (var r in character.Renderers)
                {
                    if (r == null)
                        continue;
                    var m = r.sharedMesh;
                    var mat = r.sharedMaterial;
                    sb.Append("  renderer " + r.name + " enabled " + r.enabled + " active " + r.gameObject.activeInHierarchy
                        + " mesh " + (m == null ? "none" : m.vertexCount + " verts")
                        + " mat " + (mat == null ? "none" : mat.name + " / " + mat.shader.name + " / " + (mat.mainTexture == null ? "no tex" : mat.mainTexture.name)) + "\n");
                }
                string text = sb.ToString();
                if (s_Logged.Add(text))
                    Main.Log.Log(text);
            }
            catch (Exception ex)
            {
                Main.Log.Error("doll dump: " + ex);
            }
        }
    }
}
