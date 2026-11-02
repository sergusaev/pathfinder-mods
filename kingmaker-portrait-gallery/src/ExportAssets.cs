using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace PortraitGallery.Export
{
    // A Unity serialized file (format 22, Unity 2020) with type trees: objects are read field by field into
    // dictionaries, as the type tree describes them. Only what the export needs is read.
    sealed class Assets
    {
        internal class TypeNode
        {
            public string Type, Name;
            public int Size, Level, Meta;
            public readonly List<TypeNode> Children = new List<TypeNode>();
        }

        internal struct ObjectInfo { public long PathId, Start; public int Size, ClassId; public TypeNode Tree; }

        readonly byte[] m_Data;
        internal readonly Dictionary<long, ObjectInfo> Objects = new Dictionary<long, ObjectInfo>();

        static Dictionary<int, string> s_Common;

        static string Common(int offset)
        {
            if (s_Common == null)
            {
                var map = new Dictionary<int, string>();
                int at = 0;
                foreach (string s in CommonStrings.Buffer.Split('\0')) { map[at] = s; at += s.Length + 1; }
                s_Common = map;
            }
            string r;
            return s_Common.TryGetValue(offset, out r) ? r : offset.ToString();
        }

        internal Assets(byte[] data)
        {
            m_Data = data;
            var r = new Reader(data, 0, true);
            r.Int32(); r.Int32();
            int version = r.Int32();
            r.Int32();
            if (version < 22) throw new NotSupportedException("serialized file format " + version);
            bool big = r.Byte() != 0;
            r.Pos += 3;
            r.Int32(); r.Int64();
            long dataOffset = r.Int64();
            r.Int64();
            r.Big = big;
            r.CString();
            r.Int32();
            bool trees = r.Byte() != 0;
            if (!trees) throw new NotSupportedException("no type trees");
            int typeCount = r.Int32();
            var types = new List<KeyValuePair<int, TypeNode>>();
            for (int i = 0; i < typeCount; i++)
            {
                int classId = r.Int32();
                r.Byte();
                r.Int16();
                if (classId == 114) r.Pos += 16;
                r.Pos += 16;
                types.Add(new KeyValuePair<int, TypeNode>(classId, ReadTree(r)));
                int deps = r.Int32();
                r.Pos += deps * 4;
            }
            int count = r.Int32();
            for (int i = 0; i < count; i++)
            {
                r.Align(4);
                var o = new ObjectInfo { PathId = r.Int64(), Start = r.Int64() + dataOffset, Size = r.Int32() };
                int type = r.Int32();
                o.ClassId = types[type].Key;
                o.Tree = types[type].Value;
                Objects[o.PathId] = o;
            }
        }

        static TypeNode ReadTree(Reader r)
        {
            int nodes = r.Int32(), strings = r.Int32();
            var raw = new int[nodes, 6];
            for (int i = 0; i < nodes; i++)
            {
                r.Int16();                 // version
                raw[i, 0] = r.Byte();      // level
                r.Byte();                  // type flags
                raw[i, 1] = r.Int32();     // type name
                raw[i, 2] = r.Int32();     // name
                raw[i, 3] = r.Int32();     // byte size
                r.Int32();                 // index
                raw[i, 4] = r.Int32();     // meta flags
                r.Int64();                 // ref type hash
            }
            int buffer = r.Pos;
            r.Pos += strings;
            var stack = new List<TypeNode>();
            TypeNode root = null;
            for (int i = 0; i < nodes; i++)
            {
                var n = new TypeNode
                {
                    Level = raw[i, 0], Type = Name(r, buffer, raw[i, 1]), Name = Name(r, buffer, raw[i, 2]),
                    Size = raw[i, 3], Meta = raw[i, 4]
                };
                if (root == null) root = n;
                else stack[n.Level - 1].Children.Add(n);
                while (stack.Count > n.Level) stack.RemoveAt(stack.Count - 1);
                stack.Add(n);
            }
            return root;
        }

        static string Name(Reader r, int buffer, int offset)
        {
            if ((offset & 0x80000000) != 0) return Common(offset & 0x7FFFFFFF);
            return r.CStringAt(buffer + offset);
        }

        internal int ClassOf(long pathId)
        {
            ObjectInfo o;
            return Objects.TryGetValue(pathId, out o) ? o.ClassId : -1;
        }

        internal Dictionary<string, object> Read(long pathId)
        {
            var o = Objects[pathId];
            var r = new Reader(m_Data, (int)o.Start, false);
            return (Dictionary<string, object>)Value(o.Tree, r);
        }

        static object Value(TypeNode n, Reader r)
        {
            object v;
            switch (n.Type)
            {
                case "SInt8": v = (long)(sbyte)r.Byte(); break;
                case "UInt8": case "char": v = (long)r.Byte(); break;
                case "bool": v = r.Byte() != 0; break;
                case "SInt16": case "short": v = (long)r.Int16(); break;
                case "UInt16": case "unsigned short": v = (long)(ushort)r.Int16(); break;
                case "SInt32": case "int": case "Type*": v = (long)r.Int32(); break;
                case "UInt32": case "unsigned int": v = (long)(uint)r.Int32(); break;
                case "SInt64": case "long long": case "UInt64": case "unsigned long long": case "FileSize": v = r.Int64(); break;
                case "float": v = (double)r.Float(); break;
                case "double": v = BitConverter.Int64BitsToDouble(r.Int64()); break;
                case "string":
                    {
                        int len = r.Int32();
                        v = Encoding.UTF8.GetString(r.Data, r.Pos, len);
                        r.Pos += len;
                        if (n.Children.Count > 0 && (n.Children[0].Meta & 0x4000) != 0) r.Align(4);
                        break;
                    }
                case "TypelessData":
                    {
                        int len = r.Int32();
                        var b = new byte[len];
                        Buffer.BlockCopy(r.Data, r.Pos, b, 0, len);
                        r.Pos += len;
                        v = b;
                        break;
                    }
                default:
                    if (n.Children.Count > 0 && n.Children[0].Type == "Array") { v = Array(n.Children[0], r); break; }
                    if (n.Type == "Array") { v = Array(n, r); break; }
                    var d = new Dictionary<string, object>();
                    foreach (var c in n.Children) d[c.Name] = Value(c, r);
                    v = d;
                    break;
            }
            if ((n.Meta & 0x4000) != 0) r.Align(4);
            return v;
        }

        static object Array(TypeNode array, Reader r)
        {
            int count = r.Int32();
            var item = array.Children[1];
            object v;
            if (item.Size == 1 && item.Children.Count == 0)
            {
                var b = new byte[count];
                Buffer.BlockCopy(r.Data, r.Pos, b, 0, count);
                r.Pos += count;
                v = b;
            }
            else
            {
                var list = new List<object>(count);
                for (int i = 0; i < count; i++) list.Add(Value(item, r));
                v = list;
            }
            if ((array.Meta & 0x4000) != 0) r.Align(4);
            return v;
        }

        internal class Reader
        {
            internal readonly byte[] Data;
            internal int Pos;
            internal bool Big;
            internal Reader(byte[] data, int pos, bool big) { Data = data; Pos = pos; Big = big; }

            internal byte Byte() { return Data[Pos++]; }
            internal short Int16() { int a = Data[Pos], b = Data[Pos + 1]; Pos += 2; return (short)(Big ? (a << 8) | b : (b << 8) | a); }
            internal int Int32()
            {
                int a = Data[Pos], b = Data[Pos + 1], c = Data[Pos + 2], d = Data[Pos + 3];
                Pos += 4;
                return Big ? (a << 24) | (b << 16) | (c << 8) | d : (d << 24) | (c << 16) | (b << 8) | a;
            }
            internal long Int64()
            {
                long x = (uint)Int32(), y = (uint)Int32();
                return Big ? (x << 32) | y : (y << 32) | x;
            }
            internal float Float() { int i = Int32(); return BitConverter.ToSingle(BitConverter.GetBytes(i), 0); }
            internal void Align(int n) { Pos = (Pos + n - 1) / n * n; }
            internal string CString()
            {
                string s = CStringAt(Pos);
                Pos += Encoding.UTF8.GetByteCount(s) + 1;
                return s;
            }
            internal string CStringAt(int at)
            {
                int end = at;
                while (Data[end] != 0) end++;
                return Encoding.UTF8.GetString(Data, at, end - at);
            }
        }
    }
}
