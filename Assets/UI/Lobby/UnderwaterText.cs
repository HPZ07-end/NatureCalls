using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace NatureCalls.UI
{
    /// <summary>
    /// Makes floating lobby text read as if it sits under the toilet water: glyphs bend with slow travelling waves,
    /// take on the water's colour with drifting caustic highlights, and cast a soft dark copy deeper in the bowl.
    /// A click on the water (<see cref="Kick"/>) briefly strengthens the waves. Presentation only.
    /// </summary>
    [RequireComponent(typeof(Text))]
    public sealed class UnderwaterText : BaseMeshEffect
    {
        private const float Amplitude = 1.6f, KickAmplitude = 5f;
        private static readonly Color Tint = new(.80f, .95f, .97f, .92f);
        // The depth copy is drawn as a few faint, spread-out layers so it reads as blurred by the water, not as a drop shadow.
        private static readonly Color Depth = new(.01f, .10f, .12f, .2f);
        private static readonly Vector2 DepthOffset = new(3f, -4f);
        private static readonly Vector2[] Blur = { new(-1.8f, 0), new(1.8f, 0), new(0, -1.8f), new(0, 1.8f) };
        private static float kickTime = -10;
        /// <summary>The water opening (world centre and radius). Glyphs fade out towards its rim so nothing shows on the porcelain.</summary>
        public static Vector3 ClipCenter;
        public static float ClipRadius;
        private const float RimFade = .16f;

        private readonly List<UIVertex> stream = new();

        /// <summary>Strengthens the waves for about a second, as after a splash.</summary>
        public static void Kick() => kickTime = Time.unscaledTime;

        public static float Strength
        {
            get
            {
                float since = Time.unscaledTime - kickTime;
                return Amplitude + (since < 1.5f ? KickAmplitude * Mathf.Exp(-3f * since) : 0);
            }
        }

        private void Update()
        {
            if (graphic != null && graphic.isActiveAndEnabled) graphic.SetVerticesDirty();
        }

        public override void ModifyMesh(VertexHelper helper)
        {
            if (!IsActive() || helper.currentVertCount == 0) return;
            stream.Clear();
            helper.GetUIVertexStream(stream);
            int count = stream.Count;
            float time = Time.unscaledTime, strength = Strength;
            // The depth copy goes first so the bright glyphs draw over it.
            foreach (var spread in Blur)
                for (int index = 0; index < count; index++)
                {
                    var vertex = stream[index];
                    var position = Bend(vertex.position, time - .25f, strength * .7f);
                    vertex.position = position + (Vector3)(DepthOffset + spread);
                    vertex.color = Scale(vertex.color, Depth, 1);
                    stream.Add(vertex);
                }
            for (int index = 0; index < count; index++)
            {
                var vertex = stream[index];
                Vector3 position = vertex.position;
                // Caustics: soft bright bands drifting across the letters.
                float caustic = .86f + .22f * Mathf.Sin(time * 1.6f + position.x * .021f + position.y * .034f)
                                     * Mathf.Sin(time * 1.1f - position.x * .013f + position.y * .017f);
                vertex.position = Bend(position, time, strength);
                vertex.color = Scale(vertex.color, Tint, caustic);
                stream.Add(vertex);
            }
            stream.RemoveRange(0, count);
            if (ClipRadius > 0)
                for (int index = 0; index < stream.Count; index++)
                {
                    var vertex = stream[index];
                    var world = transform.TransformPoint(vertex.position);
                    float distance = new Vector2(world.x - ClipCenter.x, world.z - ClipCenter.z).magnitude;
                    float keep = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(ClipRadius * (1 - RimFade), ClipRadius, distance));
                    var color = vertex.color; color.a = (byte)(color.a * keep); vertex.color = color;
                    stream[index] = vertex;
                }
            helper.Clear();
            helper.AddUIVertexTriangleStream(stream);
        }

        // Two crossing waves give the wobbling, refracted look of print seen through moving water.
        private static Vector3 Bend(Vector3 position, float time, float strength)
        {
            float x = position.x, y = position.y;
            position.y += strength * Mathf.Sin(time * 2.1f + x * .045f);
            position.x += strength * .6f * Mathf.Sin(time * 1.7f + y * .06f + x * .01f);
            return position;
        }

        private static Color32 Scale(Color32 source, Color tint, float brightness)
        {
            Color color = source;
            return new Color(Mathf.Clamp01(color.r * tint.r * brightness), Mathf.Clamp01(color.g * tint.g * brightness),
                Mathf.Clamp01(color.b * tint.b * brightness), color.a * tint.a);
        }
    }
}
