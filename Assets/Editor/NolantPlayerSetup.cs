using System;
using System.IO;
using System.Linq;
using NatureCalls.Map;
using NatureCalls.Player;
using Unity.Netcode;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;

namespace NatureCalls.Editor
{
    public static class NolantPlayerSetup
    {
        private const string ScenePath = "Assets/Scenes/Playground.unity";
        private const string PlayerPath = "Assets/Prefabs/Characters/Player.prefab";
        private const string NolantPath = "Assets/Stylized NPC - Peasant Nolant/Prefabs/Peasant Nolant Brown(Free Version).prefab";
        private const string NolantSourceMaterialPath = "Assets/Stylized NPC - Peasant Nolant/Materials/Peasant Nolant Brown.mat";
        private const string NolantUrpMaterialPath = "Assets/Materials/PeasantNolantBrownURP.mat";
        private const string VisualName = "Peasant Nolant Brown visual";
        private const string SupersededVisualName = "Peasant Nolant Blue visual";
        private static Movement inputProbeMovement;
        private static Vector3 inputProbeStart;
        private static int inputProbeFrames;

        [MenuItem("NatureCalls/Configure Nolant WASD Player")]
        public static void Configure()
        {
            var playerAsset = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath);
            var nolantAsset = AssetDatabase.LoadAssetAtPath<GameObject>(NolantPath);
            var nolantMaterial = EnsureUrpBrownMaterial();
            if (playerAsset == null) throw new InvalidOperationException($"Missing player prefab: {PlayerPath}");
            if (nolantAsset == null) throw new InvalidOperationException($"Missing Nolant prefab: {NolantPath}");

            var contents = PrefabUtility.LoadPrefabContents(PlayerPath);
            try
            {
                var body = contents.transform.Find("Body");
                if (body == null) throw new InvalidOperationException("Player prefab is missing Body.");

                var legacy = body.Find("Legacy placeholder body (inactive)");
                if (legacy == null)
                {
                    var legacyObject = new GameObject("Legacy placeholder body (inactive)");
                    legacy = legacyObject.transform;
                    legacy.SetParent(body, false);
                    var existingChildren = body.Cast<Transform>().Where(child => child != legacy).ToArray();
                    foreach (var child in existingChildren) child.SetParent(legacy, false);
                    legacy.gameObject.SetActive(false);
                }

                var supersededVisual = body.Find(SupersededVisualName);
                if (supersededVisual != null)
                {
                    supersededVisual.SetParent(legacy, false);
                    supersededVisual.gameObject.SetActive(false);
                }

                var previousVisual = body.Find(VisualName);
                var visual = previousVisual != null
                    ? previousVisual.gameObject
                    : (GameObject)PrefabUtility.InstantiatePrefab(nolantAsset, contents.scene);
                if (visual == null) throw new IOException("Could not instantiate Nolant visual.");
                visual.name = VisualName;
                visual.transform.SetParent(body, false);
                visual.SetActive(true);
                visual.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                visual.transform.localScale = Vector3.one;
                SetLayerRecursively(visual, 2);
                foreach (var renderer in visual.GetComponentsInChildren<Renderer>(true))
                    renderer.sharedMaterials = renderer.sharedMaterials.Select(_ => nolantMaterial).ToArray();

                var visualBounds = RendererBounds(visual.transform);
                visual.transform.position += Vector3.up * (body.position.y - visualBounds.min.y);
                visualBounds = RendererBounds(visual.transform);

                var controller = contents.GetComponent<CharacterController>();
                var movement = contents.GetComponent<Movement>();
                var input = contents.GetComponent<NatureCalls.Input.PlayerInput>();
                var posture = contents.GetComponent<Posture>();
                if (controller == null || movement == null || input == null || posture == null)
                    throw new InvalidOperationException("Player prefab is missing its CharacterController, Movement, PlayerInput, or Posture component.");

                var standingHeight = Mathf.Ceil(visualBounds.size.y * 100f) / 100f;
                movement.Body = body;
                movement.StandingHeight = standingHeight;
                movement.SquattingHeight = Mathf.Max(1f, standingHeight * .56f);
                movement.Speed = 3.5f;
                controller.height = standingHeight;
                controller.center = new Vector3(0, standingHeight * .5f, 0);
                controller.radius = Mathf.Clamp(visualBounds.size.x * .46f, .28f, .38f);
                controller.stepOffset = .2f;
                controller.minMoveDistance = 0;
                controller.enabled = false;
                input.CaptureMouse = false;
                input.enabled = false;

                var saved = PrefabUtility.SaveAsPrefabAsset(contents, PlayerPath);
                if (saved == null) throw new IOException($"Could not save {PlayerPath}.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }

            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath) throw new InvalidOperationException($"Open {ScenePath} first.");
            var map = UnityEngine.Object.FindFirstObjectByType<NatureCalls.Map.Playground>();
            var manager = UnityEngine.Object.FindFirstObjectByType<NetworkManager>();
            playerAsset = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath);
            if (map == null || manager == null) throw new InvalidOperationException("Playground scene is missing its map or NetworkManager.");
            Undo.RecordObject(map, "Assign Nolant player prefab");
            Undo.RecordObject(manager, "Assign Nolant network player prefab");
            map.PlayerPrefab = playerAsset;
            map.StartOffline = true;
            manager.NetworkConfig.PlayerPrefab = playerAsset;
            EnsureCameraRig(map.Camera, scene);
            EditorUtility.SetDirty(map);
            EditorUtility.SetDirty(manager);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save Playground.unity.");
            AssetDatabase.SaveAssets();
            Debug.Log("NOLANT_PLAYER_SETUP_OK|prefab=Assets/Prefabs/Characters/Player.prefab|visual=Peasant Nolant Brown|input=WASD|captureMouse=false|offlinePractice=true");
        }

