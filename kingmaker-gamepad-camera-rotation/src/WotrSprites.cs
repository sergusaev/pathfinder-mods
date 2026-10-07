using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace GamepadCameraRotation
{
    // Finds the installed Wrath of the Righteous and locates named sprites inside its "ui" bundle.
    static class WotrSprites
    {
        public sealed class Atlas
        {
            public string Name;
            public int Width;
            public int Height;
            public int Format;
            public byte[] Data;
        }

        public sealed class Entry
        {
            public string Name;
            public Atlas Atlas;
            public float X, Y, Width, Height;
        }

        static readonly string[] WotrFolders = { "Pathfinder Second Adventure", "Pathfinder Wrath of the Righteous" };

        public static string FindBundle(string configured, string gameDataPath)
        {
            foreach (string dir in Candidates(configured, gameDataPath))
            {
                // Steam keeps the bundles in the game root; StreamingAssets is checked for other builds.
                string bundle = Path.Combine(Path.Combine(dir, "Bundles"), "ui");
                if (File.Exists(bundle)) return bundle;
                bundle = Path.Combine(Path.Combine(Path.Combine(Path.Combine(dir, "Wrath_Data"), "StreamingAssets"), "Bundles"), "ui");
                if (File.Exists(bundle)) return bundle;
            }
            return null;
        }

        static IEnumerable<string> Candidates(string configured, string gameDataPath)
        {
            if (!string.IsNullOrEmpty(configured)) yield return configured;

            var libraries = new List<string>();
            // <library>/steamapps/common/<game>/<game>_Data
            string common = Parent(Parent(gameDataPath));
            if (common != null)
            {
                foreach (string f in WotrFolders) yield return Path.Combine(common, f);
                string steamapps = Parent(common);
                if (steamapps != null) libraries.AddRange(ReadLibraries(Path.Combine(steamapps, "libraryfolders.vdf")));
            }
            string home = Environment.GetEnvironmentVariable("HOME");
            if (!string.IsNullOrEmpty(home))
            {
                libraries.AddRange(ReadLibraries(Path.Combine(home, ".local/share/Steam/steamapps/libraryfolders.vdf")));
                libraries.AddRange(ReadLibraries(Path.Combine(home, ".steam/steam/steamapps/libraryfolders.vdf")));
            }
            string programFiles = Environment.GetEnvironmentVariable("ProgramFiles(x86)");
            if (!string.IsNullOrEmpty(programFiles))
                libraries.AddRange(ReadLibraries(Path.Combine(programFiles, @"Steam\steamapps\libraryfolders.vdf")));

            foreach (string lib in libraries.Distinct())
                foreach (string f in WotrFolders)
                    yield return Path.Combine(Path.Combine(Path.Combine(lib, "steamapps"), "common"), f);
        }

        static string Parent(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            DirectoryInfo d = Directory.GetParent(path.TrimEnd('/', '\\'));
            return d == null ? null : d.FullName;
        }

        static IEnumerable<string> ReadLibraries(string vdf)
        {
            if (!File.Exists(vdf)) return new string[0];
            return Regex.Matches(File.ReadAllText(vdf), "\"path\"\\s+\"([^\"]+)\"")
                .Cast<Match>()
                .Select(m => m.Groups[1].Value.Replace("\\\\", "\\"))
                .ToList();
        }

        public static List<Entry> Load(string bundlePath, ICollection<string> names)
        {
            using (var bundle = new UnityBundle(bundlePath))
            {
                UnityBundle.Node node = bundle.Find(n => (n.Flags & 4) != 0);
                if (node == null) throw new InvalidDataException("no serialized file in " + bundlePath);
                var file = new SerializedFile(bundle.ReadNode(node, 0, checked((int)node.Size)));

                var result = new List<Entry>();
                var atlases = new Dictionary<long, Dictionary<string, object>>();
                var textures = new Dictionary<long, Atlas>();
                foreach (SerializedFile.ObjectInfo o in file.Objects.Values)
                {
                    if (o.ClassId != SerializedFile.ClassSprite) continue;
                    string name = file.PeekName(o);
                    if (name == null || !names.Contains(name)) continue;

                    var sprite = file.Read(o);
                    var atlasRef = (Dictionary<string, object>)sprite["m_SpriteAtlas"];
                    if (Convert.ToInt32(atlasRef["m_FileID"]) != 0) throw new NotSupportedException(name + ": atlas in another file");
                    long atlasId = Convert.ToInt64(atlasRef["m_PathID"]);
                    Dictionary<string, object> atlas;
                    if (!atlases.TryGetValue(atlasId, out atlas))
                        atlases[atlasId] = atlas = file.Read(file.Objects[atlasId]);

                    Dictionary<string, object> data = FindRenderData(atlas, sprite["m_RenderDataKey"]);
                    if (data == null) throw new InvalidDataException(name + ": no render data in atlas");
                    int settings = Convert.ToInt32(data["settingsRaw"]);
                    if (((settings >> 2) & 0xF) != 0) throw new NotSupportedException(name + ": rotated in atlas");

                    long textureId = Convert.ToInt64(((Dictionary<string, object>)data["texture"])["m_PathID"]);
                    Atlas texture;
                    if (!textures.TryGetValue(textureId, out texture))
                        textures[textureId] = texture = ReadTexture(bundle, file, file.Objects[textureId]);

                    var rect = (Dictionary<string, object>)data["textureRect"];
                    result.Add(new Entry
                    {
                        Name = name,
                        Atlas = texture,
                        X = Convert.ToSingle(rect["x"]),
                        Y = Convert.ToSingle(rect["y"]),
                        Width = Convert.ToSingle(rect["width"]),
                        Height = Convert.ToSingle(rect["height"])
                    });
                }
                return result;
            }
        }

        static Dictionary<string, object> FindRenderData(Dictionary<string, object> atlas, object key)
        {
            string wanted = KeyString(key);
            foreach (object item in (List<object>)atlas["m_RenderDataMap"])
            {
                var pair = (Dictionary<string, object>)item;
                if (KeyString(pair["first"]) == wanted) return (Dictionary<string, object>)pair["second"];
            }
            return null;
        }

        // pair<GUID, long long>: compare as text so the nested dictionaries need no structural equality.
        static string KeyString(object key)
        {
            var pair = (Dictionary<string, object>)key;
            var guid = (Dictionary<string, object>)pair["first"];
            return string.Join(",", guid.Values.Select(v => v.ToString()).ToArray()) + ":" + pair["second"];
        }

        static Atlas ReadTexture(UnityBundle bundle, SerializedFile file, SerializedFile.ObjectInfo o)
        {
            var tex = file.Read(o);
            var atlas = new Atlas
            {
                Name = (string)tex["m_Name"],
                Width = Convert.ToInt32(tex["m_Width"]),
                Height = Convert.ToInt32(tex["m_Height"]),
                Format = Convert.ToInt32(tex["m_TextureFormat"])
            };
            int mips = Convert.ToInt32(tex["m_MipCount"]);
            int size = Convert.ToInt32(tex["m_CompleteImageSize"]);
            if (mips != 1) throw new NotSupportedException(atlas.Name + ": " + mips + " mips");

            var image = (byte[])tex["image data"];
            if (image.Length > 0)
            {
                atlas.Data = image;
                return atlas;
            }
            var stream = (Dictionary<string, object>)tex["m_StreamData"];
            string path = (string)stream["path"];
            string nodeName = path.Substring(path.LastIndexOf('/') + 1);
            UnityBundle.Node node = bundle.Find(n => n.Path == nodeName);
            if (node == null) throw new InvalidDataException(atlas.Name + ": stream " + path + " not found");
            atlas.Data = bundle.ReadNode(node, Convert.ToInt64(stream["offset"]), size);
            return atlas;
        }
    }
}
