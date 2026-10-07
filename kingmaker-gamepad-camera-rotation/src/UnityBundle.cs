using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace GamepadCameraRotation
{
    // Minimal read-only UnityFS bundle reader: LZ4 blocks, one serialized file with type trees, raw .resS streams.
    // Enough to pull sprites out of a newer game's bundles that this Unity version cannot load itself.
    sealed class UnityBundle : IDisposable
    {
        struct Block { public long Offset; public long FileOffset; public int Size; public int CompressedSize; public int Flags; }

        public sealed class Node { public long Offset; public long Size; public int Flags; public string Path; }

        readonly FileStream m_File;
        readonly List<Block> m_Blocks = new List<Block>();
        public readonly List<Node> Nodes = new List<Node>();
        int m_CachedBlock = -1;
        byte[] m_Cache;

        public UnityBundle(string path)
        {
            m_File = File.OpenRead(path);
            var r = new BigEndianReader(m_File);
            if (r.CString() != "UnityFS") throw new InvalidDataException("not a UnityFS bundle");
            int version = r.Int32();
            r.CString();
            r.CString();
            r.Int64();
            int infoCompressed = r.Int32();
            int infoSize = r.Int32();
            int flags = r.Int32();
            if (version >= 7) Align(16);

            byte[] info;
            if ((flags & 0x80) != 0)
            {
                long back = m_File.Position;
                m_File.Position = m_File.Length - infoCompressed;
                info = Decompress(r.Bytes(infoCompressed), infoSize, flags);
                m_File.Position = back;
            }
            else
            {
                info = Decompress(r.Bytes(infoCompressed), infoSize, flags);
            }
            if ((flags & 0x200) != 0) Align(16);
            long dataStart = m_File.Position;

            var ir = new BigEndianReader(new MemoryStream(info));
            ir.Bytes(16);
            int blocks = ir.Int32();
            long offset = 0, fileOffset = dataStart;
            for (int i = 0; i < blocks; i++)
            {
                var b = new Block { Offset = offset, FileOffset = fileOffset, Size = ir.Int32(), CompressedSize = ir.Int32(), Flags = ir.Int16() };
                m_Blocks.Add(b);
                offset += b.Size;
                fileOffset += b.CompressedSize;
            }
            int nodes = ir.Int32();
            for (int i = 0; i < nodes; i++)
                Nodes.Add(new Node { Offset = ir.Int64(), Size = ir.Int64(), Flags = ir.Int32(), Path = ir.CString() });
        }

        void Align(int n)
        {
            long pad = (n - m_File.Position % n) % n;
            m_File.Position += pad;
        }

        static byte[] Decompress(byte[] src, int size, int flags)
        {
            switch (flags & 0x3F)
            {
                case 0: return src;
                case 2:
                case 3: return Lz4.Decode(src, size);
                default: throw new NotSupportedException("bundle compression " + (flags & 0x3F));
            }
        }

        public Node Find(Func<Node, bool> match)
        {
            foreach (var n in Nodes) if (match(n)) return n;
            return null;
        }

        // Reads bytes of the uncompressed data stream.
        public byte[] Read(long offset, int count)
        {
            var result = new byte[count];
            int done = 0;
            int i = BlockAt(offset);
            while (done < count)
            {
                byte[] data = BlockData(i);
                long inBlock = offset + done - m_Blocks[i].Offset;
                int n = (int)Math.Min(data.Length - inBlock, count - done);
                Buffer.BlockCopy(data, (int)inBlock, result, done, n);
                done += n;
                i++;
            }
            return result;
        }

        public byte[] ReadNode(Node node, long offset, int count)
        {
            return Read(node.Offset + offset, count);
        }

        int BlockAt(long offset)
        {
            int lo = 0, hi = m_Blocks.Count - 1;
            while (lo < hi)
            {
                int mid = (lo + hi + 1) / 2;
                if (m_Blocks[mid].Offset <= offset) lo = mid; else hi = mid - 1;
            }
            return lo;
        }

        byte[] BlockData(int i)
        {
            if (i == m_CachedBlock) return m_Cache;
            Block b = m_Blocks[i];
            m_File.Position = b.FileOffset;
            var raw = new byte[b.CompressedSize];
            ReadFully(m_File, raw);
            m_Cache = Decompress(raw, b.Size, b.Flags);
            m_CachedBlock = i;
            return m_Cache;
        }

        static void ReadFully(Stream s, byte[] buf)
        {
            int done = 0;
            while (done < buf.Length)
            {
                int n = s.Read(buf, done, buf.Length - done);
                if (n <= 0) throw new EndOfStreamException();
                done += n;
            }
        }

        public void Dispose()
        {
            m_File.Dispose();
        }

        sealed class BigEndianReader
        {
            readonly Stream m_S;
            readonly byte[] m_Buf = new byte[8];
            public BigEndianReader(Stream s) { m_S = s; }

            byte[] Take(int n)
            {
                int done = 0;
                while (done < n)
                {
                    int k = m_S.Read(m_Buf, done, n - done);
                    if (k <= 0) throw new EndOfStreamException();
                    done += k;
                }
                return m_Buf;
            }

            public int Int16() { var b = Take(2); return (b[0] << 8) | b[1]; }
            public int Int32() { var b = Take(4); return (b[0] << 24) | (b[1] << 16) | (b[2] << 8) | b[3]; }
            public long Int64() { long hi = (uint)Int32(); return (hi << 32) | (uint)Int32(); }

            public byte[] Bytes(int n)
            {
                var r = new byte[n];
                ReadFully(m_S, r);
                return r;
            }

            public string CString()
            {
                var sb = new StringBuilder();
                int c;
                while ((c = m_S.ReadByte()) > 0) sb.Append((char)c);
                return sb.ToString();
            }
        }
    }

    static class Lz4
    {
        public static byte[] Decode(byte[] src, int size)
        {
            var dst = new byte[size];
            int s = 0, d = 0;
            while (s < src.Length)
            {
                int token = src[s++];
                int lit = token >> 4;
                if (lit == 15)
                {
                    int b;
                    do { b = src[s++]; lit += b; } while (b == 255);
                }
                Buffer.BlockCopy(src, s, dst, d, lit);
                s += lit;
                d += lit;
                if (s >= src.Length) break;
                int back = src[s] | (src[s + 1] << 8);
                s += 2;
                int len = token & 15;
                if (len == 15)
                {
                    int b;
                    do { b = src[s++]; len += b; } while (b == 255);
                }
                len += 4;
                int from = d - back;
                for (int i = 0; i < len; i++) dst[d++] = dst[from + i];
            }
            if (d != size) throw new InvalidDataException("LZ4 size mismatch");
            return dst;
        }
    }

    // Serialized file (format 22) with embedded type trees; objects are read into dictionaries, lists and primitives.
    sealed class SerializedFile
    {
        const string CommonStrings = "AABB\0AnimationClip\0AnimationCurve\0AnimationState\0Array\0Base\0BitField\0bitset\0bool\0char\0ColorRGBA\0Component\0data\0deque\0double\0dynamic_array\0FastPropertyName\0first\0float\0Font\0GameObject\0Generic Mono\0GradientNEW\0GUID\0GUIStyle\0int\0list\0long long\0map\0Matrix4x4f\0MdFour\0MonoBehaviour\0MonoScript\0m_ByteSize\0m_Curve\0m_EditorClassIdentifier\0m_EditorHideFlags\0m_Enabled\0m_ExtensionPtr\0m_GameObject\0m_Index\0m_IsArray\0m_IsStatic\0m_MetaFlag\0m_Name\0m_ObjectHideFlags\0m_PrefabInternal\0m_PrefabParentObject\0m_Script\0m_StaticEditorFlags\0m_Type\0m_Version\0Object\0pair\0PPtr<Component>\0PPtr<GameObject>\0PPtr<Material>\0PPtr<MonoBehaviour>\0PPtr<MonoScript>\0PPtr<Object>\0PPtr<Prefab>\0PPtr<Sprite>\0PPtr<TextAsset>\0PPtr<Texture>\0PPtr<Texture2D>\0PPtr<Transform>\0Prefab\0Quaternionf\0Rectf\0RectInt\0RectOffset\0second\0set\0short\0size\0SInt16\0SInt32\0SInt64\0SInt8\0staticvector\0string\0TextAsset\0TextMesh\0Texture\0Texture2D\0Transform\0TypelessData\0UInt16\0UInt32\0UInt64\0UInt8\0unsigned int\0unsigned long long\0unsigned short\0vector\0Vector2f\0Vector3f\0Vector4f\0m_ScriptingClassIdentifier\0Gradient\0Type*\0int2_storage\0int3_storage\0BoundsInt\0m_CorrespondingSourceObject\0m_PrefabInstance\0m_PrefabAsset\0FileSize\0Hash128\0RenderingLayerMask\0fixed_array\0EntityId\0LoadableObjectId\0LoadableSceneId\0";

        public const int ClassTexture2D = 28;
        public const int ClassSprite = 213;
        public const int ClassSpriteAtlas = 687078895;

        internal sealed class TypeNode { public string Type; public string Name; public int Level; public int MetaFlag; }

        internal sealed class SerializedType { public int ClassId; public List<TypeNode> Nodes; }

        public sealed class ObjectInfo { public long PathId; public long Start; public int Size; public int ClassId; internal SerializedType Type; }

        readonly byte[] m_Data;
        public readonly Dictionary<long, ObjectInfo> Objects = new Dictionary<long, ObjectInfo>();

        public SerializedFile(byte[] data)
        {
            m_Data = data;
            var r = new Reader(data, true);
            r.Int32();
            r.Int32();
            int version = r.Int32();
            r.Int32();
            if (version < 22) throw new NotSupportedException("serialized file version " + version);
            bool big = r.Byte() != 0;
            r.Pos += 3;
            r.Int32();
            r.Int64();
            long dataOffset = r.Int64();
            r.Int64();
            r.Big = big;

            r.CString();
            r.Int32();
            bool typeTree = r.Byte() != 0;
            if (!typeTree) throw new NotSupportedException("bundle has no type trees");
            int typeCount = r.Int32();
            var types = new List<SerializedType>();
            for (int i = 0; i < typeCount; i++)
            {
                var t = new SerializedType { ClassId = r.Int32() };
                r.Byte();
                r.Int16();
                if (t.ClassId == 114) r.Pos += 16;
                r.Pos += 16;
                t.Nodes = ReadTypeTree(r);
                int deps = r.Int32();
                r.Pos += deps * 4;
                types.Add(t);
            }
            int objectCount = r.Int32();
            for (int i = 0; i < objectCount; i++)
            {
                r.Align(4);
                var o = new ObjectInfo { PathId = r.Int64() };
                o.Start = r.Int64() + dataOffset;
                o.Size = r.Int32();
                o.Type = types[r.Int32()];
                o.ClassId = o.Type.ClassId;
                Objects[o.PathId] = o;
            }
        }

        static List<TypeNode> ReadTypeTree(Reader r)
        {
            int count = r.Int32();
            int stringSize = r.Int32();
            int nodeStart = r.Pos;
            r.Pos += count * 32;
            int strings = r.Pos;
            var nodes = new List<TypeNode>(count);
            int end = r.Pos + stringSize;
            r.Pos = nodeStart;
            for (int i = 0; i < count; i++)
            {
                r.Int16();
                int level = r.Byte();
                r.Byte();
                uint typeOffset = (uint)r.Int32();
                uint nameOffset = (uint)r.Int32();
                r.Int32();
                r.Int32();
                int meta = r.Int32();
                r.Int64();
                nodes.Add(new TypeNode { Level = level, MetaFlag = meta, Type = Str(r, strings, typeOffset), Name = Str(r, strings, nameOffset) });
            }
            r.Pos = end;
            return nodes;
        }

        static string Str(Reader r, int table, uint offset)
        {
            if ((offset & 0x80000000) != 0)
            {
                int o = (int)(offset & 0x7FFFFFFF);
                return CommonStrings.Substring(o, CommonStrings.IndexOf('\0', o) - o);
            }
            int p = r.Pos;
            r.Pos = table + (int)offset;
            string s = r.CString();
            r.Pos = p;
            return s;
        }

        // Objects whose type tree starts with m_Name expose it without a full read.
        public string PeekName(ObjectInfo o)
        {
            var nodes = o.Type.Nodes;
            if (nodes.Count < 2 || nodes[1].Name != "m_Name") return null;
            var r = new Reader(m_Data, false) { Pos = (int)o.Start };
            return r.String();
        }

        public Dictionary<string, object> Read(ObjectInfo o)
        {
            var r = new Reader(m_Data, false) { Pos = (int)o.Start };
            int i = 0;
            return (Dictionary<string, object>)ReadValue(r, o.Type.Nodes, ref i);
        }

        static object ReadValue(Reader r, List<TypeNode> nodes, ref int i)
        {
            TypeNode node = nodes[i];
            bool align = (node.MetaFlag & 0x4000) != 0;
            object value;
            switch (node.Type)
            {
                case "SInt8": value = (sbyte)r.Byte(); break;
                case "UInt8":
                case "char": value = (byte)r.Byte(); break;
                case "bool": value = r.Byte() != 0; break;
                case "SInt16":
                case "short": value = (short)r.Int16(); break;
                case "UInt16":
                case "unsigned short": value = (ushort)r.Int16(); break;
                case "SInt32":
                case "int": value = r.Int32(); break;
                case "UInt32":
                case "unsigned int":
                case "Type*": value = (uint)r.Int32(); break;
                case "SInt64":
                case "long long": value = r.Int64(); break;
                case "UInt64":
                case "unsigned long long":
                case "FileSize": value = (ulong)r.Int64(); break;
                case "float": value = r.Float(); break;
                case "double": value = BitConverter.Int64BitsToDouble(r.Int64()); break;
                case "string":
                    value = r.String();
                    i += 3;
                    align = true;
                    break;
                case "TypelessData":
                {
                    int size = r.Int32();
                    value = r.Bytes(size);
                    i += 2;
                    break;
                }
                default:
                {
                    int end = SubtreeEnd(nodes, i);
                    if (i + 1 < nodes.Count && nodes[i + 1].Type == "Array")
                    {
                        if ((nodes[i + 1].MetaFlag & 0x4000) != 0) align = true;
                        int size = r.Int32();
                        var list = new List<object>(size);
                        for (int k = 0; k < size; k++)
                        {
                            int j = i + 3;
                            list.Add(ReadValue(r, nodes, ref j));
                        }
                        value = list;
                    }
                    else
                    {
                        var dict = new Dictionary<string, object>();
                        int j = i + 1;
                        while (j < end)
                        {
                            string name = nodes[j].Name;
                            dict[name] = ReadValue(r, nodes, ref j);
                            j++;
                        }
                        value = dict;
                    }
                    i = end - 1;
                    break;
                }
            }
            if (align) r.Align(4);
            return value;
        }

        static int SubtreeEnd(List<TypeNode> nodes, int i)
        {
            int level = nodes[i].Level;
            int j = i + 1;
            while (j < nodes.Count && nodes[j].Level > level) j++;
            return j;
        }

        sealed class Reader
        {
            readonly byte[] m_D;
            public int Pos;
            public bool Big;
            public Reader(byte[] d, bool big) { m_D = d; Big = big; }

            public int Byte() { return m_D[Pos++]; }

            public int Int16()
            {
                int v = Big ? (m_D[Pos] << 8) | m_D[Pos + 1] : m_D[Pos] | (m_D[Pos + 1] << 8);
                Pos += 2;
                return v;
            }

            public int Int32()
            {
                int v = Big
                    ? (m_D[Pos] << 24) | (m_D[Pos + 1] << 16) | (m_D[Pos + 2] << 8) | m_D[Pos + 3]
                    : m_D[Pos] | (m_D[Pos + 1] << 8) | (m_D[Pos + 2] << 16) | (m_D[Pos + 3] << 24);
                Pos += 4;
                return v;
            }

            public long Int64()
            {
                long a = (uint)Int32(), b = (uint)Int32();
                return Big ? (a << 32) | b : (b << 32) | a;
            }

            public float Float() { return BitConverter.ToSingle(BitConverter.GetBytes(Int32()), 0); }

            public byte[] Bytes(int n)
            {
                var b = new byte[n];
                Buffer.BlockCopy(m_D, Pos, b, 0, n);
                Pos += n;
                return b;
            }

            public string String()
            {
                int n = Int32();
                string s = Encoding.UTF8.GetString(m_D, Pos, n);
                Pos += n;
                Align(4);
                return s;
            }

            public string CString()
            {
                int start = Pos;
                while (m_D[Pos] != 0) Pos++;
                string s = Encoding.UTF8.GetString(m_D, start, Pos - start);
                Pos++;
                return s;
            }

            public void Align(int n) { Pos = (Pos + n - 1) / n * n; }
        }
    }
}
