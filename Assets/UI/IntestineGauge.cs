using System.Collections.Generic;
using NatureCalls.Core;
using UnityEngine;
using UnityEngine.UI;

namespace NatureCalls.UI
{
    /// <summary>
    /// Bowel urge as a coiled intestine that fills from the top down (GLOSSARY: Urgency, Mess/StoolKind). The fill
    /// shows what is coming: <see cref="StoolKind.Soft"/> is a continuous brown fill, <see cref="StoolKind.Hard"/> is a
    /// chain of separate pellets, <see cref="StoolKind.Loose"/> is a runny fill that sloshes while the player moves.
    /// Drawn as one mesh of soft discs along a serpentine path, so it needs no art assets.
    /// </summary>
    public sealed class IntestineGauge : MaskableGraphic
    {
        /// <summary>0–1 fill (bowel urge).</summary>
        public float Fill;
        public StoolKind Kind;
        /// <summary>0–1 how hard the contents slosh (player speed); only loose stool reacts.</summary>
        public float Slosh;
        /// <summary>0–1 alarm pulse on the intestine wall (very high urge, endurance).</summary>
        public float Alarm;

        public static readonly Color Wall = new(.95f, .66f, .64f, 1f);
        public static readonly Color WallEdge = new(.42f, .16f, .18f, .95f);
        public static readonly Color Lumen = new(.25f, .10f, .12f, .85f);
        public static readonly Color SoftColor = new(.52f, .33f, .15f, 1f);
        public static readonly Color HardColor = new(.33f, .20f, .09f, 1f);
        public static readonly Color HardShine = new(.58f, .40f, .22f, 1f);
        public static readonly Color LooseColor = new(.78f, .60f, .27f, .95f);
        public static readonly Color AlarmColor = new(.95f, .15f, .12f, 1f);

        private static Texture2D disc;
        private readonly List<Vector2> path = new();
        private readonly List<float> lengths = new();
        private Rect builtFor;

        // Serpentine loops (small intestine) ending in a short descent (rectum), in 0–1 rect coordinates, top first.
        private static readonly Vector2[] Coil =
        {
            new(.18f, .97f), new(.82f, .97f), new(.86f, .82f), new(.18f, .78f), new(.14f, .62f), new(.82f, .60f),
            new(.86f, .44f), new(.18f, .42f), new(.14f, .26f), new(.70f, .22f), new(.56f, .10f), new(.54f, .00f),
        };

        public override Texture mainTexture => Disc();

