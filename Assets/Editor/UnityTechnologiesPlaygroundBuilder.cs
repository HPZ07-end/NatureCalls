using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NatureCalls.Editor
{
    public static class UnityTechnologiesPlaygroundBuilder
    {
        private const string ScenePath = "Assets/Scenes/Playground.unity";
        private const string RootName = "UnityTechnologies Layout";
        private const string FloorPath = "Assets/UnityTechnologies/Basic Asset Pack Interior/Prefabs/Floor/Floor2m.prefab";
        private const string WallPath = "Assets/UnityTechnologies/Basic Asset Pack Interior/Prefabs/Walls/Wall2m.prefab";

        [MenuItem("NatureCalls/Build UnityTechnologies Playground Shell")]
        public static void Build()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath)
                throw new InvalidOperationException($"Open {ScenePath} before building the layout.");

            var floorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(FloorPath);
            var wallPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(WallPath);
            if (floorPrefab == null || wallPrefab == null)
                throw new InvalidOperationException("UnityTechnologies Floor2m or Wall2m prefab is missing.");

            Undo.SetCurrentGroupName("Update UnityTechnologies Playground shell");
            var undoGroup = Undo.GetCurrentGroup();
            DisablePlaceholderShell(scene);

            var root = scene.GetRootGameObjects().SingleOrDefault(item => item.name == RootName);
            if (root == null)
            {
                root = new GameObject(RootName);
                Undo.RegisterCreatedObjectUndo(root, "Create UnityTechnologies layout");
            }
            var floors = FindOrCreateGroup("Floor - 2m square grid", root.transform);
            var outerWalls = FindOrCreateGroup("Outer walls - 2m modules", root.transform);
            var stalls = FindOrCreateGroup("Three adjacent stalls", root.transform);
            var frontWalls = FindOrCreateGroup("Front walls", outerWalls);
            var backWalls = FindOrCreateGroup("Back walls", outerWalls);
            var leftWalls = FindOrCreateGroup("Left walls", outerWalls);
            var rightWalls = FindOrCreateGroup("Right walls", outerWalls);

            var floorPlacements = new List<Placement>();
            for (var z = -11; z <= 11; z += 2)
                for (var x = -14; x <= 14; x += 2)
                    floorPlacements.Add(new Placement($"Floor {x},{z}", new Vector3(x, 0, z), 0));

            var frontWallPlacements = new List<Placement>();
            var backWallPlacements = new List<Placement>();
            for (var x = -14; x <= 14; x += 2)
            {
                backWallPlacements.Add(new Placement($"Back wall {x}", new Vector3(x, 0, 11), 0));
                frontWallPlacements.Add(new Placement($"Front wall {x + 2}", new Vector3(x + 2, 0, -13), 180));
            }
            var leftWallPlacements = new List<Placement>();
            var rightWallPlacements = new List<Placement>();
            for (var z = -11; z <= 11; z += 2)
            {
                leftWallPlacements.Add(new Placement($"Left wall {z}", new Vector3(-14, 0, z), 90));
                rightWallPlacements.Add(new Placement($"Right wall {z - 2}", new Vector3(16, 0, z - 2), -90));
            }

            var stallPlacements = new List<Placement>();
            foreach (var x in new[] { -9, -3, 3, 9 })
                foreach (var z in new[] { 5, 7, 9, 11 })
                    stallPlacements.Add(new Placement($"Stall divider {x},{z}", new Vector3(x, 0, z), 90));

            ApplyPlacements(floorPrefab, floors, floorPlacements);
            ApplyPlacements(wallPrefab, frontWalls, frontWallPlacements);
            ApplyPlacements(wallPrefab, backWalls, backWallPlacements);
            ApplyPlacements(wallPrefab, leftWalls, leftWallPlacements);
            ApplyPlacements(wallPrefab, rightWalls, rightWallPlacements);
            ApplyPlacements(wallPrefab, stalls, stallPlacements);

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("Unity could not save Playground.unity.");
            Undo.CollapseUndoOperations(undoGroup);
            Selection.activeGameObject = root;
            SceneView.lastActiveSceneView?.FrameSelected();
            Debug.Log("Updated UnityTechnologies Playground shell: 30m x 24m closed room, three centered adjacent 6m x 8m stalls against the back wall.");
        }

        private static Transform FindOrCreateGroup(string name, Transform parent)
        {
            var existing = parent.Find(name);
            if (existing != null) return existing;
            var group = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(group, "Create layout group");
            group.transform.SetParent(parent, false);
            return group.transform;
        }

        private static void ApplyPlacements(GameObject prefab, Transform parent, IReadOnlyList<Placement> placements)
        {
            var instances = parent.Cast<Transform>().Select(item => item.gameObject).ToList();
            if (instances.Count > placements.Count)
                throw new InvalidOperationException($"{parent.name} has {instances.Count} modules but the target layout only needs {placements.Count}; refusing to delete user objects.");
            while (instances.Count < placements.Count)
                instances.Add(Place(prefab, parent));
            for (var index = 0; index < placements.Count; index++)
            {
                var instance = instances[index];
                var placement = placements[index];
                Undo.RecordObject(instance, "Rename UnityTechnologies module");
                Undo.RecordObject(instance.transform, "Move UnityTechnologies module");
                instance.name = placement.Name;
                instance.transform.SetPositionAndRotation(placement.Position, Quaternion.Euler(0, placement.YRotation, 0));
                instance.SetActive(true);
            }
        }

        private static GameObject Place(GameObject prefab, Transform parent)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent.gameObject.scene);
            Undo.RegisterCreatedObjectUndo(instance, "Place UnityTechnologies module");
            instance.transform.SetParent(parent, false);
            if (instance.GetComponentInChildren<Collider>() == null)
                AddBoundsCollider(instance);
            return instance;
        }

        private readonly struct Placement
        {
            public readonly string Name;
            public readonly Vector3 Position;
            public readonly float YRotation;
            public Placement(string name, Vector3 position, float yRotation)
            {
                Name = name;
                Position = position;
                YRotation = yRotation;
            }
        }

        private static void AddBoundsCollider(GameObject instance)
        {
            var renderers = instance.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            var collider = Undo.AddComponent<BoxCollider>(instance);
            collider.center = instance.transform.InverseTransformPoint(bounds.center);
            var size = instance.transform.InverseTransformVector(bounds.size);
            collider.size = new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z));
        }

        private static void DisablePlaceholderShell(Scene scene)
        {
            var names = new[]
            {
                "Floor", "Back wall", "Front wall", "Left wall", "Right wall",
                "Stall left", "Stall right", "Stall back", "Entrance left", "Entrance right"
            };
            foreach (var item in scene.GetRootGameObjects().Where(item => names.Contains(item.name)))
            {
                Undo.RecordObject(item, "Disable placeholder shell");
                item.SetActive(false);
            }
        }
    }
}
