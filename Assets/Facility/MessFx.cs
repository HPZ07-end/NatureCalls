using System.Collections.Generic;
using UnityEngine;

namespace NatureCalls.Facility
{
    /// <summary>
    /// Lightweight camera-facing sprite wisps (the project ships without the Particle System module): odour waves and
    /// flies over messes, comfort sparkles and splash droplets.
    /// </summary>
    public sealed class Wisps : MonoBehaviour
    {
        public Material Material;
        public float Rate, Size = .3f, Radius = .15f, Rise = .3f, Life = 2.2f, Speed, Gravity;
        public Color Tint = Color.white;
        public bool Loop = true;
        public int Alive => alive;

        private struct Puff { public Transform Transform; public Renderer Renderer; public Vector3 Velocity; public float Age, Life, Size; }
        private readonly List<Puff> puffs = new();
        private int alive;
        private float owed;
        private MaterialPropertyBlock block;
        private static Mesh quad;
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        public void Burst(int count) { for (int i = 0; i < count; i++) Spawn(); }

        /// <summary>Sends the live puffs mainly along <paramref name="direction"/> (a directed splash).</summary>
        public Wisps Aim(Vector3 direction, float speed)
        {
            for (int i = 0; i < puffs.Count; i++)
            {
                var p = puffs[i];
                if (p.Age >= p.Life) continue;
                p.Velocity = direction.normalized * speed * Random.Range(.6f, 1.1f) + Random.insideUnitSphere * speed * .35f;
                puffs[i] = p;
            }
            return this;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (Loop && Rate > 0) { owed += Rate * dt; while (owed >= 1) { owed -= 1; Spawn(); } }
            var cam = UnityEngine.Camera.main;
            var facing = cam != null ? Quaternion.LookRotation(cam.transform.forward, cam.transform.up) : Quaternion.identity;
            block ??= new MaterialPropertyBlock();
            alive = 0;
            for (int i = 0; i < puffs.Count; i++)
            {
                var p = puffs[i];
                if (p.Age >= p.Life) continue;
                p.Age += dt;
                if (p.Age >= p.Life) { p.Renderer.enabled = false; puffs[i] = p; continue; }
                alive++;
                p.Velocity += Vector3.down * Gravity * 9.81f * dt;
                p.Transform.position += p.Velocity * dt;
                float t = p.Age / p.Life;
                p.Transform.rotation = facing;
                p.Transform.localScale = Vector3.one * p.Size * (.6f + .8f * t);
                var color = Tint; color.a *= Mathf.Clamp01(t * 5) * Mathf.Clamp01((1 - t) * 3);
                block.SetColor(BaseColor, color);
                p.Renderer.SetPropertyBlock(block);
                puffs[i] = p;
            }
            if (!Loop && alive == 0 && puffs.Count > 0) Destroy(gameObject);
        }

        private void Spawn()
        {
            if (Material == null) return;
            int index = puffs.FindIndex(p => p.Age >= p.Life);
            if (index < 0)
            {
                if (puffs.Count >= 32) return;
                if (quad == null) { var temp = GameObject.CreatePrimitive(PrimitiveType.Quad); quad = temp.GetComponent<MeshFilter>().sharedMesh; Destroy(temp); }
                var go = new GameObject("Puff", typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(transform, true);
                go.GetComponent<MeshFilter>().sharedMesh = quad;
                var renderer = go.GetComponent<MeshRenderer>(); renderer.sharedMaterial = Material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; renderer.receiveShadows = false;
                puffs.Add(new Puff { Transform = go.transform, Renderer = renderer });
                index = puffs.Count - 1;
            }
            var puff = puffs[index];
            var ring = Random.insideUnitCircle * Radius;
            puff.Transform.position = transform.position + new Vector3(ring.x, .03f, ring.y);
            puff.Velocity = Vector3.up * Rise * Random.Range(.75f, 1.25f) + Random.insideUnitSphere * Speed + (Speed > 0 ? Vector3.up * Speed * .6f : Vector3.zero);
            puff.Age = 0; puff.Life = Life * Random.Range(.7f, 1.1f); puff.Size = Size * Random.Range(.75f, 1.2f);
            puff.Renderer.enabled = true;
            puff.Transform.localScale = Vector3.zero;
            puffs[index] = puff;
        }
    }

    public static class MessFx
    {
        public static Wisps CreateOdor(Transform parent, Material material)
        {
            var go = new GameObject("Odor");
            go.transform.SetParent(parent, false);
            var wisps = go.AddComponent<Wisps>();
            wisps.Material = material; wisps.Rise = .3f; wisps.Life = 2.2f;
            return wisps;
        }

        /// <summary>Odour strength follows the mess's odour weight: more, bigger wisps for stronger smells.</summary>
        public static void SetOdor(Wisps wisps, float weight, float radius)
        {
            if (wisps == null) return;
            wisps.Rate = 1.4f * weight; wisps.Size = .2f + .12f * weight; wisps.Radius = radius;
            wisps.Tint = new Color(1, 1, 1, Mathf.Clamp01(.45f + .3f * weight));
            wisps.gameObject.SetActive(weight > 0);
        }

        /// <summary>One-shot burst (sparkles for comfort, droplets for a splash); destroys itself when done.</summary>
        public static Wisps Burst(Vector3 position, Material material, Color tint, int count, float speed, float size, float gravity, float life)
        {
            if (material == null || NatureCalls.Core.RuntimeMode.Dedicated) return null;
            var go = new GameObject("Burst");
            go.transform.position = position;
            var wisps = go.AddComponent<Wisps>();
            wisps.Material = material; wisps.Tint = tint; wisps.Loop = false; wisps.Rate = 0;
            wisps.Speed = speed; wisps.Size = size; wisps.Gravity = gravity; wisps.Life = life; wisps.Rise = 0; wisps.Radius = .05f;
            wisps.Burst(count);
            return wisps;
        }

        public static Wisps Sparkle(Vector3 position)
        {
            var art = MessArt.Current;
            return art == null ? null : Burst(position, art.Sparkle, new Color(1, .92f, .55f), 14, .9f, .2f, -.02f, 1.3f);
        }

        public static Wisps Splash(Vector3 position, Color tint, int count = 18)
        {
            var art = MessArt.Current;
            return art == null ? null : Burst(position, art.Splash, tint, count, 1.8f, .05f, 1f, .7f);
        }
    }
}
