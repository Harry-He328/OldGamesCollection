using System;
using UnityEngine;

namespace OldGames.Breakout
{
    /// <summary>
    /// Procedurally drawn sprites in a Windows XP (Luna) look. Nothing is downloaded or imported:
    /// every texture is painted once at runtime with 3x3 supersampling.
    /// </summary>
    public static class BreakoutArt
    {
        static Sprite gloss, outline, circle, ball, ring, folder, lockedFolder, disc, document, star, chevron, cursor, frame, appIcon, bolt, background, danger, pixel;

        public static Sprite Gloss => gloss ??= Make(32, 20, GlossPaint, new Vector4(6, 6, 6, 6));
        public static Sprite Outline => outline ??= Make(32, 20, OutlinePaint, new Vector4(6, 6, 6, 6));
        public static Sprite Circle => circle ??= Make(64, 64, (x, y) => Disk(x, y, 32, 32, 31, Color.white));
        public static Sprite Ball => ball ??= Make(32, 32, BallPaint);
        public static Sprite Ring => ring ??= Make(128, 128, RingPaint);
        public static Sprite Folder => folder ??= Make(48, 48, (x, y) => FolderPaint(x, y, false));
        public static Sprite LockedFolder => lockedFolder ??= Make(48, 48, (x, y) => FolderPaint(x, y, true));
        public static Sprite Disc => disc ??= Make(48, 48, DiscPaint);
        public static Sprite Document => document ??= Make(48, 48, DocumentPaint);
        public static Sprite Star => star ??= Make(32, 32, StarPaint);
        public static Sprite Chevron => chevron ??= Make(20, 12, ChevronPaint);
        public static Sprite Cursor => cursor ??= Make(16, 24, CursorPaint);
        public static Sprite Frame => frame ??= Make(16, 16, (x, y) => InRound(x, y, 0, 0, 16, 16, 4) && !InRound(x, y, 2, 2, 12, 12, 2.5f) ? Color.white : Color.clear, new Vector4(5, 5, 5, 5));
        public static Sprite AppIcon => appIcon ??= Make(32, 32, AppIconPaint);
        public static Sprite Bolt => bolt ??= Make(12, 16, BoltPaint);
        public static Sprite Background => background ??= Make(8, 256, BackgroundPaint);
        public static Sprite Danger => danger ??= Make(8, 64, (x, y) => new Color(1f, .35f, .2f, Mathf.Pow(y / 64f, 2) * .55f));
        public static Sprite Pixel => pixel ??= Make(4, 4, (x, y) => Color.white);

        // ---------- painting helpers (coordinates: pixels, origin top-left, y down) ----------

