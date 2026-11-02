using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace PortraitGallery.Export
{
    // The portraits of Wrath of the Righteous as Kingmaker custom portrait folders, written by the mod itself when WotR
    // is installed on the same machine: one folder per portrait with Small.png 185x242, Medium.png 330x432,
    // Fulllength.png 692x1024 and PortraitGallery.txt (the gallery's category and order). Nothing of WotR ships
    // with the mod.
    //
    // Categories:
    //   WotR            - the generic Wrath portraits of WotR's character creation, in the game's order
    //   WotRCharacters  - Wrath NPCs from the creation list (Horgus, Anevia, Irabeth, Staunton, Nurah, Trever),
    //                     companions, NPCs and mythic forms
    // Characters who exist in Kingmaker are never exported (LichMaleVordakai is Vordakai).
    //
    // Folder names never change (saves point at a custom portrait by folder name): the Wrath portraits of WotR's
    // creation list are !WotR_NN_<name> by their place in that list (the names of the first export), the rest
    // !WotR_<name>; a folder already there under its old name is kept. A stamp file next to the folders records the WotR files the export was made from;
    // while they are the same and every folder is whole, nothing is read.
    sealed class WotrExport
    {
        internal const int Version = 1;
        const string Marker = "PortraitGallery.txt";
        internal const string StampName = "PortraitGallery-WotR.txt";
        static readonly string[] Files = { "Small", "Medium", "Fulllength" };
        static readonly int[,] Sizes = { { 185, 242 }, { 330, 432 }, { 692, 1024 } };
        static readonly string[] ImageFields = { "m_PortraitImage", "m_HalfLengthImage", "m_FullLengthImage" };
        static readonly HashSet<string> CreationNpcs = new HashSet<string> {
            "Horgus_Portrait", "Anevia_Portrait", "Irabeth_Portrait", "Staunton_Portrait", "Nurah_Portrait", "TreverHumanMwangiMaleWarrior" };
        static readonly HashSet<string> KingmakerCharacters = new HashSet<string> { "LichMaleVordakai_Portrait" };
        static readonly string[] ExtraCharacters = { "Aivu_Portrait" };

        class Bp { public string Name, Guid, Path; public JObject Data; }
        class Item { public string Category, Folder; public int Order, Creation; public Bp Bp; public string[] Refs; }

        readonly string m_Wotr, m_Portraits;
        readonly Action<string> m_Log;

        WotrExport(string wotr, string portraits, Action<string> log) { m_Wotr = wotr; m_Portraits = portraits; m_Log = log; }

        // Finds WotR and brings the folders up to date. Returns the number of folders written.
        internal static int Run(string kingmakerData, string portraits, Action<string> log)
        {
            string wotr = WotrLocator.Find(kingmakerData, log);
            if (wotr == null) { log("wotr export: Wrath of the Righteous is not found, nothing to do"); return 0; }
            return new WotrExport(wotr, portraits, log).Update();
        }

        string Zip { get { return Path.Combine(m_Wotr, "blueprints.zip"); } }
        string BundlePath { get { return Path.Combine(Path.Combine(m_Wotr, "Bundles"), "portraits"); } }

        string Source()
        {
            var z = new FileInfo(Zip);
            var b = new FileInfo(BundlePath);
            return string.Format("version={0}\nblueprints={1} {2}\nportraits={3} {4}", Version, z.Length, z.LastWriteTimeUtc.Ticks, b.Length, b.LastWriteTimeUtc.Ticks);
        }

        bool Whole(string folder)
        {
            string dir = Path.Combine(m_Portraits, folder);
            return File.Exists(Path.Combine(dir, Marker)) && Files.All(f => File.Exists(Path.Combine(dir, f + ".png")));
        }

        int Update()
        {
            string stamp = Path.Combine(m_Portraits, StampName);
            string source = Source();
            if (File.Exists(stamp))
            {
                var lines = File.ReadAllLines(stamp);
                string recorded = string.Join("\n", lines.TakeWhile(l => l.Length > 0).ToArray());
                var folders = lines.SkipWhile(l => l.Length > 0).Where(l => l.Length > 0).ToList();
                if (recorded == source && folders.Count > 0 && folders.All(Whole))
                {
                    m_Log("wotr export: up to date, " + folders.Count + " portraits from " + m_Wotr);
                    return 0;
                }
            }

            var started = DateTime.UtcNow;
            List<Item> items;
            using (var zip = Ionic.Zip.ZipFile.Read(Zip))
                items = Plan(zip);
            var old = ExistingFolders();
            var counts = new Dictionary<string, int>();
            var todo = new List<Item>();
            var missing = new List<string>();
            foreach (var it in items)
            {
                int n;
                counts.TryGetValue(it.Category, out n);
                counts[it.Category] = it.Order = n + 1;
                string s = Short(it.Bp.Name);
                string key = s.StartsWith("Player", StringComparison.Ordinal) ? s.Substring("Player".Length) : s;
                string folder;
                if (!old.TryGetValue(key.ToLowerInvariant(), out folder) && !old.TryGetValue(s.ToLowerInvariant(), out folder))
                    folder = it.Creation > 0 ? string.Format("!WotR_{0:00}_{1}", it.Creation, key) : "!WotR_" + s;
                it.Folder = folder;
                it.Refs = ImageFields.Select(f => { var r = it.Bp.Data[f] as JObject; return r != null ? (string)r["AssetId"] : null; }).ToArray();
                if (it.Refs.Any(string.IsNullOrEmpty)) { missing.Add(it.Bp.Name); continue; }
                todo.Add(it);
            }

            int written = 0;
            var need = todo.Where(it => !Files.All(f => File.Exists(Path.Combine(Path.Combine(m_Portraits, it.Folder), f + ".png")))).ToList();
            if (need.Count > 0) written = Extract(need, missing);
            var done = new List<string>();
            foreach (var it in todo)
            {
                if (missing.Contains(it.Bp.Name)) continue;
                string dir = Path.Combine(m_Portraits, it.Folder);
                if (!Directory.Exists(dir)) continue;
                string text = string.Format("category={0}\norder={1}\nblueprint={2}\nguid={3}\n", it.Category, it.Order, it.Bp.Name, it.Bp.Guid);
                string marker = Path.Combine(dir, Marker);
                if (!File.Exists(marker) || File.ReadAllText(marker) != text) File.WriteAllText(marker, text, new UTF8Encoding(false));
                done.Add(it.Folder);
            }
            File.WriteAllText(stamp, source + "\n\n" + string.Join("\n", done.ToArray()) + "\n", new UTF8Encoding(false));
            m_Log(string.Format("wotr export: {0} portraits ({1}), {2} written now, {3:F1} s, from {4}{5}", done.Count,
                string.Join(", ", counts.Select(c => c.Key + " " + c.Value).ToArray()), written, (DateTime.UtcNow - started).TotalSeconds, m_Wotr,
                missing.Count > 0 ? "; skipped without all three images: " + string.Join(", ", missing.ToArray()) : ""));
            return written;
        }

        // Writes the three pictures of each item into a scratch folder and moves it into place whole, so the game
        // never lists a half-written folder.
        int Extract(List<Item> items, List<string> missing)
        {
            int written = 0;
            string scratch = Path.Combine(Path.GetDirectoryName(m_Portraits.TrimEnd('\\', '/')), "PortraitGallery-export");
            if (Directory.Exists(scratch)) Directory.Delete(scratch, true);
            Directory.CreateDirectory(scratch);
            try
            {
                using (var bundle = new Bundle(BundlePath))
                {
                    var cabNode = bundle.Find(p => !p.EndsWith(".resS", StringComparison.Ordinal) && !p.EndsWith(".resource", StringComparison.Ordinal));
                    var assets = new Assets(bundle.Read(cabNode.Offset, (int)cabNode.Size));
                    var sprites = SpritesByAsset(assets);
                    foreach (var it in items)
                    {
                        long[] ids = it.Refs.Select(r => { long id; return sprites.TryGetValue(r.ToLowerInvariant(), out id) ? id : 0; }).ToArray();
                        if (ids.Any(id => id == 0)) { missing.Add(it.Bp.Name); continue; }
                        string tmp = Path.Combine(scratch, it.Folder);
                        Directory.CreateDirectory(tmp);
                        for (int i = 0; i < Files.Length; i++)
                        {
                            var pic = SpritePicture(bundle, assets, ids[i]);
                            pic = Images.Resize(pic, Sizes[i, 0], Sizes[i, 1]);
                            Images.SavePng(pic, Path.Combine(tmp, Files[i] + ".png"));
                        }
                        string dir = Path.Combine(m_Portraits, it.Folder);
                        if (Directory.Exists(dir))
                        {
                            foreach (var f in Directory.GetFiles(tmp)) File.Copy(f, Path.Combine(dir, Path.GetFileName(f)), true);
                        }
                        else
                            Directory.Move(tmp, dir);
                        written++;
                    }
                }
            }
            finally
            {
                try { Directory.Delete(scratch, true); } catch { }
            }
            return written;
        }

        // Container of the bundle: asset id of a portrait image -> its Sprite.
        static Dictionary<string, long> SpritesByAsset(Assets assets)
        {
            var result = new Dictionary<string, long>();
            foreach (var o in assets.Objects.Values)
            {
                if (o.ClassId != 142) continue;
                var bundle = assets.Read(o.PathId);
                foreach (Dictionary<string, object> pair in (List<object>)bundle["m_Container"])
                {
                    var asset = (Dictionary<string, object>)((Dictionary<string, object>)pair["second"])["asset"];
                    long id = (long)asset["m_PathID"];
                    if (assets.ClassOf(id) == 213) result[((string)pair["first"]).ToLowerInvariant()] = id;
                }
            }
            return result;
        }

        static Picture SpritePicture(Bundle bundle, Assets assets, long spriteId)
        {
            var sprite = assets.Read(spriteId);
            var rd = (Dictionary<string, object>)sprite["m_RD"];
            long texId = (long)((Dictionary<string, object>)rd["texture"])["m_PathID"];
            var rect = (Dictionary<string, object>)rd["textureRect"];
            var tex = assets.Read(texId);
            int w = (int)(long)tex["m_Width"], h = (int)(long)tex["m_Height"], format = (int)(long)tex["m_TextureFormat"];
            byte[] data = (byte[])tex["image data"];
            var stream = tex.ContainsKey("m_StreamData") ? (Dictionary<string, object>)tex["m_StreamData"] : null;
            if ((data == null || data.Length == 0) && stream != null && (long)stream["size"] > 0)
            {
                string file = Path.GetFileName((string)stream["path"]);
                var node = bundle.Find(p => p == file);
                data = bundle.Read(node.Offset + (long)stream["offset"], (int)(long)stream["size"]);
            }
            var pic = Images.Decode(data, format, w, h);
            int rx = (int)Math.Round((double)rect["x"]), ry = (int)Math.Round((double)rect["y"]);
            int rw = (int)Math.Round((double)rect["width"]), rh = (int)Math.Round((double)rect["height"]);
            return Images.Crop(pic, rx, ry, Math.Min(rw, w - rx), Math.Min(rh, h - ry));
        }

        List<Item> Plan(Ionic.Zip.ZipFile zip)
        {
            var byName = new Dictionary<string, Bp>();
            var byGuid = new Dictionary<string, Bp>();
            foreach (var e in zip)
            {
                string n = e.FileName;
                if (!n.StartsWith("Units/Portraits/", StringComparison.Ordinal) || !n.EndsWith(".jbp", StringComparison.Ordinal)) continue;
                var d = Json(e);
                var data = d["Data"] as JObject;
                if (data == null || !((string)data["$type"] ?? "").Contains("BlueprintPortrait")) continue;
                string name = Path.GetFileNameWithoutExtension(n);
                var bp = new Bp { Name = name, Guid = (string)d["AssetId"], Path = n, Data = (data["Data"] as JObject) ?? new JObject() };
                if (!byName.ContainsKey(name)) byName[name] = bp;
                byGuid[bp.Guid] = bp;
            }
            var root = (JObject)Json(zip["Root/BlueprintRoot.jbp"])["Data"];
            var creation = new List<Bp>();
            foreach (var x in (JArray)root["CharGen"]["m_Portraits"])
            {
                string s = (string)x;
                Bp bp;
                if (s != null && s.Length > 4 && byGuid.TryGetValue(s.Substring(4), out bp)) creation.Add(bp);
            }
            var wrath = creation.Where(bp => (string)bp.Data["PortraitCategory"] == "Wrath").ToList();
            var output = new List<Item>();
            var seen = new HashSet<string>();
            Action<Bp, string> add = (bp, category) =>
            {
                if (seen.Contains(bp.Name) || KingmakerCharacters.Contains(bp.Name) || bp.Name.Contains("InitiativeOnly")) return;
                seen.Add(bp.Name);
                output.Add(new Item { Category = category, Bp = bp, Creation = wrath.IndexOf(bp) + 1 });
            };
            foreach (var bp in wrath) if (!CreationNpcs.Contains(bp.Name)) add(bp, "WotR");
            foreach (var bp in wrath) if (CreationNpcs.Contains(bp.Name)) add(bp, "WotRCharacters");
            foreach (var name in FolderList(byName, "Companions").Concat(ExtraCharacters).Concat(FolderList(byName, "NPC")))
            {
                Bp bp;
                if (byName.TryGetValue(name, out bp)) add(bp, "WotRCharacters");
            }
            var mythic = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var bp in byName.Values) if ((string)bp.Data["PortraitCategory"] == "Mythic") mythic.Add(bp.Name);
            foreach (var name in FolderList(byName, "Mythic")) mythic.Add(name);
            foreach (var name in mythic) add(byName[name], "WotRCharacters");
            return output;
        }

        static JObject Json(Ionic.Zip.ZipEntry e)
        {
            using (var s = e.OpenReader())
            using (var r = new StreamReader(s, Encoding.UTF8, true))
                return JObject.Parse(r.ReadToEnd());
        }

        static List<string> FolderList(Dictionary<string, Bp> byName, string folder)
        {
            string prefix = "Units/Portraits/" + folder + "/";
            var list = byName.Values.Where(bp => bp.Path.StartsWith(prefix, StringComparison.Ordinal) && bp.Path.Count(c => c == '/') == 3)
                .Select(bp => bp.Name).ToList();
            list.Sort(StringComparer.Ordinal);
            return list;
        }

        static string Short(string name)
        {
            foreach (string tail in new[] { "_Portrait", "Portrait" })
                if (name.EndsWith(tail, StringComparison.Ordinal)) return name.Substring(0, name.Length - tail.Length);
            return name;
        }

        // Folders of the first export: !WotR_NN_<name>, keyed by the lowercase name.
        Dictionary<string, string> ExistingFolders()
        {
            var found = new Dictionary<string, string>();
            if (!Directory.Exists(m_Portraits)) return found;
            foreach (string path in Directory.GetDirectories(m_Portraits))
            {
                string d = Path.GetFileName(path);
                var parts = d.Split(new[] { '_' }, 3);
                int n;
                if (d.StartsWith("!WotR_", StringComparison.Ordinal) && parts.Length == 3 && int.TryParse(parts[1], out n) && parts[1].All(char.IsDigit))
                    found[parts[2].ToLowerInvariant()] = d;
            }
            return found;
        }
    }

    // Wrath of the Righteous next to Kingmaker: the same Steam library first, then every library Steam knows of,
    // on Windows and under Proton (where Linux paths are seen through drive Z:).
    static class WotrLocator
    {
        const string Folder = "Pathfinder Second Adventure";

        internal static string Find(string kingmakerData, Action<string> log)
        {
            var libraries = new List<string>();
            // ...\steamapps\common\Pathfinder Kingmaker\Kingmaker_Data -> ...\steamapps
            var dir = new DirectoryInfo(kingmakerData);
            for (var d = dir; d != null; d = d.Parent)
                if (string.Equals(d.Name, "steamapps", StringComparison.OrdinalIgnoreCase)) { libraries.Add(d.Parent.FullName); break; }

            var vdfs = new List<string>();
            foreach (string lib in libraries.ToArray())
                vdfs.Add(Path.Combine(Path.Combine(lib, "steamapps"), "libraryfolders.vdf"));
            try
            {
                var steam = Microsoft.Win32.Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath", null) as string;
                if (!string.IsNullOrEmpty(steam)) vdfs.Add(Path.Combine(Path.Combine(steam.Replace('/', '\\'), "steamapps"), "libraryfolders.vdf"));
            }
            catch { }
            try
            {
                if (Directory.Exists(@"Z:\home"))
                    foreach (string home in Directory.GetDirectories(@"Z:\home"))
                        vdfs.Add(Path.Combine(home, @".local\share\Steam\steamapps\libraryfolders.vdf"));
            }
            catch { }

            foreach (string vdf in vdfs)
            {
                try
                {
                    if (!File.Exists(vdf)) continue;
                    foreach (Match m in Regex.Matches(File.ReadAllText(vdf), "\"path\"\\s+\"([^\"]+)\""))
                    {
                        string p = m.Groups[1].Value.Replace(@"\\", @"\");
                        if (p.StartsWith("/", StringComparison.Ordinal)) p = "Z:" + p.Replace('/', '\\');
                        if (!libraries.Contains(p)) libraries.Add(p);
                    }
                }
                catch (Exception e) { log("wotr export: " + vdf + ": " + e.Message); }
            }

            foreach (string lib in libraries)
            {
                string game = Path.Combine(Path.Combine(Path.Combine(lib, "steamapps"), "common"), Folder);
                if (Complete(game)) return game;
            }
            log("wotr export: looked in " + string.Join("; ", libraries.ToArray()));
            return null;
        }

        static bool Complete(string game)
        {
            try
            {
                return File.Exists(Path.Combine(game, "blueprints.zip")) && File.Exists(Path.Combine(Path.Combine(game, "Bundles"), "portraits"));
            }
            catch { return false; }
        }
    }
}
