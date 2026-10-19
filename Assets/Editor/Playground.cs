using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Unity.Cinemachine;
using NatureCalls.Player;
using NatureCalls.Camera;
using Unity.Netcode;
using Unity.Netcode.Components;
using Unity.Netcode.Transports.UTP;

namespace NatureCalls.Editor
{
    public static class Playground
    {
        public const string ScenePath = "Assets/Scenes/Playground.unity";
        private static Material floor, wall, ceramic, water, brown, stain;

        [MenuItem("NatureCalls/Generate Playground")]
        public static void Create()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            foreach (var folder in new[] { "Materials", "Settings", "Prefabs" })
                if (!AssetDatabase.IsValidFolder("Assets/" + folder)) AssetDatabase.CreateFolder("Assets", folder);
            ConfigureRendering();
            var settings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
            settings.FindProperty("activeInputHandler").intValue = 2;
            settings.ApplyModifiedPropertiesWithoutUndo();
            PlayerSettings.runInBackground = true;
            PlayerSettings.defaultScreenWidth = 1100;
            PlayerSettings.defaultScreenHeight = 760;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            floor = Material("Floor", new Color(.25f, .34f, .36f));
            wall = Material("Wall", new Color(.68f, .77f, .72f));
            ceramic = Material("Ceramic", new Color(.76f, .8f, .75f));
            water = Material("Bowl", new Color(.12f, .25f, .27f));
            brown = Material("Placeholder", new Color(.28f, .10f, .035f));
            stain = Material("Stain", new Color(.25f, .32f, .10f));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.42f, .46f, .5f);
            var light = new GameObject("Daylight").AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1f;
            light.transform.rotation = Quaternion.Euler(48, -30, 0);
            light.shadows = LightShadows.Soft;
            Box("Floor", new Vector3(0, -.15f, 0), new Vector3(16, .3f, 14), floor);
            Box("Back wall", new Vector3(0, 1.5f, 7), new Vector3(16, 3, .25f), wall);
            Box("Front wall", new Vector3(0, 1.5f, -7), new Vector3(16, 3, .25f), wall);
            Box("Left wall", new Vector3(-8, 1.5f, 0), new Vector3(.25f, 3, 14), wall);
            Box("Right wall", new Vector3(8, 1.5f, 0), new Vector3(.25f, 3, 14), wall);
            Box("Stall left", new Vector3(-1.9f, 1.15f, 3.4f), new Vector3(.15f, 2.3f, 3.8f), wall);
            Box("Stall right", new Vector3(1.9f, 1.15f, 3.4f), new Vector3(.15f, 2.3f, 3.8f), wall);
            Box("Stall back", new Vector3(0, 1.15f, 5.3f), new Vector3(3.95f, 2.3f, .15f), wall);
            Box("Entrance left", new Vector3(-1.65f, 1.15f, 1.5f), new Vector3(.5f, 2.3f, .15f), wall);
            Box("Entrance right", new Vector3(1.65f, 1.15f, 1.5f), new Vector3(.5f, 2.3f, .15f), wall);
            var toilet = new GameObject("Toilet");
            toilet.transform.position = new Vector3(0, 0, 3.5f);
            Part(toilet, "Left ceramic", new Vector3(-.48f, .35f, 0), new Vector3(.18f, .7f, 1.15f), ceramic);
            Part(toilet, "Right ceramic", new Vector3(.48f, .35f, 0), new Vector3(.18f, .7f, 1.15f), ceramic);
            Part(toilet, "Front rim", new Vector3(0, .48f, -.52f), new Vector3(.85f, .16f, .16f), ceramic);
            Part(toilet, "Back rim", new Vector3(0, .48f, .52f), new Vector3(.85f, .16f, .16f), ceramic);
            Part(toilet, "Bowl interior", new Vector3(0, .22f, 0), new Vector3(.8f, .1f, 1), water);
            Part(toilet, "Tank", new Vector3(0, .65f, .86f), new Vector3(1.15f, 1.3f, .35f), ceramic);
            var placeholder = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            placeholder.name = "Visible placeholder";
            placeholder.transform.SetParent(toilet.transform, false);
            placeholder.transform.localPosition = new Vector3(0, .39f, .12f);
            placeholder.transform.localScale = new Vector3(.32f, .24f, .38f);
            placeholder.GetComponent<Renderer>().sharedMaterial = brown;
            for (int i = 0; i < 7; i++)
            {
                var mark = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                mark.name = "Wall stain " + i;
                mark.transform.position = new Vector3(-.6f + i * .18f, 1.2f + Mathf.Sin(i * 2) * .25f, 5.21f);
                mark.transform.localScale = new Vector3(.22f + i * .025f, .3f, .025f);
                mark.GetComponent<Renderer>().sharedMaterial = stain;
                Object.DestroyImmediate(mark.GetComponent<Collider>());
            }
            Populate(toilet);
            EditorSceneManager.SaveScene(scene, ScenePath);
            var others = EditorBuildSettings.scenes.Where(item => item.path != ScenePath).ToList();
            others.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = others.ToArray();
            AssetDatabase.SaveAssets();
        }

        private static void Populate(GameObject toilet)
        {
            var facility = toilet.AddComponent<NatureCalls.Facility.Toilet>();
            facility.Anchor = new GameObject("Posture anchor").transform;
            facility.Anchor.SetParent(toilet.transform, false);
            facility.Anchor.localPosition = new Vector3(0, 0, -.85f);
            facility.Interior = toilet.transform.Find("Visible placeholder");
            PrefabUtility.SaveAsPrefabAssetAndConnect(toilet, "Assets/Prefabs/Toilet.prefab", InteractionMode.AutomatedAction);

            var player = new GameObject("Player");
            player.layer = 2; // Ignore Raycast: camera collision uses world layer only.
            var controller = player.AddComponent<CharacterController>();
            controller.height = 1.8f; controller.radius = .27f;
            controller.center = new Vector3(0, .9f, 0); controller.stepOffset = .2f;
            controller.minMoveDistance = 0; // Preserve small steps at high frame rates.
            controller.enabled = false; // Movement enables only the local player's controller after spawn.
            player.AddComponent<NetworkObject>();
            var sync = player.AddComponent<NetworkTransform>();
            sync.AuthorityMode = NetworkTransform.AuthorityModes.Owner;
            sync.SyncRotAngleX = false; sync.SyncRotAngleZ = false;
            sync.SyncScaleX = false; sync.SyncScaleY = false; sync.SyncScaleZ = false;
            sync.Interpolate = true;
            player.AddComponent<Posture>();
            var movement = player.AddComponent<Movement>();
            movement.Body = new GameObject("Body").transform;
            movement.Body.SetParent(player.transform, false);
            var color = Material("Player", new Color(.15f, .55f, .9f));
            BodyPart(movement.Body, "Torso", PrimitiveType.Capsule, new Vector3(0, .85f, 0), new Vector3(.54f, .55f, .38f), color);
            BodyPart(movement.Body, "Head", PrimitiveType.Sphere, new Vector3(0, 1.55f, 0), Vector3.one * .4f, color);
            BodyPart(movement.Body, "Left leg", PrimitiveType.Cube, new Vector3(-.15f, .22f, 0), new Vector3(.18f, .44f, .24f), color);
            BodyPart(movement.Body, "Right leg", PrimitiveType.Cube, new Vector3(.15f, .22f, 0), new Vector3(.18f, .44f, .24f), color);
            player.AddComponent<NatureCalls.Input.PlayerInput>().enabled = false;
            player.AddComponent<NatureCalls.Network.Avatar>();
            var prefab = PrefabUtility.SaveAsPrefabAsset(player, "Assets/Prefabs/Player.prefab");
            Object.DestroyImmediate(player);

            var rig = new GameObject("Local camera").AddComponent<CameraController>();
            rig.Output = rig.gameObject.AddComponent<UnityEngine.Camera>();
            rig.Output.tag = "MainCamera";
            rig.Output.nearClipPlane = .05f;
            rig.Output.farClipPlane = 100;
            rig.Output.fieldOfView = 65;
            rig.gameObject.AddComponent<AudioListener>();
            rig.Brain = rig.gameObject.AddComponent<CinemachineBrain>();
            rig.Brain.UpdateMethod = CinemachineBrain.UpdateMethods.LateUpdate;
            rig.Brain.BlendUpdateMethod = CinemachineBrain.BrainUpdateMethods.LateUpdate;
            rig.Eye = new GameObject("Eye target").transform;
            rig.Shoulder = new GameObject("Shoulder target").transform;
            rig.FirstPerson = new GameObject("First person rig").AddComponent<CinemachineCamera>();
            rig.FirstPerson.Follow = rig.Eye;
            rig.FirstPerson.Lens.FieldOfView = 65; rig.FirstPerson.Lens.NearClipPlane = .05f;
            rig.FirstPerson.gameObject.AddComponent<CinemachineHardLockToTarget>();
            rig.FirstPerson.gameObject.AddComponent<CinemachineRotateWithFollowTarget>();
            rig.ThirdPerson = new GameObject("Toileting rig").AddComponent<CinemachineCamera>();
            rig.ThirdPerson.Follow = rig.Shoulder;
            rig.ThirdPerson.Lens.FieldOfView = 65; rig.ThirdPerson.Lens.NearClipPlane = .05f;
            rig.ThirdPerson.gameObject.AddComponent<CinemachineThirdPersonFollow>();
            var world = new GameObject("Playground").AddComponent<NatureCalls.Map.Playground>();
            world.PlayerPrefab = prefab; world.Camera = rig;
            world.Spawns = new Transform[4];
            var positions = new[] { new Vector3(0, 0, -1), new Vector3(-3, 0, -2), new Vector3(3, 0, -2), new Vector3(0, 0, -4) };
            for (int i = 0; i < 4; i++)
            {
                world.Spawns[i] = new GameObject("Spawn " + (i + 1)).transform;
                world.Spawns[i].SetParent(world.transform, false);
                world.Spawns[i].position = positions[i];
            }
            var network = new GameObject("Network");
            var manager = network.AddComponent<NetworkManager>();
            var transport = network.AddComponent<UnityTransport>();
            manager.NetworkConfig = new NetworkConfig
            {
                PlayerPrefab = prefab, NetworkTransport = transport,
                ConnectionApproval = true, EnableSceneManagement = false,
                TickRate = 30, ProtocolVersion = 1
            };
            manager.NetworkConfig.Prefabs.NetworkPrefabsLists.Clear();
            var session = network.AddComponent<NatureCalls.Network.Session>();
            session.Manager = manager; session.Transport = transport; session.World = world;
            var panel = new GameObject("Controls").AddComponent<NatureCalls.UI.Panel>();
            panel.World = world; panel.Session = session;
        }

        private static void BodyPart(Transform parent, string name, PrimitiveType type, Vector3 position, Vector3 scale, Material material)
        {
            var part = GameObject.CreatePrimitive(type);
            part.name = name; part.layer = 2;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position; part.transform.localScale = scale;
            part.GetComponent<Renderer>().sharedMaterial = material;
            Object.DestroyImmediate(part.GetComponent<Collider>());
        }

        private static void ConfigureRendering()
        {
            const string path = "Assets/Settings/PlaygroundPipeline.asset";
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
            if (pipeline == null)
            {
                var renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(renderer, "Assets/Settings/PlaygroundRenderer.asset");
                pipeline = UniversalRenderPipelineAsset.Create(renderer);
                AssetDatabase.CreateAsset(pipeline, path);
            }
            pipeline.mainLightShadowmapResolution = 2048;
            pipeline.shadowDistance = 30;
            GraphicsSettings.defaultRenderPipeline = pipeline;
            QualitySettings.renderPipeline = pipeline;
        }

        public static Material Material(string name, Color color)
        {
            var path = "Assets/Materials/" + name + ".mat";
            var result = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (result == null)
            {
                result = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(result, path);
            }
            result.SetColor("_BaseColor", color);
            result.SetFloat("_Smoothness", .2f);
            return result;
        }

        public static GameObject Box(string name, Vector3 position, Vector3 size, Material material)
        {
            var result = GameObject.CreatePrimitive(PrimitiveType.Cube);
            result.name = name;
            result.transform.position = position;
            result.transform.localScale = size;
            result.GetComponent<Renderer>().sharedMaterial = material;
            return result;
        }

        private static void Part(GameObject parent, string name, Vector3 position, Vector3 size, Material material)
        {
            var part = Box(name, parent.transform.position + position, size, material);
            part.transform.SetParent(parent.transform, true);
        }
    }
}