        static Sprite Make(int w, int h, Func<float, float, Color> paint, Vector4 border = default)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "Breakout " + paint.Method.Name };
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    Color acc = default;
                    for (int sy = 0; sy < 3; sy++)
                        for (int sx = 0; sx < 3; sx++)
                        {
                            Color c = paint(x + (sx + .5f) / 3f, y + (sy + .5f) / 3f);
                            acc += new Color(c.r * c.a, c.g * c.a, c.b * c.a, c.a);   // premultiply while averaging
                        }
                    acc /= 9f;
                    if (acc.a > 1e-4f) acc = new Color(acc.r / acc.a, acc.g / acc.a, acc.b / acc.a, acc.a);
                    px[(h - 1 - y) * w + x] = acc;   // flip to Unity's bottom-up rows
                }
            tex.SetPixels(px);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect, border);
        }

        static Color Hex(uint rgb, float a = 1) => new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, a);

        static bool InRound(float x, float y, float rx, float ry, float rw, float rh, float r)
        {
            if (x < rx || y < ry || x > rx + rw || y > ry + rh) return false;
            float cx = Mathf.Clamp(x, rx + r, rx + rw - r), cy = Mathf.Clamp(y, ry + r, ry + rh - r);
            float dx = x - cx, dy = y - cy;
            return dx * dx + dy * dy <= r * r;
        }

        static Color Disk(float x, float y, float cx, float cy, float r, Color c)
        {
            float dx = x - cx, dy = y - cy;
            return dx * dx + dy * dy <= r * r ? c : Color.clear;
        }

        // ---------- sprites ----------

        static Color GlossPaint(float x, float y)
        {
            const float w = 32, h = 20;
            if (!InRound(x, y, 0, 0, w, h, 4)) return Color.clear;
            bool edge = !InRound(x, y, 1, 1, w - 2, h - 2, 3);
            if (edge) return new Color(.5f, .5f, .5f, 1);
            float t = y / h;
            float v = Mathf.Lerp(1f, .72f, t);
            Color c = new Color(v, v, v, 1);
            if (y < h * .45f) c = Color.Lerp(c, Color.white, .55f * (1 - y / (h * .45f)) + .15f);
            if (y > h - 3) c *= .9f;
            c.a = 1;
            return c;
        }

        static Color OutlinePaint(float x, float y)
        {
            const float w = 32, h = 20;
            if (!InRound(x, y, 0, 0, w, h, 4)) return Color.clear;
            if (!InRound(x, y, 1.5f, 1.5f, w - 3, h - 3, 3)) return Color.white;
            // dashed inner hatch gives the "not quite there" look
            return ((int)((x + y) / 3) % 2 == 0) ? new Color(1, 1, 1, .22f) : new Color(1, 1, 1, .06f);
        }

        static Color BallPaint(float x, float y)
        {
            float dx = x - 16, dy = y - 16, d = Mathf.Sqrt(dx * dx + dy * dy);
            if (d > 15) return Color.clear;
            float hl = Mathf.Sqrt((x - 11) * (x - 11) + (y - 10) * (y - 10));
            Color c = Color.Lerp(Color.white, Hex(0x7FA2E0), Mathf.Clamp01(hl / 20f));
            if (d > 13.6f) c = Hex(0x3B5FAE);
            return c;
        }

        static Color RingPaint(float x, float y)
        {
            float dx = x - 64, dy = y - 64, d = Mathf.Sqrt(dx * dx + dy * dy);
            return d <= 62 && d >= 47 ? Color.white : Color.clear;
        }

        static Color FolderPaint(float x, float y, bool locked)
        {
            Color c = Color.clear;
            Color back = locked ? Hex(0xC9B27A) : Hex(0xE2A73A);
            Color backEdge = locked ? Hex(0x8E7A4C) : Hex(0xA8701A);
            // tab and back panel
            if (InRound(x, y, 4, 7, 17, 8, 2)) c = InRound(x, y, 5, 8, 15, 7, 1.5f) ? back : backEdge;
            if (InRound(x, y, 4, 11, 40, 29, 2)) c = InRound(x, y, 5, 12, 38, 27, 1.5f) ? back : backEdge;
            // a sheet of paper peeking out
            if (InRound(x, y, 9, 13, 30, 10, 1))
            {
                c = Color.white;
                if (y > 15 && ((int)y % 3 == 0) && x > 12 && x < 35) c = Hex(0xB9C9E6);
            }
            // front panel
            if (InRound(x, y, 3, 18, 42, 25, 2))
            {
                float t = (y - 18) / 25f;
                Color top = locked ? Hex(0xE8DDB8) : Hex(0xFBE08A);
                Color bottom = locked ? Hex(0xC8B888) : Hex(0xEDB94A);
                c = Color.Lerp(top, bottom, t);
                if (y < 19.5f) c = locked ? Hex(0xF6F0DC) : Hex(0xFFF5C8);
                if (!InRound(x, y, 4, 19, 40, 23, 1.5f)) c = locked ? Hex(0x9A8858) : Hex(0xC08A28);
            }
            if (locked)
            {
                // padlock in the lower right corner
                float cx = 34, cy = 30;
                float dx = x - cx, dy = y - cy, d = Mathf.Sqrt(dx * dx + dy * dy);
                if (y < cy + 1 && d <= 6 && d >= 3.8f) c = Hex(0x6E7480);
                if (InRound(x, y, 27, 30, 14, 12, 2))
                {
                    c = Color.Lerp(Hex(0xF2D06A), Hex(0xC2902C), (y - 30) / 12f);
                    if (!InRound(x, y, 28, 31, 12, 10, 1.5f)) c = Hex(0x7A5A16);
                    if (Disk(x, y, 34, 35, 1.6f, Color.white).a > 0 || (x > 33.3f && x < 34.7f && y > 35 && y < 39)) c = Hex(0x3A2A08);
                }
            }
            return c;
        }

        static Color DiscPaint(float x, float y)
        {
            float dx = x - 24, dy = y - 24, d = Mathf.Sqrt(dx * dx + dy * dy);
            if (d > 21.5f || d < 3) return Color.clear;
            if (d > 20.5f) return Hex(0x8F96A3);
            if (d < 7.5f) return d > 6.5f ? Hex(0xA9B2BE) : new Color(.86f, .9f, .94f, .85f);
            float ang = Mathf.Atan2(dy, dx);
            float sheen = Mathf.Pow(Mathf.Abs(Mathf.Sin(ang * 2 + .6f)), 6);
            Color rainbow = Color.HSVToRGB(Mathf.Repeat(ang / (2 * Mathf.PI) + d / 40f, 1), .35f, 1);
            Color silver = Color.Lerp(Hex(0xD7DCE4), Hex(0xF4F6FA), (d - 7.5f) / 13f);
            return Color.Lerp(silver, rainbow, .25f + .45f * sheen);
        }

        // Notepad-style text file: white page with a folded corner and blue lines.
        static Color DocumentPaint(float x, float y)
        {
            const float l = 9, t = 4, w = 30, h = 40, fold = 10;
            if (x < l || x > l + w || y < t || y > t + h) return Color.clear;
            float fx = x - (l + w - fold), fy = y - t;
            if (fx > 0 && fy < fold)
            {
                if (fx > fy) return Color.clear;                              // cut corner
                return fx > fy - 1.2f ? Hex(0x7A8699) : Hex(0xD8DEE8);          // folded flap
            }
            bool edge = x < l + 1.2f || x > l + w - 1.2f || y > t + h - 1.2f || (y < t + 1.2f && fx <= 0);
            if (edge) return Hex(0x7A8699);
            Color c = Color.Lerp(Color.white, Hex(0xE9EEF6), (y - t) / h);
            float line = y - (t + 14);
            if (line >= 0 && y < t + h - 5 && x > l + 5 && x < l + w - 5 && Mathf.Repeat(line, 5) < 1.3f) c = Hex(0x8DA6D6);
            return c;
        }

        static Color StarPaint(float x, float y)
        {
            var poly = new Vector2[10];
            for (int i = 0; i < 10; i++)
            {
                float a = -Mathf.PI / 2 + i * Mathf.PI / 5, r = i % 2 == 0 ? 15 : 6.3f;
                poly[i] = new Vector2(16 + Mathf.Cos(a) * r, 17 + Mathf.Sin(a) * r);
            }
            if (!Inside(poly, x, y)) return Color.clear;
            // gold with a soft top highlight
            return Color.Lerp(Hex(0xFFF3A0), Hex(0xF0A800), Mathf.Clamp01((y - 4) / 24f));
        }

        static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return Vector2.Distance(p, a + ab * t);
        }

        static Color ChevronPaint(float x, float y)
        {
            var p = new Vector2(x, y);
            float d = Mathf.Min(SegmentDistance(p, new Vector2(3, 9.5f), new Vector2(10, 2.5f)), SegmentDistance(p, new Vector2(10, 2.5f), new Vector2(17, 9.5f)));
            return d < 2 ? Color.white : Color.clear;
        }

        // The classic arrow pointer: white with a black outline, tip at the top-left.
        static Color CursorPaint(float x, float y)
        {
            Vector2[] poly = { new Vector2(1, 1), new Vector2(1, 18), new Vector2(5, 14.2f), new Vector2(8, 21.5f), new Vector2(11, 20.3f), new Vector2(8, 13.2f), new Vector2(13.5f, 13.2f) };
            if (!Inside(poly, x, y)) return Color.clear;
            var p = new Vector2(x, y);
            float d = float.MaxValue;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++) d = Mathf.Min(d, SegmentDistance(p, poly[j], poly[i]));
            return d < 1.1f ? Color.black : Color.white;
        }

        static Color AppIconPaint(float x, float y)
        {
            Color c = Color.clear;
            if (InRound(x, y, 1, 1, 30, 30, 3))
            {
                c = Color.Lerp(Hex(0x5B86E5), Hex(0x1F4FC9), y / 32f);
                if (!InRound(x, y, 2, 2, 28, 28, 2.5f)) c = Hex(0x0B2A8C);
            }
            Color[] rows = { Hex(0xF59A33), Hex(0xB8C25E), Hex(0x5CB84D) };
            for (int r = 0; r < 3; r++)
                for (int col = 0; col < 4; col++)
                    if (InRound(x, y, 4 + col * 6.2f, 5 + r * 4.2f, 5.4f, 3.4f, .8f)) c = rows[r];
            if (Disk(x, y, 19, 21.5f, 2.4f, Color.white).a > 0) c = Color.white;
            if (InRound(x, y, 9, 25.5f, 14, 2.8f, 1.2f)) c = Hex(0xE6F0FF);
            return c;
        }

        static Color BoltPaint(float x, float y)
        {
            Vector2[] poly = { new Vector2(7, 0), new Vector2(1, 9), new Vector2(5.5f, 9), new Vector2(4, 16), new Vector2(11, 6), new Vector2(6.5f, 6), new Vector2(8.5f, 0) };
            return Inside(poly, x, y) ? Color.white : Color.clear;
        }

        static bool Inside(Vector2[] poly, float x, float y)
        {
            bool inside = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
                if ((poly[i].y > y) != (poly[j].y > y) &&
                    x < (poly[j].x - poly[i].x) * (y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)
                    inside = !inside;
            return inside;
        }

        static Color BackgroundPaint(float x, float y)
        {
            float t = y / 256f;
            // Luna blue, the same family as the XP title bar and logon screen
            Color c = Color.Lerp(Hex(0x4A78DE), Hex(0x10307E), t);
            if (y < 2) c = Color.Lerp(c, Color.white, .25f);
            return c;
        }
    }
}