        [MenuItem("NatureCalls/Repair Playground Camera Rig")]
        public static void RepairPlaygroundCameraRig()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath) throw new InvalidOperationException($"Open {ScenePath} first.");
            var map = UnityEngine.Object.FindFirstObjectByType<NatureCalls.Map.Playground>();
            if (map == null || map.Camera == null) throw new InvalidOperationException("Playground scene is missing its map or CameraController.");
            EnsureCameraRig(map.Camera, scene);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save repaired Playground camera.");
            AssetDatabase.SaveAssets();
            Debug.Log($"PLAYGROUND_CAMERA_REPAIR_OK|thirdPerson={map.Camera.ThirdPerson.name}|audioListeners={scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<AudioListener>(true)).Count(listener => listener.enabled)}");
        }

        [MenuItem("NatureCalls/Log Offline Player State")]
        public static void LogOfflinePlayerState()
        {
            if (!EditorApplication.isPlaying) throw new InvalidOperationException("Enter Play Mode first.");
            var map = UnityEngine.Object.FindFirstObjectByType<NatureCalls.Map.Playground>();
            if (map == null || map.OfflinePlayer == null) throw new InvalidOperationException("Offline practice player is not active.");
            var movement = map.OfflinePlayer;
            var visual = movement.Body != null ? movement.Body.Find(VisualName) : null;
            var listeners = UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Debug.Log($"OFFLINE_PLAYER_STATE|position={Format(movement.transform.position)}|controlled={movement.Controlled}|" +
                $"inputEnabled={movement.GetComponent<NatureCalls.Input.PlayerInput>().enabled}|visual={(visual != null && visual.gameObject.activeInHierarchy)}|height={movement.GetComponent<CharacterController>().height:F2}|" +
                $"listeners={string.Join(",", listeners.Select(listener => listener.gameObject.name + ":" + listener.enabled + ":" + listener.gameObject.scene.name))}");
        }

        [MenuItem("NatureCalls/Run WASD Input Probe")]
        public static void RunWasdInputProbe()
        {
            if (!EditorApplication.isPlaying) throw new InvalidOperationException("Enter Play Mode first.");
            var map = UnityEngine.Object.FindFirstObjectByType<NatureCalls.Map.Playground>();
            if (map == null || map.OfflinePlayer == null) throw new InvalidOperationException("Offline practice player is not active.");
            if (Keyboard.current == null) throw new InvalidOperationException("The Input System has no current keyboard.");

            EditorApplication.update -= TickWasdInputProbe;
            inputProbeMovement = map.OfflinePlayer;
            inputProbeStart = inputProbeMovement.transform.position;
            inputProbeFrames = 0;
            InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(Key.W));
            EditorApplication.update += TickWasdInputProbe;
        }

        private static void TickWasdInputProbe()
        {
            inputProbeFrames++;
            if (inputProbeFrames < 20) return;

            EditorApplication.update -= TickWasdInputProbe;
            if (Keyboard.current != null) InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
            var end = inputProbeMovement != null ? inputProbeMovement.transform.position : inputProbeStart;
            Debug.Log($"WASD_INPUT_PROBE_OK|frames={inputProbeFrames}|from={Format(inputProbeStart)}|to={Format(end)}|distance={Vector3.Distance(inputProbeStart, end):F3}");
            inputProbeMovement = null;
        }

        private static Bounds RendererBounds(Transform root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) throw new InvalidOperationException($"{root.name} has no Renderer.");
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            return bounds;
        }

        private static void EnsureCameraRig(NatureCalls.Camera.CameraController rig, Scene scene)
        {
            if (rig.Output == null) rig.Output = rig.GetComponent<UnityEngine.Camera>();
            if (rig.Brain == null) rig.Brain = rig.GetComponent<CinemachineBrain>();
            if (rig.Eye == null) rig.Eye = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true)).FirstOrDefault(item => item.name == "Eye target");
            if (rig.Shoulder == null) rig.Shoulder = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true)).FirstOrDefault(item => item.name == "Shoulder target");
            if (rig.FirstPerson == null) rig.FirstPerson = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<CinemachineCamera>(true)).FirstOrDefault(item => item.name == "First person rig");
            if (rig.Output == null || rig.Brain == null || rig.Eye == null || rig.Shoulder == null || rig.FirstPerson == null)
                throw new InvalidOperationException("Playground camera is missing an output camera, brain, eye, shoulder, or first-person rig.");

            if (rig.ThirdPerson == null)
            {
                var thirdObject = new GameObject("Toileting rig");
                Undo.RegisterCreatedObjectUndo(thirdObject, "Restore third-person camera rig");
                rig.ThirdPerson = thirdObject.AddComponent<CinemachineCamera>();
                rig.ThirdPerson.Follow = rig.Shoulder;
                rig.ThirdPerson.Lens.FieldOfView = 65;
                rig.ThirdPerson.Lens.NearClipPlane = .05f;
                thirdObject.AddComponent<CinemachineThirdPersonFollow>();
            }

            var outputListener = rig.Output.GetComponent<AudioListener>();
            if (outputListener == null) outputListener = Undo.AddComponent<AudioListener>(rig.Output.gameObject);
            foreach (var listener in scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<AudioListener>(true)))
            {
                Undo.RecordObject(listener, "Keep one Playground audio listener");
                listener.enabled = listener == outputListener;
                EditorUtility.SetDirty(listener);
            }
            EditorUtility.SetDirty(rig);
        }

        private static void SetLayerRecursively(GameObject root, int layer)
        {
            root.layer = layer;
            foreach (Transform child in root.transform) SetLayerRecursively(child.gameObject, layer);
        }

        private static Material EnsureUrpBrownMaterial()
        {
            var source = AssetDatabase.LoadAssetAtPath<Material>(NolantSourceMaterialPath);
            if (source == null) throw new InvalidOperationException($"Missing Brown material: {NolantSourceMaterialPath}");
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("Universal Render Pipeline/Lit shader is unavailable.");
            var material = AssetDatabase.LoadAssetAtPath<Material>(NolantUrpMaterialPath);
            if (material == null)
            {
                material = new Material(shader) { name = "Peasant Nolant Brown URP" };
                AssetDatabase.CreateAsset(material, NolantUrpMaterialPath);
            }
            material.shader = shader;
            var texture = source.GetTexture("_MainTex");
            material.SetTexture("_BaseMap", texture);
            material.SetTexture("_MainTex", texture);
            material.SetColor("_BaseColor", Color.white);
            material.SetColor("_Color", Color.white);
            material.SetFloat("_Smoothness", .15f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static string Format(Vector3 value) => $"({value.x:F3},{value.y:F3},{value.z:F3})";
    }
}
