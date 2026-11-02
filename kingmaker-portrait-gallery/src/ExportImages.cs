using System;
using System.IO;
using System.Text;

namespace PortraitGallery.Export
{
    // RGBA pixels, top row first.
    sealed class Picture
    {
        internal readonly int Width, Height;
        internal readonly byte[] Rgba;
        internal Picture(int w, int h) { Width = w; Height = h; Rgba = new byte[w * h * 4]; }
    }

    static class Images
    {
        // Texture formats of WotR's portraits bundle (UnityEngine.TextureFormat).
        const int RGB24 = 3, RGBA32 = 4, DXT1 = 10, DXT5 = 12;

        // The texture's top mip as a picture, top row first (Unity keeps the bottom row first).
        internal static Picture Decode(byte[] data, int format, int w, int h)
        {
            var p = new Picture(w, h);
            switch (format)
            {
                case RGB24:
                case RGBA32:
                    {
                        int bpp = format == RGB24 ? 3 : 4;
                        for (int y = 0; y < h; y++)
                            for (int x = 0; x < w; x++)
                            {
                                int s = (y * w + x) * bpp, d = ((h - 1 - y) * w + x) * 4;
                                p.Rgba[d] = data[s]; p.Rgba[d + 1] = data[s + 1]; p.Rgba[d + 2] = data[s + 2];
                                p.Rgba[d + 3] = bpp == 4 ? data[s + 3] : (byte)255;
                            }
                        break;
                    }
                case DXT1:
                case DXT5:
                    Dxt(data, format == DXT5, p);
                    break;
                default:
                    throw new NotSupportedException("texture format " + format);
            }
            return p;
        }

        static void Dxt(byte[] data, bool dxt5, Picture p)
        {
            int w = p.Width, h = p.Height, bw = (w + 3) / 4, bh = (h + 3) / 4, size = dxt5 ? 16 : 8;
            var colors = new byte[16];
            var alphas = new byte[8];
            for (int by = 0; by < bh; by++)
                for (int bx = 0; bx < bw; bx++)
                {
                    int at = (by * bw + bx) * size;
                    ulong alphaBits = 0;
                    if (dxt5)
                    {
                        alphas[0] = data[at]; alphas[1] = data[at + 1];
                        if (alphas[0] > alphas[1])
                            for (int i = 1; i < 7; i++) alphas[i + 1] = (byte)(((7 - i) * alphas[0] + i * alphas[1]) / 7);
                        else
                        {
                            for (int i = 1; i < 5; i++) alphas[i + 1] = (byte)(((5 - i) * alphas[0] + i * alphas[1]) / 5);
                            alphas[6] = 0; alphas[7] = 255;
                        }
                        for (int i = 0; i < 6; i++) alphaBits |= (ulong)data[at + 2 + i] << (8 * i);
                        at += 8;
                    }
                    int c0 = data[at] | (data[at + 1] << 8), c1 = data[at + 2] | (data[at + 3] << 8);
                    Rgb565(c0, colors, 0); Rgb565(c1, colors, 4);
                    bool four = c0 > c1 || dxt5;
                    for (int k = 0; k < 3; k++)
                    {
                        if (four)
                        {
                            colors[8 + k] = (byte)((2 * colors[k] + colors[4 + k]) / 3);
                            colors[12 + k] = (byte)((colors[k] + 2 * colors[4 + k]) / 3);
                        }
                        else
                        {
                            colors[8 + k] = (byte)((colors[k] + colors[4 + k]) / 2);
                            colors[12 + k] = 0;
                        }
                    }
                    colors[3] = colors[7] = colors[11] = 255;
                    colors[15] = (byte)(four ? 255 : 0);
                    uint bits = (uint)(data[at + 4] | (data[at + 5] << 8) | (data[at + 6] << 16) | (data[at + 7] << 24));
                    for (int py = 0; py < 4; py++)
                        for (int px = 0; px < 4; px++)
                        {
                            int x = bx * 4 + px, y = by * 4 + py;
                            if (x >= w || y >= h) continue;
                            int i = py * 4 + px;
                            int c = (int)((bits >> (2 * i)) & 3) * 4;
                            int d = ((h - 1 - y) * w + x) * 4;
                            p.Rgba[d] = colors[c]; p.Rgba[d + 1] = colors[c + 1]; p.Rgba[d + 2] = colors[c + 2];
                            p.Rgba[d + 3] = dxt5 ? alphas[(int)((alphaBits >> (3 * i)) & 7)] : colors[c + 3];
                        }
                }
        }

        static void Rgb565(int c, byte[] to, int at)
        {
            int r = (c >> 11) & 31, g = (c >> 5) & 63, b = c & 31;
            to[at] = (byte)((r << 3) | (r >> 2)); to[at + 1] = (byte)((g << 2) | (g >> 4)); to[at + 2] = (byte)((b << 3) | (b >> 2));
        }

        // A rectangle given in Unity's texture coordinates (from the bottom).
        internal static Picture Crop(Picture p, int x, int yFromBottom, int w, int h)
        {
            if (x == 0 && yFromBottom == 0 && w == p.Width && h == p.Height) return p;
            var r = new Picture(w, h);
            int top = p.Height - yFromBottom - h;
            for (int y = 0; y < h; y++)
                Buffer.BlockCopy(p.Rgba, ((top + y) * p.Width + x) * 4, r.Rgba, y * w * 4, w * 4);
            return r;
        }

