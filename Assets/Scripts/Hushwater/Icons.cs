using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hushwater
{
    // Flat white icons drawn in code from simple shapes (distance fields), tinted by whatever Image shows them.
    // Used by the how-to-play pages, the on-screen guide, the danger warning and the journal map.
    public static class Icons
    {
        public enum Kind
        {
            Lantern, Boat, Camera, Page, Tape, Sound, Footsteps, Wardrobe, Sexton, Bell, Bottle,
            LanternPost, Arrow, Check, Salvage, Map, Breath, Crouch, Eye, Note, Danger
        }

        private const int Size = 128;
        private static readonly Dictionary<Kind, Sprite> Cache = new Dictionary<Kind, Sprite>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Cache.Clear();

        public static Sprite Get(Kind kind)
        {
            if (Cache.TryGetValue(kind, out Sprite sprite) && sprite != null) return sprite;
            Func<Vector2, float> shape = Shape(kind);
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Icon " + kind };
            var pixels = new Color32[Size * Size];
            float px = 1f / Size;
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    float d = shape(new Vector2((x + 0.5f) * px, (y + 0.5f) * px));
                    byte a = (byte)(Mathf.Clamp01(0.5f - d / px) * 255f);
                    pixels[y * Size + x] = new Color32(255, 255, 255, a);
                }
            tex.SetPixels32(pixels);
            tex.Apply();
            return Cache[kind] = Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), Size);
        }

        // ---------- Shapes, in 0..1 space with y up. Negative inside, positive outside. ----------

        private static float Circle(Vector2 p, float cx, float cy, float r) => Vector2.Distance(p, new Vector2(cx, cy)) - r;
        private static float Ring(Vector2 p, float cx, float cy, float r, float w) => Mathf.Abs(Vector2.Distance(p, new Vector2(cx, cy)) - r) - w * 0.5f;

        private static float Box(Vector2 p, float cx, float cy, float hx, float hy, float round = 0f)
        {
            Vector2 q = new Vector2(Mathf.Abs(p.x - cx) - hx + round, Mathf.Abs(p.y - cy) - hy + round);
            return Vector2.Max(q, Vector2.zero).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - round;
        }

        private static float Frame(Vector2 p, float cx, float cy, float hx, float hy, float w, float round = 0f) =>
            Mathf.Abs(Box(p, cx, cy, hx, hy, round)) - w * 0.5f;

        private static float Seg(Vector2 p, float ax, float ay, float bx, float by, float r)
        {
            Vector2 a = new Vector2(ax, ay), b = new Vector2(bx, by);
            Vector2 pa = p - a, ba = b - a;
            float h = Mathf.Clamp01(Vector2.Dot(pa, ba) / Vector2.Dot(ba, ba));
            return (pa - ba * h).magnitude - r;
        }

        // Signed distance to a convex or concave polygon (points in order).
        private static float Poly(Vector2 p, params float[] xy)
        {
            int n = xy.Length / 2;
            Vector2 v0 = new Vector2(xy[0], xy[1]);
            float d = Vector2.Dot(p - v0, p - v0);
            float s = 1f;
            for (int i = 0, j = n - 1; i < n; j = i, i++)
            {
                Vector2 vi = new Vector2(xy[i * 2], xy[i * 2 + 1]), vj = new Vector2(xy[j * 2], xy[j * 2 + 1]);
                Vector2 e = vj - vi, w = p - vi;
                Vector2 b = w - e * Mathf.Clamp01(Vector2.Dot(w, e) / Vector2.Dot(e, e));
                d = Mathf.Min(d, Vector2.Dot(b, b));
                bool c1 = p.y >= vi.y, c2 = p.y < vj.y, c3 = e.x * w.y > e.y * w.x;
                if ((c1 && c2 && c3) || (!c1 && !c2 && !c3)) s = -s;
            }
            return s * Mathf.Sqrt(d);
        }

        // An arc of a ring between two angles (degrees, 0 = right, counter-clockwise).
        private static float Arc(Vector2 p, float cx, float cy, float r, float w, float from, float to)
        {
            Vector2 d = p - new Vector2(cx, cy);
            float a = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            float mid = (from + to) * 0.5f, half = (to - from) * 0.5f;
            if (Mathf.Abs(Mathf.DeltaAngle(a, mid)) <= half) return Mathf.Abs(d.magnitude - r) - w * 0.5f;
            Vector2 e1 = new Vector2(cx, cy) + new Vector2(Mathf.Cos(from * Mathf.Deg2Rad), Mathf.Sin(from * Mathf.Deg2Rad)) * r;
            Vector2 e2 = new Vector2(cx, cy) + new Vector2(Mathf.Cos(to * Mathf.Deg2Rad), Mathf.Sin(to * Mathf.Deg2Rad)) * r;
            return Mathf.Min(Vector2.Distance(p, e1), Vector2.Distance(p, e2)) - w * 0.5f;
        }

        private static float U(params float[] d)
        {
            float m = d[0];
            for (int i = 1; i < d.Length; i++) m = Mathf.Min(m, d[i]);
            return m;
        }

        private static float Cut(float a, float b) => Mathf.Max(a, -b);

        private static Func<Vector2, float> Shape(Kind kind)
        {
            switch (kind)
            {
                case Kind.Lantern:
                    return p => U(
                        Arc(p, 0.5f, 0.84f, 0.1f, 0.035f, 0f, 180f),                 // handle
                        Poly(p, 0.33f, 0.72f, 0.67f, 0.72f, 0.58f, 0.8f, 0.42f, 0.8f),  // cap
                        Frame(p, 0.5f, 0.48f, 0.15f, 0.22f, 0.04f, 0.03f),           // glass frame
                        Seg(p, 0.5f, 0.27f, 0.5f, 0.7f, 0.012f),                     // centre bar
                        U(Circle(p, 0.5f, 0.43f, 0.065f), Poly(p, 0.44f, 0.45f, 0.56f, 0.45f, 0.5f, 0.6f)), // flame
                        Box(p, 0.5f, 0.22f, 0.19f, 0.035f, 0.01f));                   // base
                case Kind.Boat:
                    return p => U(
                        Poly(p, 0.1f, 0.42f, 0.9f, 0.42f, 0.76f, 0.25f, 0.24f, 0.25f), // hull
                        Seg(p, 0.66f, 0.42f, 0.66f, 0.78f, 0.016f),                   // lantern pole
                        Seg(p, 0.66f, 0.78f, 0.74f, 0.78f, 0.014f),
                        Box(p, 0.74f, 0.7f, 0.04f, 0.055f, 0.012f),                   // lantern
                        Seg(p, 0.3f, 0.5f, 0.12f, 0.16f, 0.018f),                     // oar
                        Arc(p, 0.25f, 0.12f, 0.1f, 0.025f, 20f, 160f),                // waves
                        Arc(p, 0.5f, 0.12f, 0.1f, 0.025f, 20f, 160f),
                        Arc(p, 0.75f, 0.12f, 0.1f, 0.025f, 20f, 160f));
                case Kind.Camera:
                    return p => U(
                        Cut(U(Box(p, 0.5f, 0.45f, 0.36f, 0.22f, 0.05f),
                              Box(p, 0.3f, 0.7f, 0.08f, 0.05f, 0.02f),                // flash
                              Box(p, 0.55f, 0.69f, 0.12f, 0.05f, 0.02f)),             // viewfinder
                            Circle(p, 0.52f, 0.45f, 0.155f)),                         // hole for the lens
                        Ring(p, 0.52f, 0.45f, 0.11f, 0.035f),
                        Circle(p, 0.52f, 0.45f, 0.045f));
                case Kind.Page:
                    return p => U(
                        Cut(Poly(p, 0.25f, 0.12f, 0.75f, 0.12f, 0.75f, 0.72f, 0.6f, 0.88f, 0.25f, 0.88f),
                            U(Seg(p, 0.33f, 0.7f, 0.55f, 0.7f, 0.014f), Seg(p, 0.33f, 0.58f, 0.67f, 0.58f, 0.014f),
                              Seg(p, 0.33f, 0.46f, 0.67f, 0.46f, 0.014f), Seg(p, 0.33f, 0.34f, 0.67f, 0.34f, 0.014f),
                              Seg(p, 0.33f, 0.22f, 0.55f, 0.22f, 0.014f))));
                case Kind.Tape:
                    return p => Cut(Box(p, 0.5f, 0.5f, 0.38f, 0.26f, 0.05f),
                        U(Ring(p, 0.33f, 0.53f, 0.09f, 0.04f), Ring(p, 0.67f, 0.53f, 0.09f, 0.04f),
                          Box(p, 0.5f, 0.32f, 0.16f, 0.04f, 0.01f), Seg(p, 0.33f, 0.62f, 0.67f, 0.62f, 0.012f)));
                case Kind.Sound:
                    return p => U(Circle(p, 0.25f, 0.5f, 0.07f),
                        Arc(p, 0.25f, 0.5f, 0.2f, 0.05f, -45f, 45f),
                        Arc(p, 0.25f, 0.5f, 0.38f, 0.05f, -45f, 45f),
                        Arc(p, 0.25f, 0.5f, 0.56f, 0.05f, -40f, 40f));
                case Kind.Footsteps:
                    return p => U(
                        Box(p, 0.36f, 0.32f, 0.08f, 0.14f, 0.08f), Circle(p, 0.36f, 0.12f, 0.06f),
                        Box(p, 0.64f, 0.66f, 0.08f, 0.14f, 0.08f), Circle(p, 0.64f, 0.46f, 0.06f));
                case Kind.Wardrobe:
                    return p => U(
                        Frame(p, 0.5f, 0.5f, 0.27f, 0.38f, 0.05f, 0.02f),
                        Seg(p, 0.5f, 0.14f, 0.5f, 0.86f, 0.02f),
                        Circle(p, 0.44f, 0.5f, 0.03f), Circle(p, 0.56f, 0.5f, 0.03f),
                        Box(p, 0.5f, 0.92f, 0.32f, 0.03f), Seg(p, 0.29f, 0.12f, 0.27f, 0.05f, 0.02f), Seg(p, 0.71f, 0.12f, 0.73f, 0.05f, 0.02f));
                case Kind.Sexton:
                    return p => U(
                        Circle(p, 0.47f, 0.8f, 0.09f),                                // hooded head
                        Poly(p, 0.38f, 0.78f, 0.58f, 0.8f, 0.66f, 0.62f, 0.64f, 0.06f, 0.34f, 0.06f, 0.36f, 0.6f), // stooped body
                        Seg(p, 0.64f, 0.66f, 0.78f, 0.36f, 0.025f),                   // arm
                        Seg(p, 0.86f, 0.9f, 0.74f, 0.05f, 0.016f),                    // hook pole
                        Arc(p, 0.81f, 0.86f, 0.05f, 0.022f, -20f, 200f));
                case Kind.Bell:
                    return p => U(
                        Poly(p, 0.36f, 0.68f, 0.64f, 0.68f, 0.68f, 0.42f, 0.8f, 0.28f, 0.2f, 0.28f, 0.32f, 0.42f),
                        Circle(p, 0.5f, 0.7f, 0.14f), Ring(p, 0.5f, 0.86f, 0.05f, 0.03f),
                        Circle(p, 0.5f, 0.2f, 0.06f));
                case Kind.Bottle:
                    return p => U(Box(p, 0.5f, 0.35f, 0.15f, 0.24f, 0.08f), Box(p, 0.5f, 0.68f, 0.05f, 0.14f, 0.02f), Box(p, 0.5f, 0.84f, 0.07f, 0.03f));
                case Kind.LanternPost:
                    return p => U(
                        Seg(p, 0.5f, 0.06f, 0.5f, 0.68f, 0.03f), Box(p, 0.5f, 0.06f, 0.14f, 0.03f),
                        Box(p, 0.5f, 0.78f, 0.1f, 0.1f, 0.02f), Poly(p, 0.36f, 0.88f, 0.64f, 0.88f, 0.5f, 0.97f));
                case Kind.Arrow:
                    return p => Poly(p, 0.5f, 0.92f, 0.84f, 0.3f, 0.5f, 0.45f, 0.16f, 0.3f);
                case Kind.Check:
                    return p => U(Seg(p, 0.2f, 0.52f, 0.42f, 0.28f, 0.06f), Seg(p, 0.42f, 0.28f, 0.82f, 0.76f, 0.06f));
                case Kind.Salvage:
                    return p => Cut(U(Circle(p, 0.5f, 0.5f, 0.28f),
                            Box(p, 0.5f, 0.86f, 0.06f, 0.08f), Box(p, 0.5f, 0.14f, 0.06f, 0.08f),
                            Box(p, 0.14f, 0.5f, 0.08f, 0.06f), Box(p, 0.86f, 0.5f, 0.08f, 0.06f)),
                        Circle(p, 0.5f, 0.5f, 0.12f));
                case Kind.Map:
                    return p => Cut(Poly(p, 0.1f, 0.2f, 0.36f, 0.12f, 0.64f, 0.2f, 0.9f, 0.12f, 0.9f, 0.8f, 0.64f, 0.88f, 0.36f, 0.8f, 0.1f, 0.88f),
                        U(Seg(p, 0.36f, 0.14f, 0.36f, 0.78f, 0.015f), Seg(p, 0.64f, 0.22f, 0.64f, 0.86f, 0.015f)));
                case Kind.Breath:
                    return p => U(Arc(p, 0.4f, 0.5f, 0.12f, 0.05f, 90f, 360f), Seg(p, 0.4f, 0.62f, 0.85f, 0.62f, 0.025f),
                        Arc(p, 0.6f, 0.36f, 0.1f, 0.05f, -90f, 180f), Seg(p, 0.15f, 0.46f, 0.6f, 0.46f, 0.025f));
                case Kind.Crouch:
                    return p => U(Circle(p, 0.62f, 0.72f, 0.09f), Seg(p, 0.55f, 0.6f, 0.38f, 0.38f, 0.06f),
                        Seg(p, 0.38f, 0.38f, 0.58f, 0.26f, 0.05f), Seg(p, 0.58f, 0.26f, 0.5f, 0.08f, 0.045f),
                        Seg(p, 0.5f, 0.52f, 0.7f, 0.4f, 0.035f));
                case Kind.Eye:
                    return p => U(Cut(Mathf.Max(Circle(p, 0.5f, 0.25f, 0.4f), Circle(p, 0.5f, 0.75f, 0.4f)), Circle(p, 0.5f, 0.5f, 0.17f)),
                        Circle(p, 0.5f, 0.5f, 0.09f));
                case Kind.Note:
                    return p => U(Cut(Box(p, 0.47f, 0.5f, 0.26f, 0.34f, 0.02f),
                            U(Seg(p, 0.32f, 0.66f, 0.6f, 0.66f, 0.014f), Seg(p, 0.32f, 0.52f, 0.62f, 0.52f, 0.014f), Seg(p, 0.32f, 0.38f, 0.55f, 0.38f, 0.014f))),
                        Seg(p, 0.86f, 0.82f, 0.62f, 0.22f, 0.03f), Poly(p, 0.6f, 0.2f, 0.64f, 0.16f, 0.58f, 0.1f));
                default: // Danger: a warning triangle with an exclamation mark
                    return p => Cut(Poly(p, 0.5f, 0.92f, 0.94f, 0.12f, 0.06f, 0.12f),
                        U(Seg(p, 0.5f, 0.62f, 0.5f, 0.38f, 0.045f), Circle(p, 0.5f, 0.24f, 0.05f)));
            }
        }
    }
}
