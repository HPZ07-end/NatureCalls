using System;
using System.IO;
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
    public static class Map3Integration
    {
        public const string ScenePath = "Assets/Scenes/Map3.unity";
        private const string ModelPath = "Assets/Art/Map3/Map3_Backrooms.fbx";

        [MenuItem("NatureCalls/Connect Map3 To Lobby")]
        public static void Apply()
        {
            if (!File.Exists(ModelPath)) throw new FileNotFoundException("Map3 FBX is missing", ModelPath);
            ConfigureModelImport();
            var scene = File.Exists(ScenePath) ? EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single) : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = scene.GetRootGameObjects().FirstOrDefault(o => o.name == "Map3_Backrooms");
            if (root == null)
            {
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
                root = PrefabUtility.InstantiatePrefab(model, scene) as GameObject;
                root.name = "Map3_Backrooms";
            }
            ConfigureGeometry(root.transform);
            ConfigureWorld(scene, root.transform);
            ConfigureLobby(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new InvalidOperationException("Could not save Map3.");
            AssetDatabase.SaveAssets();
            Debug.Log("MAP3_LOBBY_INTEGRATION_OK");
        }

        private static void ConfigureModelImport()
        {
            var importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
            if (importer == null) return;
            importer.bakeAxisConversion = true;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            importer.SaveAndReimport();
        }

        private static void ConfigureGeometry(Transform root)
        {
            foreach (var mesh in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                var collider = mesh.GetComponent<Collider>();
                if (collider == null) collider = mesh.gameObject.AddComponent<BoxCollider>();
                if (collider is BoxCollider box)
                {
                    var bounds = mesh.bounds;
                    box.center = bounds.center;
                    box.size = bounds.size;
                }
            }
            foreach (var name in new[] { "Segment_N", "Segment_M" })
            {
                var segment = root.Find(name); if (segment == null) continue;
                var pipe = segment.GetComponent<Pipe>() ?? segment.gameObject.AddComponent<Pipe>();
                foreach (var stall in segment.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith("Stall_", StringComparison.Ordinal)))
                    ConfigureStall(stall, pipe);
            }
            var roomRoot = root.Find("Rooms") ?? root;
            foreach (var mirror in roomRoot.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith("Mirror_", StringComparison.Ordinal)))
                if (mirror.GetComponent<PlanarMirror>() == null) mirror.gameObject.AddComponent<PlanarMirror>();
            if (root.GetComponent<NearestMirrorSelector>() == null) root.gameObject.AddComponent<NearestMirrorSelector>();
            foreach (var flicker in root.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith("Light_Flicker_", StringComparison.Ordinal)))
            {
                var light = flicker.GetComponentInChildren<Light>();
                if (light == null) { light = new GameObject("Realtime light").AddComponent<Light>(); light.type = LightType.Point; light.range = 8; light.intensity = 2.5f; light.transform.SetParent(flicker, false); }
                if (flicker.GetComponent<LightFlicker>() == null) flicker.gameObject.AddComponent<LightFlicker>();
            }
            ConfigurePortal(root, "Portal_Forward", 1, new Vector3(-40, -8.8f, 0));
            ConfigurePortal(root, "Portal_Back", -1, new Vector3(40, 8.8f, 0));
        }

        private static void ConfigureStall(Transform stall, Pipe pipe)
        {
            var doorLeaf = stall.Find(stall.name.Replace("Stall_", "Door_"));
            var door = stall.GetComponent<StallDoor>() ?? stall.gameObject.AddComponent<StallDoor>();
            door.DoorLeaf = door.DoorColliderPivot = doorLeaf ?? stall;
            door.OpenAngle = stall.name.Contains("Segment_M", StringComparison.Ordinal) ? -95 : 95;
            var kind = GetKind(stall.name);
            SetPrivate(door, "initialState", kind is "Engaged" or "OutOfOrder" ? StallDoorState.Locked : StallDoorState.Closed);
            var toiletVisual = stall.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name.StartsWith("Toilet_", StringComparison.Ordinal));
            if (toiletVisual == null || kind is "PipeChase" or "Janitor" or "Empty") return;
            var toilet = toiletVisual.GetComponent<Toilet>() ?? toiletVisual.gameObject.AddComponent<Toilet>();
            toilet.Interior = toiletVisual;
            toilet.Anchor = Child(toiletVisual, "Posture anchor", Vector3.back * .65f + Vector3.up * .15f);
            toilet.SetPipe(pipe);
            toilet.SetFlushTarget(Child(toiletVisual, "Flush handle target", Vector3.up * .8f + Vector3.forward * .2f));
            foreach (var socket in stall.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith("Socket_PaperRoll", StringComparison.Ordinal)))
            {
                var rollPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Models/Supplies/Supplies-PaperRoll.fbx");
                if (rollPrefab == null || socket.name.EndsWith("_Floor", StringComparison.Ordinal)) continue;
                var item = Object.Instantiate(rollPrefab, socket.position, socket.rotation, socket).AddComponent<PaperRoll>();
                item.name = "PaperRoll";
                item.gameObject.AddComponent<BoxCollider>(); item.gameObject.AddComponent<Rigidbody>().isKinematic = true;
            }
            var toolboxSocket = stall.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name.StartsWith("Socket_Toolbox", StringComparison.Ordinal));
            var mopSocket = stall.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name.StartsWith("Socket_Mop", StringComparison.Ordinal));
            if (toolboxSocket != null && mopSocket != null)
            {
                var box = toolboxSocket.gameObject.GetComponent<Toolbox>() ?? toolboxSocket.gameObject.AddComponent<Toolbox>();
                var mop = mopSocket.gameObject.GetComponent<Mop>() ?? mopSocket.gameObject.AddComponent<Mop>();
                box.Mop = mop; toolboxSocket.gameObject.AddComponent<BoxCollider>(); mopSocket.gameObject.AddComponent<BoxCollider>(); mopSocket.gameObject.AddComponent<Rigidbody>().isKinematic = true;
            }
        }

        private static void ConfigurePortal(Transform root, string name, int direction, Vector3 offset)
        {
            var marker = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name); if (marker == null) return;
            var portal = marker.GetComponent<Portal>() ?? marker.gameObject.AddComponent<Portal>(); portal.CrossingDirectionX = direction; portal.Offset = offset; portal.ZoneSize = new Vector3(4.4f, 2.6f, 6.6f);
        }

        private static void ConfigureWorld(Scene scene, Transform root)
        {
            var world = scene.GetRootGameObjects().SelectMany(o => o.GetComponentsInChildren<Map.Playground>(true)).FirstOrDefault();
            if (world == null) world = new GameObject("Playground").AddComponent<Map.Playground>();
            world.PlayerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerAppearanceSetup.PlayerPath); world.StartOffline = true;
            world.Spawns = Enumerable.Range(1, 4).Select(i => root.GetComponentsInChildren<Transform>(true).First(t => t.name == $"Marker_Spawn_{i:00}")).ToArray();
            var camera = Object.FindFirstObjectByType<CameraController>(); if (camera != null) world.Camera = camera;
        }

        private static void ConfigureLobby(Scene scene)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<MapCatalog>(LobbyFlowBuilder.MapCatalogPath); var entries = Enumerable.Range(0, catalog.Count).Select(i => catalog[i]).ToList();
            if (entries.All(e => e.ScenePath != ScenePath)) entries.Add(new MapCatalog.Entry { DisplayName = "后室", ScenePath = ScenePath }); catalog.SetMaps(entries.ToArray()); EditorUtility.SetDirty(catalog);
            EditorBuildSettings.scenes = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).Append(new EditorBuildSettingsScene(ScenePath, true)).ToArray();
            LobbyFlowBuilder.AddMatchHud();
        }

        private static string GetKind(string name) => new[] { "Normal", "Ajar", "Engaged", "PipeChase", "Janitor", "StallInStall", "Empty", "OutOfOrder" }.FirstOrDefault(k => name.Contains(k, StringComparison.Ordinal)) ?? "Normal";
        private static Transform Child(Transform parent, string name, Vector3 local) { var t = parent.Find(name) ?? new GameObject(name).transform; t.SetParent(parent, false); t.localPosition = local; return t; }
        private static void SetPrivate<T>(Component target, string name, T value) { var so = new SerializedObject(target); so.FindProperty(name).boxedValue = value; so.ApplyModifiedPropertiesWithoutUndo(); }
    }
}
