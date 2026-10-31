using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NatureCalls.Editor
{
    public static class WCPlaygroundLayoutBuilder
    {
        private const string ScenePath = "Assets/Scenes/Playground.unity";
        private const string LayoutName = "WC Layout v2";
        private const string StallPrefabPath = "Assets/Prefabs/Levels/WCStall.prefab";
        private const string FloorPath = "Assets/UnityTechnologies/Basic Asset Pack Interior/Prefabs/Floor/Floor2m.prefab";
        private const string WallPath = "Assets/UnityTechnologies/Basic Asset Pack Interior/Prefabs/Walls/Wall2m.prefab";
        private const string DividerPath = "Assets/UnityTechnologies/Basic Asset Pack Interior/Prefabs/Walls/Wall1m.prefab";
        private const string ShelfPath = "Assets/UnityTechnologies/Basic Asset Pack Interior/Prefabs/Furniture/ShelvesMediumA.prefab";
        private const string CubiclePath = "Assets/WC/Prefabs/props/SM_toilet_cubicle Variant.prefab";
        private const string ToiletPath = "Assets/WC/Prefabs/props/SM_toilet_bowl Variant.prefab";
        private const string UrinalPath = "Assets/WC/Prefabs/props/SM_urinal Variant.prefab";
        private const string SinkPath = "Assets/WC/Prefabs/props/SM_sink Variant.prefab";
        private const string MirrorPath = "Assets/WC/Prefabs/props/SM_mirror.prefab";
        private const string PaperPath = "Assets/WC/Prefabs/props/SM_toilet_paper Variant.prefab";

        [MenuItem("NatureCalls/Rebuild Playground WC Layout")]
        public static void Build()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath) throw new InvalidOperationException($"Open {ScenePath} first.");
            if (scene.GetRootGameObjects().Any(item => item.name == LayoutName))
                throw new InvalidOperationException($"{LayoutName} already exists; refusing to overwrite it.");
            if (AssetDatabase.LoadAssetAtPath<GameObject>(StallPrefabPath) != null)
                throw new InvalidOperationException($"{StallPrefabPath} already exists; refusing to overwrite it.");

            var floor = Load(FloorPath);
            var wall = Load(WallPath);
            var divider = Load(DividerPath);
            var shelf = Load(ShelfPath);
            var cubicle = Load(CubiclePath);
            var toilet = Load(ToiletPath);
            var urinal = Load(UrinalPath);
            var sink = Load(SinkPath);
            var mirror = Load(MirrorPath);
            var paper = Load(PaperPath);

            Undo.SetCurrentGroupName("Rebuild compact WC Playground layout");
            var undoGroup = Undo.GetCurrentGroup();
            DisableLegacy(scene, "UnityTechnologies Layout", "Legacy UnityTechnologies Layout (inactive)");
            DisableLegacy(scene, "WC", "Legacy WC placement (inactive)");

            var root = NewGroup(LayoutName, null);
            var shell = NewGroup("8m x 10m room shell", root);
            var floorGroup = NewGroup("Floor - 2m square grid", shell);
            var wallGroup = NewGroup("Closed outer walls", shell);
            BuildRoom(floor, wall, floorGroup, wallGroup);

            var stalls = NewGroup("Three reusable WC stalls", root);
            BuildStalls(cubicle, toilet, stalls, scene);

            var urinals = NewGroup("Right wall urinals", root);
            foreach (var z in new[] { -2f, -1f, 0f })
                Place(urinal, urinals, $"Urinal {z:F0}", new Vector3(4.979f, 0, z), -90);
            foreach (var z in new[] { -1.5f, -.5f })
            {
                var panel = Place(divider, urinals, $"Urinal divider {z:F1}", new Vector3(4.990f, 0, z), 180);
                panel.transform.localScale = new Vector3(.7f, .6f, 1);
            }

            var sinks = NewGroup("Left wall sinks and mirrors", root);
            foreach (var z in new[] { -1.75f, -.25f })
            {
                Place(sink, sinks, $"Sink {z:F2}", new Vector3(-2.990f, 0, z), 90);
                Place(mirror, sinks, $"Mirror {z:F2}", new Vector3(-2.990f, 0, z), 90);
            }

            var storage = NewGroup("Storage and paper", root);
            Place(shelf, storage, "Back-left shelf", new Vector3(-2.859f, 0, 3.385f), 0);
            Place(paper, storage, "Paper roll 1", new Vector3(-3.059f, 1.05f, 3.285f), 0);
            Place(paper, storage, "Paper roll 2", new Vector3(-2.759f, 1.05f, 3.285f), 0);
            Place(paper, storage, "Paper roll 3", new Vector3(-2.909f, 1.05f, 3.555f), 90);
            Place(shelf, storage, "Front shelf near urinals", new Vector3(2.33f, 0, -5.738f), 90);

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save Playground.unity.");
            AssetDatabase.SaveAssets();
            Undo.CollapseUndoOperations(undoGroup);
            Selection.activeGameObject = root.gameObject;
            SceneView.lastActiveSceneView?.FrameSelected();
            Debug.Log("Built compact WC Layout v2: 8m x 10m room with 3m outer walls sized for Peasant Nolant, three WC stall prefab instances, three urinals with dividers, two sinks with mirrors, and two shelves.");
        }

        private static void BuildRoom(GameObject floor, GameObject wall, Transform floorGroup, Transform wallGroup)
        {
            for (var z = -4; z <= 4; z += 2)
                for (var x = -3; x <= 3; x += 2)
                    Place(floor, floorGroup, $"Floor {x},{z}", new Vector3(x, 0, z), 0);
            for (var x = -3; x <= 3; x += 2)
            {
                Place(wall, wallGroup, $"Back wall {x}", new Vector3(x, 0, 4), 0);
                Place(wall, wallGroup, $"Front wall {x + 2}", new Vector3(x + 2, 0, -6), 180);
            }
            for (var z = -4; z <= 4; z += 2)
            {
                Place(wall, wallGroup, $"Left wall {z}", new Vector3(-3, 0, z), 90);
                Place(wall, wallGroup, $"Right wall {z - 2}", new Vector3(5, 0, z - 2), -90);
            }
        }

        private static void BuildStalls(GameObject cubicle, GameObject toilet, Transform parent, Scene scene)
        {
            var first = new GameObject("WC Stall 1");
            Undo.RegisterCreatedObjectUndo(first, "Create WC stall source");
            first.transform.SetParent(parent, false);
            first.transform.SetPositionAndRotation(new Vector3(-1f, 0, 3.85f), Quaternion.identity);
            PlaceLocal(cubicle, first.transform, "Cubicle", Vector3.zero, 0);
            PlaceLocal(toilet, first.transform, "Toilet", new Vector3(.65f, 0, -.48f), 0);
            var connected = PrefabUtility.SaveAsPrefabAssetAndConnect(first, StallPrefabPath, InteractionMode.AutomatedAction);
            if (connected == null) throw new IOException($"Could not create {StallPrefabPath}.");
            connected.name = "WC Stall 1";
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(StallPrefabPath);
            CreateStallInstance(asset, parent, scene, "WC Stall 2", new Vector3(.35f, 0, 3.85f));
            CreateStallInstance(asset, parent, scene, "WC Stall 3", new Vector3(1.70f, 0, 3.85f));
        }

        private static void CreateStallInstance(GameObject prefab, Transform parent, Scene scene, string name, Vector3 position)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            if (instance == null) throw new IOException("Could not instantiate WCStall.prefab.");
            Undo.RegisterCreatedObjectUndo(instance, "Create WC stall instance");
            instance.name = name;
            instance.transform.SetParent(parent, false);
            instance.transform.SetPositionAndRotation(position, Quaternion.identity);
        }

        private static GameObject Load(string path)
        {
            var result = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (result == null) throw new InvalidOperationException($"Missing prefab: {path}");
            return result;
        }

        private static Transform NewGroup(string name, Transform parent)
        {
            var group = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(group, "Create WC layout group");
            if (parent != null) group.transform.SetParent(parent, false);
            return group.transform;
        }

        private static GameObject Place(GameObject prefab, Transform parent, string name, Vector3 position, float yRotation)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent.gameObject.scene);
            if (instance == null) throw new IOException($"Could not instantiate {prefab.name}.");
            Undo.RegisterCreatedObjectUndo(instance, "Place WC layout object");
            instance.name = name;
            instance.transform.SetParent(parent, false);
            instance.transform.SetPositionAndRotation(position, Quaternion.Euler(0, yRotation, 0));
            AddColliderIfMissing(instance);
            return instance;
        }

        private static void PlaceLocal(GameObject prefab, Transform parent, string name, Vector3 localPosition, float localYRotation)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent.gameObject.scene);
            if (instance == null) throw new IOException($"Could not instantiate {prefab.name}.");
            Undo.RegisterCreatedObjectUndo(instance, "Place WC stall object");
            instance.name = name;
            instance.transform.SetParent(parent, false);
            instance.transform.SetLocalPositionAndRotation(localPosition, Quaternion.Euler(0, localYRotation, 0));
            AddColliderIfMissing(instance);
        }

        private static void AddColliderIfMissing(GameObject instance)
        {
            if (instance.GetComponentInChildren<Collider>() != null) return;
            var renderers = instance.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return;
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            var collider = Undo.AddComponent<BoxCollider>(instance);
            collider.center = instance.transform.InverseTransformPoint(bounds.center);
            var size = instance.transform.InverseTransformVector(bounds.size);
            collider.size = new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z));
        }

        private static void DisableLegacy(Scene scene, string currentName, string legacyName)
        {
            var target = scene.GetRootGameObjects().SingleOrDefault(item => item.name == currentName);
            if (target == null) return;
            Undo.RecordObject(target, "Disable legacy WC layout");
            target.name = legacyName;
            target.SetActive(false);
        }
    }
}