        // Lanczos-3 resampling, one direction at a time.
        internal static Picture Resize(Picture p, int w, int h)
        {
            if (p.Width == w && p.Height == h) return p;
            int sw = p.Width, sh = p.Height;
            var src = new float[sw * sh * 4];
            for (int i = 0; i < src.Length; i++) src[i] = p.Rgba[i];
            // rows: sw -> w
            var mid = new float[w * sh * 4];
            Pass(src, mid, sw, w, sh, 4, sw * 4, w * 4);
            // columns: sh -> h
            var dst = new float[w * h * 4];
            Pass(mid, dst, sh, h, w, w * 4, 4, 4);
            var r = new Picture(w, h);
            for (int i = 0; i < dst.Length; i++)
            {
                float v = dst[i];
                r.Rgba[i] = (byte)(v <= 0 ? 0 : v >= 255 ? 255 : (int)(v + 0.5f));
            }
            return r;
        }

        // Resamples along one axis: sample k of line j sits at j * lineStep + k * step (channels follow it).
        static void Pass(float[] src, float[] dst, int from, int to, int lines, int step, int srcLine, int dstLine)
        {
            double scale = (double)from / to, stretch = Math.Max(1, scale), support = 3 * stretch;
            for (int d = 0; d < to; d++)
            {
                double center = (d + 0.5) * scale;
                int lo = Math.Max(0, (int)Math.Floor(center - support)), hi = Math.Min(from - 1, (int)Math.Ceiling(center + support));
                var weights = new float[hi - lo + 1];
                double sum = 0;
                for (int s = lo; s <= hi; s++) sum += weights[s - lo] = (float)Lanczos((s + 0.5 - center) / stretch);
                for (int s = 0; s < weights.Length; s++) weights[s] = (float)(weights[s] / sum);
                for (int line = 0; line < lines; line++)
                {
                    float r = 0, g = 0, b = 0, a = 0;
                    int at = line * srcLine + lo * step;
                    for (int s = 0; s < weights.Length; s++, at += step)
                    {
                        float wt = weights[s];
                        r += wt * src[at]; g += wt * src[at + 1]; b += wt * src[at + 2]; a += wt * src[at + 3];
                    }
                    int o = step == 4 ? line * dstLine + d * 4 : d * (lines * 4) + line * 4;
                    dst[o] = r; dst[o + 1] = g; dst[o + 2] = b; dst[o + 3] = a;
                }
            }
        }

        static double Lanczos(double x)
        {
            x = Math.Abs(x);
            if (x < 1e-8) return 1;
            if (x >= 3) return 0;
            double px = Math.PI * x;
            return 3 * Math.Sin(px) * Math.Sin(px / 3) / (px * px);
        }

        // PNG, RGBA 8 bits, every row filtered by Sub.
        internal static void SavePng(Picture p, string path)
        {
            var raw = new MemoryStream();
            int stride = p.Width * 4;
            for (int y = 0; y < p.Height; y++)
            {
                raw.WriteByte(1);
                int row = y * stride;
                for (int i = 0; i < stride; i++)
                    raw.WriteByte((byte)(p.Rgba[row + i] - (i >= 4 ? p.Rgba[row + i - 4] : 0)));
            }
            byte[] packed = Zlib(raw.ToArray());
            using (var f = new FileStream(path, FileMode.Create, FileAccess.Write))
            {
                f.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, 0, 8);
                var ihdr = new byte[13];
                Put32(ihdr, 0, p.Width); Put32(ihdr, 4, p.Height);
                ihdr[8] = 8; ihdr[9] = 6;
                Chunk(f, "IHDR", ihdr);
                Chunk(f, "IDAT", packed);
                Chunk(f, "IEND", new byte[0]);
            }
        }

        static byte[] Zlib(byte[] data)
        {
            var o = new MemoryStream();
            using (var z = new Ionic.Zlib.ZlibStream(o, Ionic.Zlib.CompressionMode.Compress, Ionic.Zlib.CompressionLevel.BestSpeed, true))
                z.Write(data, 0, data.Length);
            return o.ToArray();
        }

        static void Put32(byte[] b, int at, int v) { b[at] = (byte)(v >> 24); b[at + 1] = (byte)(v >> 16); b[at + 2] = (byte)(v >> 8); b[at + 3] = (byte)v; }

        static uint[] s_Crc;
        static uint Crc(byte[] type, byte[] data)
        {
            if (s_Crc == null)
            {
                var t = new uint[256];
                for (uint n = 0; n < 256; n++)
                {
                    uint c = n;
                    for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
                    t[n] = c;
                }
                s_Crc = t;
            }
            uint crc = 0xFFFFFFFF;
            foreach (byte b in type) crc = s_Crc[(crc ^ b) & 0xFF] ^ (crc >> 8);
            foreach (byte b in data) crc = s_Crc[(crc ^ b) & 0xFF] ^ (crc >> 8);
            return crc ^ 0xFFFFFFFF;
        }

        static void Chunk(Stream s, string type, byte[] data)
        {
            var head = new byte[4];
            Put32(head, 0, data.Length);
            s.Write(head, 0, 4);
            byte[] t = Encoding.ASCII.GetBytes(type);
            s.Write(t, 0, 4);
            s.Write(data, 0, data.Length);
            Put32(head, 0, (int)Crc(t, data));
            s.Write(head, 0, 4);
        }
    }
}
