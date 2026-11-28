using System.Collections.Generic;
using NatureCalls.Core;
using NatureCalls.Map;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NatureCalls.Facility
{
    /// <summary>
    /// Presentation only: urine puddles laid by one stream sit close together, and drawn as separate translucent
    /// patches they overlapped and darkened. Puddles closer than <see cref="Link"/> are joined into one flat,
    /// continuous trail (a single mesh, so it never overlaps itself); a lone puddle keeps its patch. Gameplay
    /// (slip, composure, tidiness, networking) still uses the individual Puddle messes.
    /// </summary>
    public sealed class UrineTrails : MonoBehaviour
    {
        public const float Link = .7f, Width = .34f, Lift = .006f;
        private static UrineTrails instance;
        private Playground world;
        private readonly List<LineRenderer> lines = new();
        private readonly List<Mess> puddles = new();
        private int signature;

        /// <summary>Number of joined trails currently drawn (for tests).</summary>
        public int TrailCount { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (RuntimeMode.Dedicated) return;
            if (instance != null || FindFirstObjectByType<Playground>() == null) return;
            var go = new GameObject("Urine trails");
            SceneManager.MoveGameObjectToScene(go, scene);
            instance = go.AddComponent<UrineTrails>();
        }

        private void OnDestroy() { if (instance == this) instance = null; }

        private void LateUpdate()
        {
            if (world == null) world = FindFirstObjectByType<Playground>();
            if (world == null) return;
            puddles.Clear();
            int hash = 17;
            foreach (var mess in world.Messes)
                if (mess != null && mess.gameObject.activeInHierarchy && mess.Kind == MessKind.Puddle)
                {
                    puddles.Add(mess);
                    var p = mess.transform.position;
                    hash = hash * 31 + Mathf.RoundToInt(p.x * 100) * 7 + Mathf.RoundToInt(p.z * 100);
                }
            hash = hash * 31 + puddles.Count;
            if (hash == signature) return;
            signature = hash;
            Rebuild();
        }

        private void Rebuild()
        {
            // Connected groups by distance (union-find).
            int n = puddles.Count;
            var parent = new int[n];
            for (int i = 0; i < n; i++) parent[i] = i;
            int Root(int i) { while (parent[i] != i) i = parent[i] = parent[parent[i]]; return i; }
            for (int i = 0; i < n; i++)
                for (int j = i + 1; j < n; j++)
                    if (Flat(puddles[i].transform.position, puddles[j].transform.position) < Link) parent[Root(i)] = Root(j);
            var groups = new Dictionary<int, List<Mess>>();
            for (int i = 0; i < n; i++)
            {
                int r = Root(i);
                if (!groups.TryGetValue(r, out var list)) groups[r] = list = new List<Mess>();
                list.Add(puddles[i]);
            }

            int used = 0;
            foreach (var group in groups.Values)
            {
                bool joined = group.Count >= 2;
                foreach (var mess in group) SetPatchVisible(mess, !joined);
                if (!joined) continue;
                var line = Line(used++);
                var points = Order(group);
                line.positionCount = points.Count;
                for (int i = 0; i < points.Count; i++) line.SetPosition(i, points[i] + Vector3.up * Lift);
                line.enabled = true;
            }
            for (int i = used; i < lines.Count; i++) lines[i].enabled = false;
            TrailCount = used;
        }

        /// <summary>Walks the group from one end to the other so the trail follows the stream.</summary>
        private static List<Vector3> Order(List<Mess> group)
        {
            var left = new List<Vector3>();
            foreach (var mess in group) left.Add(mess.transform.position);
            // Start at the point farthest from the group centre (an end of the trail).
            var centre = Vector3.zero; foreach (var p in left) centre += p; centre /= left.Count;
            int start = 0; float best = -1;
            for (int i = 0; i < left.Count; i++) { float d = Flat(left[i], centre); if (d > best) { best = d; start = i; } }
            var ordered = new List<Vector3> { left[start] }; left.RemoveAt(start);
            while (left.Count > 0)
            {
                var last = ordered[^1]; int next = 0; float near = float.MaxValue;
                for (int i = 0; i < left.Count; i++) { float d = Flat(left[i], last); if (d < near) { near = d; next = i; } }
                ordered.Add(left[next]); left.RemoveAt(next);
            }
            return ordered;
        }

        private LineRenderer Line(int index)
        {
            while (lines.Count <= index)
            {
                var go = new GameObject("Urine trail");
                go.transform.SetParent(transform, false);
                go.transform.rotation = Quaternion.Euler(90, 0, 0); // lie flat: the strip faces up
                var line = go.AddComponent<LineRenderer>();
                line.useWorldSpace = true; line.alignment = LineAlignment.TransformZ;
                line.widthMultiplier = Width; line.numCapVertices = 8; line.numCornerVertices = 6;
                line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; line.receiveShadows = false;
                line.generateLightingData = true; // proper normals so the lit urine material is not shaded dark
                var art = MessArt.Current;
                if (art != null && art.Urine != null) line.sharedMaterial = art.Urine;
                lines.Add(line);
            }
            return lines[index];
        }

        private static void SetPatchVisible(Mess mess, bool visible)
        {
            var look = mess.GetComponent<MessLook>();
            if (look == null || look.Look == null) return;
            foreach (var renderer in look.Look.GetComponentsInChildren<Renderer>(true)) renderer.enabled = visible;
        }

        private static float Flat(Vector3 a, Vector3 b) => Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
    }
}
