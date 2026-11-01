using System;
using System.IO;
using System.Linq;
using NatureCalls.Camera;
using NatureCalls.Map;
using NatureCalls.UI;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NatureCalls.Editor
{
    public static class DressingRoomPracticeBuilder
    {
        private const string ScenePath = "Assets/Scenes/DressingRoom.unity";
        private const string RootName = "DressingRoomPractice";
        private const string PlayerPrefabPath = "Assets/Prefabs/Player.prefab";

        [MenuItem("NatureCalls/Configure Dressing Room Practice")]
        public static void Build()
        {
            if (!File.Exists(ScenePath))
                throw new FileNotFoundException("Create the DressingRoom scene before configuring practice.", ScenePath);

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (GameObject.Find(RootName) != null)
                throw new InvalidOperationException("DressingRoomPractice already exists. Refusing to overwrite existing practice setup.");

            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (playerPrefab == null)
                throw new FileNotFoundException("The shared Player prefab is missing.", PlayerPrefabPath);
            var mirrorRoot = GameObject.Find("Mirror");
            var controller = UnityEngine.Object.FindFirstObjectByType<LobbyUIController>();
            if (mirrorRoot == null || controller == null)
                throw new InvalidOperationException("Build the DressingRoom blockout and UI before configuring local practice.");

            var root = new GameObject(RootName);
            var camera = ConfigureCamera(root.transform);
            var world = root.AddComponent<NatureCalls.Map.Playground>();
            var spawn = CreateSpawn(root.transform);
            world.PlayerPrefab = playerPrefab;
            world.Spawns = new[] { spawn };
            world.Camera = camera;
            world.StartOffline = true;
            ConfigureMirrorZone(mirrorRoot.transform, controller);
            RegisterBuildScene();

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new IOException("Could not save " + ScenePath);
            Selection.activeGameObject = root;
            Debug.Log("NatureCalls DressingRoom local practice configured: " + ScenePath);
        }

        [MenuItem("NatureCalls/Register Dressing Room Scene")]
        public static void RegisterBuildScene()
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.Any(scene => scene.path == ScenePath)) return;
            scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static CameraController ConfigureCamera(Transform parent)
        {
            var cameraObject = GameObject.Find("Main Camera");
            if (cameraObject == null)
                throw new InvalidOperationException("DressingRoom is missing its Main Camera.");
            cameraObject.transform.SetParent(parent, true);
            var output = cameraObject.GetComponent<UnityEngine.Camera>();
            var rig = cameraObject.GetComponent<CameraController>() ?? cameraObject.AddComponent<CameraController>();
            rig.Output = output;
            rig.Brain = cameraObject.GetComponent<CinemachineBrain>() ?? cameraObject.AddComponent<CinemachineBrain>();
            rig.Brain.UpdateMethod = CinemachineBrain.UpdateMethods.LateUpdate;
            rig.Brain.BlendUpdateMethod = CinemachineBrain.BrainUpdateMethods.LateUpdate;
            rig.Eye = Target(parent, "Eye Target");
            rig.Shoulder = Target(parent, "Shoulder Target");

            rig.FirstPerson = new GameObject("First Person Rig").AddComponent<CinemachineCamera>();
            rig.FirstPerson.transform.SetParent(parent, false);
            rig.FirstPerson.Follow = rig.Eye;
            rig.FirstPerson.Lens.FieldOfView = 65f;
            rig.FirstPerson.Lens.NearClipPlane = .05f;
            rig.FirstPerson.gameObject.AddComponent<CinemachineHardLockToTarget>();
            rig.FirstPerson.gameObject.AddComponent<CinemachineRotateWithFollowTarget>();

            rig.ThirdPerson = new GameObject("Third Person Rig").AddComponent<CinemachineCamera>();
            rig.ThirdPerson.transform.SetParent(parent, false);
            rig.ThirdPerson.Follow = rig.Shoulder;
            rig.ThirdPerson.Lens.FieldOfView = 65f;
            rig.ThirdPerson.Lens.NearClipPlane = .05f;
            rig.ThirdPerson.gameObject.AddComponent<CinemachineThirdPersonFollow>();
            rig.CameraCollisionLayers = 1;
            return rig;
        }

        private static Transform CreateSpawn(Transform parent)
        {
            var spawn = new GameObject("Player Spawn").transform;
            spawn.SetParent(parent, false);
            spawn.SetPositionAndRotation(new Vector3(1.1f, 0f, 0f), Quaternion.identity);
            return spawn;
        }

        private static void ConfigureMirrorZone(Transform mirrorRoot, LobbyUIController controller)
        {
            var zone = new GameObject("Mirror Interaction Zone");
            zone.transform.SetParent(mirrorRoot, false);
            zone.transform.position = new Vector3(1.1f, 1f, 2.35f);
            var collider = zone.AddComponent<SphereCollider>();
            collider.isTrigger = true;
            collider.radius = 1.65f;
            var body = zone.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            var mirror = zone.AddComponent<DressingMirror>();
            mirror.InteractionDistance = 1.65f;
            UnityEventTools.AddPersistentListener(mirror.PlayerEntered, controller.ShowMirrorPrompt);
            UnityEventTools.AddPersistentListener(mirror.PlayerExited, controller.HideMirrorPrompt);
            UnityEventTools.AddPersistentListener(mirror.Interacted, controller.OpenDressingPanel);
        }

        private static Transform Target(Transform parent, string name)
        {
            var target = new GameObject(name).transform;
            target.SetParent(parent, false);
            return target;
        }
    }
}