        private static Texture2D Disc()
        {
            if (disc != null) return disc;
            const int n = 32; disc = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                    disc.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(n / 2f - new Vector2(x - n / 2f + .5f, y - n / 2f + .5f).magnitude)));
            disc.Apply();
            return disc;
        }

        private void Update()
        {
            // Animated states redraw every frame; a static soft or hard fill only redraws when values change.
            if (Alarm > .01f || (Kind == StoolKind.Loose && Fill > .01f)) SetVerticesDirty();
        }

        public void Set(float fill, StoolKind kind, float slosh, float alarm)
        {
            fill = Mathf.Clamp01(fill); slosh = Mathf.Clamp01(slosh); alarm = Mathf.Clamp01(alarm);
            if (Mathf.Abs(fill - Fill) < .002f && kind == Kind && Mathf.Abs(slosh - Slosh) < .02f && Mathf.Abs(alarm - Alarm) < .02f) return;
            Fill = fill; Kind = kind; Slosh = slosh; Alarm = alarm;
            SetVerticesDirty();
        }

        private void BuildPath(Rect rect)
        {
            path.Clear(); lengths.Clear(); builtFor = rect;
            for (int i = 0; i < Coil.Length - 1; i++)
            {
                var p0 = Coil[Mathf.Max(0, i - 1)]; var p1 = Coil[i]; var p2 = Coil[i + 1]; var p3 = Coil[Mathf.Min(Coil.Length - 1, i + 2)];
                for (int k = 0; k < 24; k++)
                {
                    float t = k / 24f, t2 = t * t, t3 = t2 * t;
                    var p = .5f * (2 * p1 + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t2 + (-p0 + 3 * p1 - 3 * p2 + p3) * t3);
                    path.Add(new Vector2(rect.xMin + p.x * rect.width, rect.yMin + p.y * rect.height));
                }
            }
            var last = Coil[^1]; path.Add(new Vector2(rect.xMin + last.x * rect.width, rect.yMin + last.y * rect.height));
            float total = 0; lengths.Add(0);
            for (int i = 1; i < path.Count; i++) { total += Vector2.Distance(path[i - 1], path[i]); lengths.Add(total); }
        }

        private Vector2 At(float distance, out Vector2 normal)
        {
            int i = lengths.BinarySearch(distance); if (i < 0) i = ~i;
            i = Mathf.Clamp(i, 1, path.Count - 1);
            var a = path[i - 1]; var b = path[i];
            var t = Mathf.InverseLerp(lengths[i - 1], lengths[i], distance);
            var direction = (b - a).normalized; normal = new Vector2(-direction.y, direction.x);
            return Vector2.Lerp(a, b, t);
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var rect = GetPixelAdjustedRect();
            if (path.Count == 0 || rect != builtFor) BuildPath(rect);
            float total = lengths[^1];
            float radius = Mathf.Min(rect.width, rect.height) * .082f;
            float step = Mathf.Max(1.5f, radius * .35f);
            float time = Application.isPlaying ? Time.unscaledTime : 0;
            float pulse = Alarm > .01f ? Alarm * (.55f + .45f * Mathf.Sin(time * 10f)) : 0f;

            var edge = Color.Lerp(WallEdge, AlarmColor, pulse);
            for (float s = 0; s <= total; s += step) Disc(vh, At(s, out _), radius + 2.5f + pulse * 1.5f, edge);
            for (float s = 0; s <= total; s += step) Disc(vh, At(s, out _), radius, Wall);
            for (float s = 0; s <= total; s += step) Disc(vh, At(s, out _), radius * .62f, Lumen);

            float filled = Fill * total;
            float inner = radius * .62f;
            switch (Kind)
            {
                case StoolKind.Hard:
                    // Separate pellets with gaps, slightly uneven sizes, each with a little highlight.
                    int index = 0;
                    for (float s = inner; s <= filled; s += inner * 2.5f, index++)
                    {
                        var jitter = .82f + .18f * Mathf.PerlinNoise(index * .73f, 1.3f);
                        var centre = At(s, out _);
                        Disc(vh, centre, inner * jitter, HardColor);
                        Disc(vh, centre + new Vector2(-.3f, .3f) * inner * jitter, inner * jitter * .35f, HardShine);
                    }
                    break;
                case StoolKind.Loose:
                    // Runny contents: a wobbling surface at the front and a sideways slosh that grows with speed.
                    float wave = (.25f + Slosh) * inner;
                    float front = filled + Mathf.Sin(time * 7f) * (1.5f + 4f * Slosh);
                    for (float s = 0; s <= Mathf.Min(total, front); s += step * .8f)
                    {
                        var centre = At(s, out var normal);
                        centre += normal * Mathf.Sin(s * .12f - time * (4f + 8f * Slosh)) * wave * .45f;
                        Disc(vh, centre, inner * (.92f + .08f * Mathf.Sin(s * .3f + time * 6f)), LooseColor);
                    }
                    break;
                default:
                    for (float s = 0; s <= filled; s += step * .8f) Disc(vh, At(s, out _), inner, SoftColor);
                    break;
            }
        }

        private void Disc(VertexHelper vh, Vector2 centre, float radius, Color tint)
        {
            int start = vh.currentVertCount;
            Color32 c = tint * color;
            vh.AddVert(new Vector3(centre.x - radius, centre.y - radius), c, new Vector2(0, 0));
            vh.AddVert(new Vector3(centre.x - radius, centre.y + radius), c, new Vector2(0, 1));
            vh.AddVert(new Vector3(centre.x + radius, centre.y + radius), c, new Vector2(1, 1));
            vh.AddVert(new Vector3(centre.x + radius, centre.y - radius), c, new Vector2(1, 0));
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start + 2, start + 3, start);
        }
    }
}
