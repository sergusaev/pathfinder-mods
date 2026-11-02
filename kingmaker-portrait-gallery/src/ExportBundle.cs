using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace PortraitGallery.Export
{
    // A Unity asset bundle (UnityFS) of WotR, read without Unity: Kingmaker's Unity 2018 cannot load a 2020 bundle.
    // Blocks are LZ4 (or stored) and are decompressed only when read, one block kept at a time.
    sealed class Bundle : IDisposable
    {
        struct Block { public long FileOffset, DataOffset; public int Packed, Size; public int Flags; }
        internal struct Node { public long Offset, Size; public string Path; }

        readonly FileStream m_File;
        readonly List<Block> m_Blocks = new List<Block>();
        internal readonly List<Node> Nodes = new List<Node>();
        int m_Cached = -1;
        byte[] m_CachedData;

        internal Bundle(string path)
        {
            m_File = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 16);
            var r = new BigReader(m_File);
            string signature = r.CString();
            if (signature != "UnityFS") throw new InvalidDataException("not a UnityFS bundle: " + signature);
            int version = r.Int32();
            r.CString(); r.CString();
            r.Int64();
            int packedInfo = r.Int32(), infoSize = r.Int32(), flags = r.Int32();
            if (version >= 7) Align(16);
            long headerEnd = m_File.Position;
            long infoAt = (flags & 0x80) != 0 ? m_File.Length - packedInfo : headerEnd;
            m_File.Position = infoAt;
            byte[] info = Unpack(r.Bytes(packedInfo), infoSize, flags & 0x3F);
            long dataAt = (flags & 0x80) != 0 ? headerEnd : infoAt + packedInfo;
            if ((flags & 0x200) != 0) dataAt = (dataAt + 15) & ~15L;

            var ir = new BigReader(new MemoryStream(info));
            ir.Bytes(16);
            int blocks = ir.Int32();
            long fileOffset = dataAt, dataOffset = 0;
            for (int i = 0; i < blocks; i++)
            {
                var b = new Block { Size = ir.Int32(), Packed = ir.Int32(), Flags = ir.UInt16(), FileOffset = fileOffset, DataOffset = dataOffset };
                fileOffset += b.Packed; dataOffset += b.Size;
                m_Blocks.Add(b);
            }
            int nodes = ir.Int32();
            for (int i = 0; i < nodes; i++)
            {
                var n = new Node { Offset = ir.Int64(), Size = ir.Int64() };
                ir.Int32();
                n.Path = ir.CString();
                Nodes.Add(n);
            }
        }

        void Align(int n)
        {
            long p = m_File.Position;
            m_File.Position = (p + n - 1) / n * n;
        }

        static byte[] Unpack(byte[] data, int size, int compression)
        {
            switch (compression)
            {
                case 0: return data;
                case 2:
                case 3: return Lz4.Decode(data, size);
                default: throw new NotSupportedException("bundle compression " + compression);
            }
        }

        internal Node Find(Func<string, bool> match)
        {
            foreach (var n in Nodes) if (match(n.Path)) return n;
            throw new InvalidDataException("no file in the bundle");
        }

        // Bytes of the bundle's data stream (all its files one after another).
        internal byte[] Read(long offset, int count)
        {
            var result = new byte[count];
            int done = 0;
            int i = BlockAt(offset);
            while (done < count)
            {
                var b = m_Blocks[i];
                byte[] data = BlockData(i);
                int from = (int)(offset + done - b.DataOffset);
                int n = Math.Min(count - done, b.Size - from);
                Buffer.BlockCopy(data, from, result, done, n);
                done += n;
                i++;
            }
            return result;
        }

        int BlockAt(long offset)
        {
            int lo = 0, hi = m_Blocks.Count - 1;
            while (lo < hi)
            {
                int mid = (lo + hi + 1) / 2;
                if (m_Blocks[mid].DataOffset <= offset) lo = mid; else hi = mid - 1;
            }
            return lo;
        }

        byte[] BlockData(int i)
        {
            if (i == m_Cached) return m_CachedData;
            var b = m_Blocks[i];
            m_File.Position = b.FileOffset;
            var packed = new byte[b.Packed];
            ReadAll(m_File, packed);
            m_CachedData = Unpack(packed, b.Size, b.Flags & 0x3F);
            m_Cached = i;
            return m_CachedData;
        }

        internal static void ReadAll(Stream s, byte[] buffer)
        {
            int done = 0;
            while (done < buffer.Length)
            {
                int n = s.Read(buffer, done, buffer.Length - done);
                if (n <= 0) throw new EndOfStreamException();
                done += n;
            }
        }

        public void Dispose() { m_File.Dispose(); }
    }

    static class Lz4
    {
        internal static byte[] Decode(byte[] src, int size)
        {
            var dst = new byte[size];
            int s = 0, d = 0;
            while (s < src.Length)
            {
                int token = src[s++];
                int literals = token >> 4;
                if (literals == 15) { int b; do { b = src[s++]; literals += b; } while (b == 255); }
                Buffer.BlockCopy(src, s, dst, d, literals);
                s += literals; d += literals;
                if (s >= src.Length) break;
                int offset = src[s] | (src[s + 1] << 8);
                s += 2;
                int match = token & 15;
                if (match == 15) { int b; do { b = src[s++]; match += b; } while (b == 255); }
                match += 4;
                int from = d - offset;
                for (int k = 0; k < match; k++) dst[d + k] = dst[from + k];
                d += match;
            }
            if (d != size) throw new InvalidDataException("LZ4: " + d + " of " + size + " bytes");
            return dst;
        }
    }

    class BigReader
    {
        readonly Stream m_S;
        readonly byte[] m_B = new byte[8];
        internal BigReader(Stream s) { m_S = s; }

        byte[] Take(int n) { int done = 0; while (done < n) { int k = m_S.Read(m_B, done, n - done); if (k <= 0) throw new EndOfStreamException(); done += k; } return m_B; }
        internal int Int32() { var b = Take(4); return (b[0] << 24) | (b[1] << 16) | (b[2] << 8) | b[3]; }
        internal int UInt16() { var b = Take(2); return (b[0] << 8) | b[1]; }
        internal long Int64() { long hi = (uint)Int32(); long lo = (uint)Int32(); return (hi << 32) | lo; }
        internal byte[] Bytes(int n) { var r = new byte[n]; Bundle.ReadAll(m_S, r); return r; }
        internal string CString()
        {
            var sb = new StringBuilder();
            int c;
            while ((c = m_S.ReadByte()) > 0) sb.Append((char)c);
            return sb.ToString();
        }
    }
}
