using System;
using System.Collections.Generic;
using System.Linq;
using NatureCalls.Facility;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NatureCalls.Editor
{
    /// <summary>
    /// Places the Playground spawn points on open floor of the room, measured in the scene. Spawn 1 (the host's
    /// slot) seeds the search: candidates must be visible from it (so they lie in the same room), stand on the
    /// floor, keep a standing player clear of every collider, and stay out of the stalls. Points are spread at
    /// least <see cref="Spacing"/> apart, nearest to Spawn 1 first.
    /// </summary>
    public static class SpawnPointSetup
    {
        private const string ScenePath = "Assets/Scenes/Playground.unity";
        private const float PlayerRadius = .32f;
        private const float PlayerHeight = 2.05f;
        private const float Clearance = .15f;
        private const float Spacing = 1.2f;
        private const float Step = .25f;
        private const float SearchRadius = 4f;

        [MenuItem("NatureCalls/Place Playground Spawn Points")]
        public static void Configure()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Physics.SyncTransforms();
            var world = Object.FindFirstObjectByType<Map.Playground>();
            if (world == null || world.Spawns == null || world.Spawns.Length == 0) throw new InvalidOperationException("Playground has no spawn points.");
            var stalls = StallBounds();
            var seed = world.Spawns[0].position;
            if (!IsClear(seed, stalls, out var reason)) throw new InvalidOperationException("Spawn 1 is not on open floor: " + reason);

            var chosen = new List<Vector3> { seed };
            var candidates = new List<Vector3>();
            for (float x = -SearchRadius; x <= SearchRadius; x += Step)
            for (float z = -SearchRadius; z <= SearchRadius; z += Step)
                candidates.Add(new Vector3(seed.x + x, seed.y, seed.z + z));
            foreach (var point in candidates.OrderBy(point => Vector3.Distance(point, seed)))
            {
                if (chosen.Count == world.Spawns.Length) break;
                if (chosen.Any(other => Vector3.Distance(other, point) < Spacing)) continue;
                if (IsClear(point, stalls, out _) && Visible(seed, point)) chosen.Add(point);
            }
            if (chosen.Count < world.Spawns.Length) throw new InvalidOperationException($"Only {chosen.Count} open spawn points found near Spawn 1.");

            for (int i = 1; i < world.Spawns.Length; i++)
            {
                Undo.RecordObject(world.Spawns[i], "Place spawn point");
                world.Spawns[i].position = chosen[i];
                world.Spawns[i].rotation = world.Spawns[0].rotation;
            }
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Failed to save " + ScenePath);
            Verify();
        }

        // Re-opens the scene and checks every spawn point.
        public static void Verify()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Physics.SyncTransforms();
            var world = Object.FindFirstObjectByType<Map.Playground>();
            var stalls = StallBounds();
            foreach (var spawn in world.Spawns)
            {
                var clear = IsClear(spawn.position, stalls, out var reason);
                Debug.Log($"PLAYGROUND_SPAWN|name={spawn.name}|position={spawn.position}|clear={clear}|reason={reason}");
                if (!clear) throw new InvalidOperationException(spawn.name + " is not on open floor: " + reason);
            }
        }

        private static bool IsClear(Vector3 point, Bounds[] stalls, out string reason)
        {
            reason = "";
            if (stalls.Any(bounds => bounds.Contains(new Vector3(point.x, bounds.center.y, point.z)))) { reason = "inside a stall"; return false; }
            if (!Physics.Raycast(point + Vector3.up * .5f, Vector3.down, out var floor, 1f, 1, QueryTriggerInteraction.Ignore)) { reason = "no floor below"; return false; }
            if (Mathf.Abs(floor.point.y - point.y) > .1f) { reason = "floor at " + floor.point.y.ToString("F2"); return false; }
            var radius = PlayerRadius + Clearance;
            var hits = Physics.OverlapCapsule(point + Vector3.up * (radius + .05f), point + Vector3.up * (PlayerHeight - radius), radius, 1, QueryTriggerInteraction.Ignore);
            if (hits.Length > 0) { reason = "overlaps " + string.Join(",", hits.Select(hit => hit.name)); return false; }
            return true;
        }

        private static bool Visible(Vector3 from, Vector3 to)
        {
            var eye = Vector3.up * 1f;
            return !Physics.Linecast(from + eye, to + eye, 1, QueryTriggerInteraction.Ignore);
        }

        private static Bounds[] StallBounds() =>
            Object.FindObjectsByType<StallDoor>(FindObjectsInactive.Include, FindObjectsSortMode.InstanceID)
                .Select(door =>
                {
                    var renderers = door.GetComponentsInChildren<Renderer>(true);
                    var bounds = renderers[0].bounds;
                    foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
                    return bounds;
                }).ToArray();
    }
}
