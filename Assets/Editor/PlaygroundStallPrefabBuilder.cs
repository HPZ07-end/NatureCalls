using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NatureCalls.Editor
{
    public static class PlaygroundStallPrefabBuilder
    {
        private const string ScenePath = "Assets/Scenes/Playground.unity";
        private const string LayoutName = "UnityTechnologies Layout";
        private const string LegacyGroupName = "Three adjacent stalls";
        private const string InstanceGroupName = "Stall Prefab Instances";
        private const string StallPrefabPath = "Assets/Prefabs/Levels/Stall6x8.prefab";
        private const string WallPath = "Assets/UnityTechnologies/Basic Asset Pack Interior/Prefabs/Walls/Wall2m.prefab";
        private const string DoorPath = "Assets/UnityTechnologies/Basic Asset Pack Interior/Prefabs/Walls/WallDoor2m.prefab";

        [MenuItem("NatureCalls/Build Reusable Playground Stalls")]
        public static void Build()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath)
                throw new InvalidOperationException($"Open {ScenePath} before building reusable stalls.");
            if (AssetDatabase.LoadAssetAtPath<GameObject>(StallPrefabPath) != null)
                throw new InvalidOperationException($"{StallPrefabPath} already exists; refusing to overwrite it.");

            var layout = scene.GetRootGameObjects().SingleOrDefault(item => item.name == LayoutName);
            if (layout == null) throw new InvalidOperationException($"{LayoutName} is missing.");
            if (layout.transform.Find(InstanceGroupName) != null)
                throw new InvalidOperationException($"{InstanceGroupName} already exists.");

            var wallPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(WallPath);
            var doorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DoorPath);
            if (wallPrefab == null || doorPrefab == null)
                throw new InvalidOperationException("UnityTechnologies Wall2m or WallDoor2m prefab is missing.");
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs/Levels"))
                throw new DirectoryNotFoundException("Assets/Prefabs/Levels is missing.");

            Undo.SetCurrentGroupName("Build reusable 6x8 stalls");
            var undoGroup = Undo.GetCurrentGroup();

            var legacy = layout.transform.Find(LegacyGroupName);
            if (legacy != null)
            {
                Undo.RecordObject(legacy.gameObject, "Mark legacy stall dividers");
                legacy.name = "Legacy stall dividers (inactive)";
                legacy.gameObject.SetActive(false);
            }

            var instances = new GameObject(InstanceGroupName);
            Undo.RegisterCreatedObjectUndo(instances, "Create stall instance group");
            instances.transform.SetParent(layout.transform, false);

            var first = new GameObject("Stall 1");
            Undo.RegisterCreatedObjectUndo(first, "Create reusable stall source");
            first.transform.SetParent(instances.transform, false);
            first.transform.SetPositionAndRotation(new Vector3(-6, 0, 3), Quaternion.identity);
            BuildStallContents(first.transform, wallPrefab, doorPrefab);

            var firstInstance = PrefabUtility.SaveAsPrefabAssetAndConnect(first, StallPrefabPath, InteractionMode.AutomatedAction);
            if (firstInstance == null)
                throw new IOException($"Could not create {StallPrefabPath}.");
            firstInstance.name = "Stall 1";

            CreateInstance(firstInstance, instances.transform, "Stall 2", new Vector3(0, 0, 3));
            CreateInstance(firstInstance, instances.transform, "Stall 3", new Vector3(6, 0, 3));

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new IOException("Unity could not save Playground.unity.");
            AssetDatabase.SaveAssets();
            Undo.CollapseUndoOperations(undoGroup);
            Selection.activeGameObject = instances;
            SceneView.lastActiveSceneView?.FrameSelected();
            Debug.Log("Created Stall6x8 prefab and placed three prefab instances with WallDoor2m doors.");
        }

        [MenuItem("NatureCalls/Repair Reusable Playground Stalls")]
        public static void Repair()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath)
                throw new InvalidOperationException($"Open {ScenePath} before repairing reusable stalls.");
            var stallPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(StallPrefabPath);
            var wallPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(WallPath);
            if (stallPrefab == null || wallPrefab == null)
                throw new InvalidOperationException("Stall6x8 or UnityTechnologies Wall2m prefab is missing.");

            AddFrontClosure(wallPrefab);
            stallPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(StallPrefabPath);

            var layout = scene.GetRootGameObjects().SingleOrDefault(item => item.name == LayoutName);
            var instanceGroup = layout?.transform.Find(InstanceGroupName);
            if (instanceGroup == null)
                throw new InvalidOperationException($"{InstanceGroupName} is missing from the partial build.");

            Undo.SetCurrentGroupName("Repair reusable 6x8 stalls");
            var undoGroup = Undo.GetCurrentGroup();
            var instances = instanceGroup.Cast<Transform>().Select(item => item.gameObject).ToList();
            if (instances.Count > 3)
                throw new InvalidOperationException($"Expected at most 3 partial stall instances, found {instances.Count}.");
            while (instances.Count < 3)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(stallPrefab, scene);
                if (instance == null) throw new IOException("Unity could not instantiate Stall6x8.prefab.");
                Undo.RegisterCreatedObjectUndo(instance, "Create repaired stall instance");
                instance.transform.SetParent(instanceGroup, false);
                instances.Add(instance);
            }

            var positions = new[] { new Vector3(-6, 0, 3), new Vector3(0, 0, 3), new Vector3(6, 0, 3) };
            for (var index = 0; index < 3; index++)
            {
                var instance = instances[index];
                Undo.RecordObject(instance, "Rename repaired stall instance");
                Undo.RecordObject(instance.transform, "Position repaired stall instance");
                instance.name = $"Stall {index + 1}";
                instance.transform.SetPositionAndRotation(positions[index], Quaternion.identity);
                instance.SetActive(true);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new IOException("Unity could not save repaired Playground.unity.");
            AssetDatabase.SaveAssets();
            Undo.CollapseUndoOperations(undoGroup);
            Selection.activeGameObject = instanceGroup.gameObject;
            SceneView.lastActiveSceneView?.FrameSelected();
            Debug.Log("Repaired Stall6x8 prefab: three shared prefab instances, each closed by two Wall2m modules and one WallDoor2m door.");
        }

        private static void AddFrontClosure(GameObject wallPrefab)
        {
            var contents = PrefabUtility.LoadPrefabContents(StallPrefabPath);
            try
            {
                var closure = contents.transform.Find("Front closure");
                if (closure == null)
                {
                    var closureObject = new GameObject("Front closure");
                    closureObject.transform.SetParent(contents.transform, false);
                    closure = closureObject.transform;
                }

                var door = contents.transform.Find("Door");
                if (door != null && door.parent != closure)
                    door.SetParent(closure, false);
                if (door != null)
                    door.SetLocalPositionAndRotation(new Vector3(1, 0, 0), Quaternion.Euler(0, 180, 0));

                if (closure.Find("Front left wall") == null)
                    PlaceInPrefabContents(wallPrefab, contents.scene, closure, "Front left wall", new Vector3(-1, 0, 0), 180);
                if (closure.Find("Front right wall") == null)
                    PlaceInPrefabContents(wallPrefab, contents.scene, closure, "Front right wall", new Vector3(3, 0, 0), 180);

                if (PrefabUtility.SaveAsPrefabAsset(contents, StallPrefabPath) == null)
                    throw new IOException($"Could not update {StallPrefabPath}.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        private static void PlaceInPrefabContents(GameObject prefab, Scene scene, Transform parent, string name, Vector3 localPosition, float localYRotation)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            if (instance == null) throw new IOException($"Could not instantiate {prefab.name} in prefab contents.");
            instance.name = name;
            instance.transform.SetParent(parent, false);
            instance.transform.SetLocalPositionAndRotation(localPosition, Quaternion.Euler(0, localYRotation, 0));
            if (instance.GetComponentInChildren<Collider>() == null) AddBoundsColliderWithoutUndo(instance);
        }

        private static void BuildStallContents(Transform stall, GameObject wallPrefab, GameObject doorPrefab)
        {
            var left = CreateGroup("Left divider", stall);
            var right = CreateGroup("Right divider", stall);
            foreach (var z in new[] { 2, 4, 6, 8 })
            {
                Place(wallPrefab, left, $"Wall {z}", new Vector3(-3, 0, z), 90);
                Place(wallPrefab, right, $"Wall {z}", new Vector3(3, 0, z), 90);
            }
            Place(doorPrefab, stall, "Door", new Vector3(1, 0, 0), 180);
        }

        private static Transform CreateGroup(string name, Transform parent)
        {
            var group = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(group, "Create stall group");
            group.transform.SetParent(parent, false);
            return group.transform;
        }

        private static void Place(GameObject prefab, Transform parent, string name, Vector3 localPosition, float localYRotation)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent.gameObject.scene);
            Undo.RegisterCreatedObjectUndo(instance, "Place stall module");
            instance.name = name;
            instance.transform.SetParent(parent, false);
            instance.transform.SetLocalPositionAndRotation(localPosition, Quaternion.Euler(0, localYRotation, 0));
            if (instance.GetComponentInChildren<Collider>() == null) AddBoundsCollider(instance);
        }

        private static void CreateInstance(GameObject sourceInstance, Transform parent, string name, Vector3 position)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(StallPrefabPath);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent.gameObject.scene);
            if (instance == null) throw new IOException("Unity could not instantiate Stall6x8.prefab.");
            Undo.RegisterCreatedObjectUndo(instance, "Create stall prefab instance");
            instance.name = name;
            instance.transform.SetParent(parent, false);
            instance.transform.SetPositionAndRotation(position, Quaternion.identity);
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

        private static void AddBoundsColliderWithoutUndo(GameObject instance)
        {
            var renderers = instance.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            var collider = instance.AddComponent<BoxCollider>();
            collider.center = instance.transform.InverseTransformPoint(bounds.center);
            var size = instance.transform.InverseTransformVector(bounds.size);
            collider.size = new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z));
        }
    }
}
