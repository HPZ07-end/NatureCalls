using System;
using System.Collections.Generic;
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

        public static void CaptureCheckScreenshots()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var world = scene.GetRootGameObjects().SelectMany(o => o.GetComponentsInChildren<NatureCalls.Map.Playground>(true)).FirstOrDefault();
            var camera = world?.Camera?.Output ?? UnityEngine.Object.FindFirstObjectByType<UnityEngine.Camera>();
            if (camera == null) throw new InvalidOperationException("Map3 has no output camera for screenshots.");
            var directory = Path.Combine(Application.dataPath, "..", "Artifacts", "Map3Check");
            Directory.CreateDirectory(directory);
            RenderShot(camera, directory, "spawn_level", new Vector3(6, 1.55f, 1.0f), new Vector3(8, 1.25f, 2.2f));
            RenderShot(camera, directory, "ceiling", new Vector3(6, 1.55f, 2.2f), new Vector3(6, 2.6f, 2.2f));
            RenderShot(camera, directory, "stall", new Vector3(6, 1.4f, 1.65f), new Vector3(6, 1.2f, 2.45f));
            RenderShot(camera, directory, "mirror", new Vector3(1.5f, 1.55f, 1.5f), new Vector3(0, 1.2f, 0));
            var mapRoot = scene.GetRootGameObjects().FirstOrDefault(o => o.name == "Map3_Backrooms")?.transform;
            var forward = mapRoot == null ? null : FindNamed(mapRoot, "Portal_Forward");
            var back = mapRoot == null ? null : FindNamed(mapRoot, "Portal_Back");
            if (forward != null) RenderShot(camera, directory, "portal_before", forward.position + Vector3.left * 2f + Vector3.up * 1.55f, forward.position + Vector3.up * 1.2f);
            if (back != null) RenderShot(camera, directory, "portal_after", back.position + Vector3.right * 2f + Vector3.up * 1.55f, back.position + Vector3.up * 1.2f);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"MAP3_SCREENSHOTS_OK|directory={directory}");
        }

        private static void RenderShot(UnityEngine.Camera camera, string directory, string name, Vector3 position, Vector3 target)
        {
            camera.enabled = false;
            camera.transform.SetPositionAndRotation(position, Quaternion.LookRotation(target - position, Vector3.up));
            var texture = new RenderTexture(960, 540, 24, RenderTextureFormat.ARGB32);
            var previous = RenderTexture.active;
            camera.targetTexture = texture;
            camera.Render();
            RenderTexture.active = texture;
            var image = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
            image.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
            image.Apply();
            File.WriteAllBytes(Path.Combine(directory, name + ".png"), image.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(image);
            camera.targetTexture = null;
            RenderTexture.active = previous;
            texture.Release();
            UnityEngine.Object.DestroyImmediate(texture);
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
                // Structure meshes combine floor, walls and ceiling. A bounds box fills
                // the corridor, so preserve the source triangles as static non-convex
                // collision. Replacing colliders makes this operation idempotent.
                foreach (var collider in mesh.GetComponents<Collider>()) Object.DestroyImmediate(collider);
                var filter = mesh.GetComponent<MeshFilter>();
                if (filter != null && filter.sharedMesh != null)
                    mesh.gameObject.AddComponent<MeshCollider>().sharedMesh = filter.sharedMesh;
            }
            EnsureWalkableFloorSupport(root);
            foreach (var name in new[] { "Segment_N", "Segment_M" })
            {
                var segment = root.Find(name); if (segment == null) continue;
                var pipe = segment.GetComponent<Pipe>() ?? segment.gameObject.AddComponent<Pipe>();
                foreach (var stall in segment.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith("Stall_", StringComparison.Ordinal)).ToArray())
                    ConfigureStall(stall, pipe);
            }
            Debug.Log($"MAP3_SOCKET_COUNTS|paper={root.GetComponentsInChildren<Transform>(true).Count(t => t.name.StartsWith("Socket_PaperRoll", StringComparison.Ordinal))}|holder={root.GetComponentsInChildren<Transform>(true).Count(t => t.name.StartsWith("Socket_RollHolder", StringComparison.Ordinal))}|toolbox={root.GetComponentsInChildren<Transform>(true).Count(t => t.name.StartsWith("Socket_Toolbox", StringComparison.Ordinal))}|mop={root.GetComponentsInChildren<Transform>(true).Count(t => t.name.StartsWith("Socket_Mop", StringComparison.Ordinal))}");
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
            var r0 = FindNamed(root, "Room_R0");
            var r2 = FindNamed(root, "Room_R2");
            if (r0 == null || r2 == null) throw new InvalidOperationException("Map3 requires Room_R0 and Room_R2 for portal offsets.");
            var offset = r0.position - r2.position;
            ConfigurePortal(root, "Portal_Forward", 1, offset);
            ConfigurePortal(root, "Portal_Back", -1, -offset);
            Debug.Log($"MAP3_PORTAL_LAYOUT|r0={r0.position}|r2={r2.position}|offset={offset}");
        }

        private static void ConfigureStall(Transform stall, Pipe pipe)
        {
            var children = stall.GetComponentsInChildren<Transform>(true).Where(t => t != null).ToArray();
            var cleanup = new List<GameObject>();
            var doorLeaf = children
                .FirstOrDefault(t => t.name == stall.name.Replace("Stall_", "Door_"));
            var door = stall.GetComponent<StallDoor>() ?? stall.gameObject.AddComponent<StallDoor>();
            door.DoorLeaf = door.DoorColliderPivot = doorLeaf ?? stall;
            var kind = GetKind(stall.name);
            var mirrored = stall.GetComponentsInParent<Transform>(true).Any(t => t.name == "Segment_M");
            door.OpenAngle = mirrored ? -95 : 95;
            if (kind == "Ajar") door.OpenAngle = mirrored ? -28 : 28;
            if (kind is "PipeChase" or "Janitor" or "Empty") door.OpenAngle = mirrored ? -80 : 80;
            SetPrivate(door, "initialState", kind is "Engaged" or "OutOfOrder" ? StallDoorState.Locked : StallDoorState.Closed);
            if (kind is "Ajar" or "PipeChase" or "Janitor" or "Empty") SetPrivate(door, "initialState", StallDoorState.Open);
            var toiletVisual = children.FirstOrDefault(t => t.name.StartsWith("Toilet_", StringComparison.Ordinal));
            if (toiletVisual != null && kind is not ("PipeChase" or "Janitor" or "Empty"))
            {
                var toilet = toiletVisual.GetComponent<Toilet>() ?? toiletVisual.gameObject.AddComponent<Toilet>();
                toilet.Interior = toiletVisual;
                toilet.Anchor = Child(toiletVisual, "Posture anchor", Vector3.back * .65f + Vector3.up * .15f);
                toilet.SetPipe(pipe);
                var flush = Child(toiletVisual, "Flush handle target", Vector3.up * .8f + Vector3.forward * .2f);
                toilet.SetFlushTarget(flush);
                var button = flush.GetComponent<ToiletFlushButton>();
                if (button == null) button = Undo.AddComponent<ToiletFlushButton>(flush.gameObject);
                button.SetToilet(toilet);
                flush.gameObject.SetActive(true);
                flush.gameObject.layer = 0;
                var flushCollider = flush.GetComponent<BoxCollider>();
                if (flushCollider == null) flushCollider = Undo.AddComponent<BoxCollider>(flush.gameObject);
                if (flushCollider == null) throw new InvalidOperationException("Could not add flush button collider.");
                flushCollider.size = new Vector3(.07f, .07f, .07f);
                foreach (var socket in children.Where(t => t.name.StartsWith("Socket_PaperRoll", StringComparison.Ordinal)).ToArray())
                {
                var rollPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Models/Supplies/Supplies-PaperRoll.fbx");
                if (rollPrefab == null) continue;
                var existingRolls = socket.GetComponentsInChildren<PaperRoll>(true);
                var itemObject = existingRolls.FirstOrDefault()?.gameObject;
                var duplicateObjects = existingRolls.Skip(1).Select(duplicate => duplicate.gameObject).ToArray();
                if (itemObject == null)
                {
                    itemObject = PrefabUtility.InstantiatePrefab(rollPrefab, socket.gameObject.scene) as GameObject;
                    itemObject.transform.SetParent(socket, false);
                }
                itemObject.name = "PaperRoll";
                var item = itemObject.GetComponent<PaperRoll>() ?? itemObject.AddComponent<PaperRoll>();
                item.InteractionDistance = 1.5f;
                if (itemObject.GetComponentInChildren<Collider>(true) == null) itemObject.AddComponent<BoxCollider>();
                var body = itemObject.GetComponent<Rigidbody>();
                if (body == null) body = Undo.AddComponent<Rigidbody>(itemObject);
                if (body == null) throw new InvalidOperationException($"Could not add Rigidbody to {itemObject.name}.");
                body.isKinematic = true; body.useGravity = false;
                    cleanup.AddRange(duplicateObjects);
                }
            }
            var holderSocket = children.FirstOrDefault(t => t.name.StartsWith("Socket_RollHolder", StringComparison.Ordinal));
            EnsureStaticModel(holderSocket, "Assets/Art/Models/Supplies/Supplies-PaperRollHolder.fbx", "PaperRollHolder");
            var toolboxSocket = children.FirstOrDefault(t => t.name.StartsWith("Socket_Toolbox", StringComparison.Ordinal));
            var mopSocket = children.FirstOrDefault(t => t.name.StartsWith("Socket_Mop", StringComparison.Ordinal));
            if (toolboxSocket != null && mopSocket != null)
            {
                EnsureStaticModel(toolboxSocket, "Assets/Art/Models/Tools/Tools-Toolbox.fbx", "Toolbox");
                EnsureStaticModel(mopSocket, "Assets/Art/Models/Tools/Tools-Mop.fbx", "Mop");
                var box = toolboxSocket.gameObject.GetComponent<Toolbox>() ?? toolboxSocket.gameObject.AddComponent<Toolbox>();
                var mop = mopSocket.gameObject.GetComponent<Mop>() ?? mopSocket.gameObject.AddComponent<Mop>();
                box.Mop = mop;
                if (toolboxSocket.GetComponentInChildren<Collider>(true) == null) toolboxSocket.gameObject.AddComponent<BoxCollider>();
                var body = mopSocket.GetComponent<Rigidbody>();
                if (body == null) body = Undo.AddComponent<Rigidbody>(mopSocket.gameObject);
                if (body == null) throw new InvalidOperationException($"Could not add Rigidbody to {mopSocket.name}.");
                body.isKinematic = true; body.useGravity = false;
            }
            foreach (var duplicate in cleanup) if (duplicate != null) Object.DestroyImmediate(duplicate);
        }

        private static GameObject EnsureStaticModel(Transform socket, string path, string name)
        {
            if (socket == null) return null;
            var existing = socket.Find(name)?.gameObject;
            if (existing != null) return existing;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) return null;
            var instance = PrefabUtility.InstantiatePrefab(prefab, socket.gameObject.scene) as GameObject;
            instance.name = name;
            instance.transform.SetParent(socket, false);
            return instance;
        }

        private static void EnsureWalkableFloorSupport(Transform root)
        {
            foreach (var section in root.GetComponentsInChildren<Transform>(true)
                         .Where(t => t.name.StartsWith("Corridor_", StringComparison.Ordinal) || t.name.StartsWith("Room_", StringComparison.Ordinal)))
            {
                var renderers = section.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length == 0) continue;
                var bounds = renderers.Select(r => r.bounds).Aggregate((a, b) => { a.Encapsulate(b); return a; });
                var support = root.Find($"Map3 Walkable Floor {section.name}")?.gameObject;
                if (support == null)
                {
                    support = new GameObject($"Map3 Walkable Floor {section.name}");
                    // Bounds are in Unity world axes; keep the support unrotated because
                    // the FBX root carries the Blender Z-up to Unity Y-up conversion.
                    support.transform.SetParent(null, true);
                    support.AddComponent<BoxCollider>();
                }
                else support.transform.SetParent(null, true);
                support.transform.position = new Vector3(bounds.center.x, bounds.min.y + .025f, bounds.center.z);
                var collider = support.GetComponent<BoxCollider>();
                collider.size = new Vector3(Mathf.Max(.1f, bounds.size.x), .05f, Mathf.Max(.1f, bounds.size.z));
                collider.center = Vector3.zero;
            }
        }

        private static void ConfigurePortal(Transform root, string name, int direction, Vector3 offset)
        {
            var marker = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name); if (marker == null) return;
            var portal = marker.GetComponent<Portal>() ?? marker.gameObject.AddComponent<Portal>();
            portal.CrossingDirectionX = direction; portal.CrossingAxis = Vector3.right; portal.Offset = offset; portal.ZoneSize = new Vector3(4.4f, 2.6f, 6.6f);
        }

        private static void ConfigureWorld(Scene scene, Transform root)
        {
            var world = scene.GetRootGameObjects().SelectMany(o => o.GetComponentsInChildren<Map.Playground>(true)).FirstOrDefault();
            if (world == null) world = new GameObject("Playground").AddComponent<Map.Playground>();
            world.PlayerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerAppearanceSetup.PlayerPath); world.StartOffline = true;
            if (world.Camera == null)
            {
                var templateScene = EditorSceneManager.OpenScene("Assets/Scenes/Playground.unity", OpenSceneMode.Additive);
                var template = templateScene.GetRootGameObjects().SelectMany(o => o.GetComponentsInChildren<Map.Playground>(true)).First();
                var source = new GameObject("Local camera rig");
                SceneManager.MoveGameObjectToScene(source, templateScene);
                foreach (var rootObject in new[] { template.Camera.transform.root, template.Camera.Eye.root,
                    template.Camera.Shoulder.root, template.Camera.FirstPerson.transform.root,
                    template.Camera.ThirdPerson.transform.root }.Distinct()) rootObject.SetParent(source.transform, true);
                SceneManager.SetActiveScene(scene);
                var rig = Object.Instantiate(source, scene) as GameObject;
                rig.name = "Local camera rig";
                world.Camera = rig.GetComponentInChildren<CameraController>(true);
                EditorSceneManager.CloseScene(templateScene, true);
            }
            ConfigureLighting(scene, root);
            world.Spawns = Enumerable.Range(1, 4).Select(i => root.GetComponentsInChildren<Transform>(true).First(t => t.name == $"Marker_Spawn_{i:00}")).ToArray();
            Physics.SyncTransforms();
            foreach (var spawn in world.Spawns)
            {
                // Cast from above the whole corridor and choose the highest surface below
                // the marker. A cast starting above a marker can otherwise hit Ceiling_*
                // first and place the player inside the ceiling.
                var hits = Physics.RaycastAll(spawn.position + Vector3.up * 10f, Vector3.down, 30f, ~0, QueryTriggerInteraction.Ignore);
                var hit = hits.Where(h => !IsCeiling(h.collider.transform)).OrderBy(h => h.point.y).FirstOrDefault();
                if (hit.collider == null) hit = hits.OrderBy(h => h.point.y).FirstOrDefault();
                if (hit.collider != null)
                {
                    var position = spawn.position;
                    // Playground spawns the CharacterController by its transform origin;
                    // place that origin above the floor so its capsule bottom sits on it.
                    position.y = hit.point.y + .8f;
                    spawn.position = position;
                }
            }
        }

        // Map3 has no baked lighting data. With skybox ambient the Editor shows an
        // auto-generated probe, but a Player build gets black ambient, so the level
        // rendered black outside the few flicker lights. Use flat ambient like Map2 and
        // one shadowless fill light per corridor and room (buffers too, so the portal
        // seam looks the same on both sides).
        private static void ConfigureLighting(Scene scene, Transform root)
        {
            SceneManager.SetActiveScene(scene);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.58f, 0.6f, 0.56f);
            RenderSettings.ambientIntensity = 1f;
            var existing = scene.GetRootGameObjects().FirstOrDefault(o => o.name == "Map3 Fill Lights");
            if (existing != null) Object.DestroyImmediate(existing);
            var fills = new GameObject("Map3 Fill Lights");
            SceneManager.MoveGameObjectToScene(fills, scene);
            foreach (var section in root.GetComponentsInChildren<Transform>(true)
                         .Where(t => t.name.StartsWith("Corridor_", StringComparison.Ordinal) || t.name.StartsWith("Room_", StringComparison.Ordinal)))
            {
                var renderers = section.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length == 0) continue;
                var bounds = renderers.Select(r => r.bounds).Aggregate((a, b) => { a.Encapsulate(b); return a; });
                var light = new GameObject($"Fill {section.name}").AddComponent<Light>();
                light.transform.SetParent(fills.transform, false);
                light.transform.position = new Vector3(bounds.center.x, bounds.min.y + 2.3f, bounds.center.z);
                light.type = LightType.Point;
                light.range = Mathf.Max(bounds.size.x, bounds.size.z) * .75f;
                light.intensity = 1.2f;
                light.color = new Color(1f, .97f, .88f);
                light.shadows = LightShadows.None;
            }
        }

        private static void ConfigureLobby(Scene scene)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<MapCatalog>(LobbyFlowBuilder.MapCatalogPath); var entries = Enumerable.Range(0, catalog.Count).Select(i => catalog[i]).ToList();
            if (entries.All(e => e.ScenePath != ScenePath)) entries.Add(new MapCatalog.Entry { DisplayName = "后室", ScenePath = ScenePath }); catalog.SetMaps(entries.ToArray()); EditorUtility.SetDirty(catalog);
            EditorBuildSettings.scenes = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).Append(new EditorBuildSettingsScene(ScenePath, true)).ToArray();
            LobbyFlowBuilder.AddMatchHud();
        }

        private static string GetKind(string name) => new[] { "Normal", "Ajar", "Engaged", "PipeChase", "Janitor", "StallInStall", "Empty", "OutOfOrder" }.FirstOrDefault(k => name.Contains(k, StringComparison.Ordinal)) ?? "Normal";
        private static Transform FindNamed(Transform root, string name) => root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);
        private static bool IsCeiling(Transform transform) => transform.GetComponentsInParent<Transform>(true)
            .Any(t => t.name.StartsWith("Ceiling_", StringComparison.Ordinal));
        private static Transform Child(Transform parent, string name, Vector3 local) { var t = parent.Find(name) ?? new GameObject(name).transform; t.SetParent(parent, false); t.localPosition = local; return t; }
        private static void SetPrivate<T>(Component target, string name, T value) { var so = new SerializedObject(target); so.FindProperty(name).boxedValue = value; so.ApplyModifiedPropertiesWithoutUndo(); }
    }
}
