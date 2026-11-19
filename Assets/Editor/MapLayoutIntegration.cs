using System;
using System.Linq;
using NatureCalls.Camera;
using NatureCalls.Facility;
using NatureCalls.Map;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace NatureCalls.Editor
{
    /// <summary>Connects the existing layout to the same runtime contract as Playground.</summary>
    public static class MapLayoutIntegration
    {
        public const string ScenePath = "Assets/Scenes/MapLayoutPrototype.unity";

        [MenuItem("NatureCalls/Connect Layout Map To Lobby")]
        public static void Configure()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var source = EditorSceneManager.OpenScene(Playground.ScenePath, OpenSceneMode.Single);
            var template = Object.FindFirstObjectByType<Map.Playground>();
            var player = template.PlayerPrefab;
            // Clone the existing camera as a group so all Cinemachine references stay internal.
            var cameraGroup = new GameObject("Local camera rig");
            foreach (var root in new[] { template.Camera.transform.root, template.Camera.Eye.root,
                         template.Camera.Shoulder.root, template.Camera.FirstPerson.transform.root,
                         template.Camera.ThirdPerson.transform.root }.Distinct().ToArray())
                root.SetParent(cameraGroup.transform, true);
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            var world = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Map.Playground>(true)).SingleOrDefault();
            if (world == null)
            {
                world = new GameObject("Playground").AddComponent<Map.Playground>();
                var camera = Object.Instantiate(cameraGroup);
                camera.name = "Local camera rig";
                world.Camera = camera.GetComponentInChildren<CameraController>(true);
            }
            world.PlayerPrefab = player;
            world.StartOffline = true;
            // Close without saving: the original Playground and its camera hierarchy stay unchanged.
            EditorSceneManager.CloseScene(source, true);
            foreach (var camera in Object.FindObjectsByType<UnityEngine.Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (camera != world.Camera.Output) camera.enabled = false;

            ConfigureStalls();
            ConfigureSupplies();
            ConfigureSpawns(world);
            LobbyFlowBuilder.AddMatchHud();
            var catalog = AssetDatabase.LoadAssetAtPath<MapCatalog>("Assets/Map/MapCatalog.asset");
            if (!Enumerable.Range(0, catalog.Count).Any(i => catalog[i].ScenePath == ScenePath))
                catalog.SetMaps(Enumerable.Range(0, catalog.Count).Select(i => catalog[i]).Concat(new[] {
                    new MapCatalog.Entry { DisplayName = "模块厕所（MapLayoutPrototype）", ScenePath = ScenePath }
                }).ToArray());
            EditorUtility.SetDirty(catalog);
            var buildScenes = EditorBuildSettings.scenes.ToList();
            var existing = buildScenes.FindIndex(item => item.path == ScenePath);
            if (existing < 0) buildScenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            else buildScenes[existing] = new EditorBuildSettingsScene(ScenePath, true);
            EditorBuildSettings.scenes = buildScenes.ToArray();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save layout map.");
            AssetDatabase.SaveAssets();
            Automation.Validate();
        }

        private static void ConfigureStalls()
        {
            var leaves = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(t => t.name == "Door visual - replace with interactive door").ToArray();
            if (leaves.Length == 0) throw new InvalidOperationException("Layout has no stall doors.");
            foreach (var leaf in leaves)
            {
                var stall = leaf.GetComponentInParent<StallDoor>();
                var shell = leaf.parent;
                if (stall == null) stall = shell.gameObject.AddComponent<StallDoor>();
                var center = leaf.localPosition;
                var size = leaf.localScale;
                var pivotPosition = new Vector3(center.x - size.x * .5f, 0, center.z);
                var hinge = Child(shell, "Door hinge", pivotPosition);
                var collision = Child(shell, "Door collider pivot", pivotPosition);
                // A source door belongs to a nested prefab instance. Reparenting that source child is not
                // saved as a reliable scene override, leaving an empty hinge at runtime. Keep it hidden as
                // the visual source and create an equivalent scene-owned panel below the hinge instead.
                var panel = hinge.Find("Interactive door panel");
                if (panel == null)
                {
                    var panelObject = Object.Instantiate(leaf.gameObject, hinge);
                    panelObject.name = "Interactive door panel";
                    panelObject.SetActive(true);
                    panel = panelObject.transform;
                    panel.localPosition = new Vector3(size.x * .5f, center.y, 0);
                    panel.localRotation = leaf.localRotation;
                    panel.localScale = size;
                    foreach (var panelCollider in panelObject.GetComponentsInChildren<Collider>(true))
                        Object.DestroyImmediate(panelCollider);
                }
                panel.localPosition = new Vector3(size.x * .5f, center.y, 0);
                panel.localRotation = leaf.localRotation;
                panel.localScale = size;
                leaf.gameObject.SetActive(false);
                var box = collision.GetComponent<BoxCollider>() ?? collision.gameObject.AddComponent<BoxCollider>();
                box.center = new Vector3(size.x * .5f, center.y, 0);
                box.size = size;
                stall.DoorLeaf = hinge;
                stall.DoorColliderPivot = collision;
                // These doors hinge on their left edge. A negative angle opens them along the left
                // interior partition, leaving the middle of the 1m doorway clear for the player.
                stall.OpenAngle = -95f;
                var bowl = shell.Find("Toilet visual");
                if (bowl == null) throw new InvalidOperationException("Stall has no toilet: " + shell.name);
                var bounds = bowl.GetComponentsInChildren<Renderer>().Select(r => r.bounds)
                    .Aggregate((a, b) => { a.Encapsulate(b); return a; });
                var toilet = bowl.gameObject.AddComponent<Toilet>();
                var target = new Vector3(bounds.center.x, bounds.min.y + .4f, bounds.center.z) - shell.forward * .1f;
                toilet.Interior = Child(bowl, "Toilet interior", bowl.InverseTransformPoint(target));
                toilet.Anchor = Child(bowl, "Posture anchor", bowl.InverseTransformPoint(
                    new Vector3(bounds.center.x, shell.position.y + .15f, bounds.center.z) - shell.forward * .65f));
                toilet.Anchor.rotation = Quaternion.LookRotation(shell.forward);
                // Fault stripes remain decoration; they move with the door rather than blocking it.
                var outer = shell.parent;
                if (outer != null)
                    foreach (var stripe in outer.GetComponentsInChildren<Transform>(true)
                                 .Where(t => t.name.StartsWith("Fault stripe", StringComparison.Ordinal)).ToArray())
                    {
                        foreach (var collider in stripe.GetComponents<Collider>()) Object.DestroyImmediate(collider);
                        stripe.SetParent(hinge, true);
                    }
            }
            // Reserved markers describe future systems, not physical obstacles or new rules.
            foreach (var marker in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                         .Where(t => t.name.EndsWith("_Reserved", StringComparison.Ordinal)))
                foreach (var collider in marker.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
        }

        private static void ConfigureSupplies()
        {
            foreach (var roll in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                         .Where(t => t.name == "Toilet paper roll" || t.name.StartsWith("Paper roll ", StringComparison.Ordinal)))
            {
                if (roll.GetComponent<PaperRoll>() == null) roll.gameObject.AddComponent<PaperRoll>();
                if (roll.GetComponent<Rigidbody>() == null) roll.gameObject.AddComponent<Rigidbody>().mass = .05f;
            }
            if (Object.FindFirstObjectByType<Toolbox>() == null)
            {
                var mopVisual = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Single(t => t.name == "Mop visual");
                var toolbox = mopVisual.parent.gameObject.AddComponent<Toolbox>();
                toolbox.Mop = mopVisual.gameObject.AddComponent<Mop>();
                if (mopVisual.GetComponent<Collider>() == null) mopVisual.gameObject.AddComponent<BoxCollider>();
                if (mopVisual.GetComponent<Rigidbody>() == null) mopVisual.gameObject.AddComponent<Rigidbody>().mass = .5f;
            }
        }

        private static void ConfigureSpawns(Map.Playground world)
        {
            Physics.SyncTransforms();
            if (world.Spawns == null || world.Spawns.Length != 4)
                world.Spawns = Enumerable.Range(1, 4).Select(i => Child(world.transform, "Spawn " + i, Vector3.zero)).ToArray();
            var positions = new[] { new Vector3(-.65f, 0, -.65f), new Vector3(.65f, 0, -.65f),
                new Vector3(-.65f, 0, .65f), new Vector3(.65f, 0, .65f) };
            for (int i = 0; i < positions.Length; i++)
            {
                var point = positions[i];
                if (!Physics.Raycast(point + Vector3.up, Vector3.down, out var floor, 2, 1, QueryTriggerInteraction.Ignore))
                    throw new InvalidOperationException("Spawn has no floor: " + point);
                point.y = floor.point.y + .01f;
                var hits = Physics.OverlapCapsule(point + Vector3.up * .4f, point + Vector3.up * 1.65f,
                    .35f, 1, QueryTriggerInteraction.Ignore);
                if (hits.Length > 0) throw new InvalidOperationException("Blocked spawn: " + string.Join(",", hits.Select(h => h.name)));
                world.Spawns[i].SetPositionAndRotation(point, Quaternion.identity);
            }
        }

        private static Transform Child(Transform parent, string name, Vector3 localPosition)
        {
            var existing = parent.Find(name);
            if (existing != null) return existing;
            var child = new GameObject(name).transform;
            child.SetParent(parent, false);
            child.localPosition = localPosition;
            return child;
        }
    }
}
