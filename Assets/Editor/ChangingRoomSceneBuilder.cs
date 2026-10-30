using System.IO;
using System.Linq;
using NatureCalls.Environment;
using NatureCalls.Player;
using NatureCalls.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NatureCalls.Editor
{
    public static class ChangingRoomSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/ChangingRoom.unity";
        private const string MaterialFolder = "Assets/Environment/ChangingRoom/Materials";

        [MenuItem("NatureCalls/Changing Room/Build Scene")]
        public static void BuildScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            EnsureFolders();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var architecture = new GameObject("Architecture");
            var gameplay = new GameObject("Gameplay");
            var lighting = new GameObject("Lighting");

            var dark = Material("Dark", new Color(0.055f, 0.07f, 0.11f));
            var wall = Material("Wall", new Color(0.16f, 0.19f, 0.26f));
            var accent = Material("Accent", new Color(0.13f, 0.82f, 0.85f));
            var magenta = Material("Magenta", new Color(0.92f, 0.2f, 0.58f));
            var platformMaterial = Material("Platform", new Color(0.16f, 0.36f, 0.48f));
            var playerMaterial = Material("Player", new Color(0.18f, 0.48f, 0.95f));

            Cube("Floor", architecture.transform, new Vector3(0f, -0.25f, 0f), new Vector3(18f, 0.5f, 14f), dark);
            Cube("BackWall", architecture.transform, new Vector3(0f, 3f, 5.75f), new Vector3(18f, 6f, 0.5f), wall);
            Cube("LeftWall", architecture.transform, new Vector3(-8.75f, 3f, 0f), new Vector3(0.5f, 6f, 12f), wall);
            Cube("RightWall", architecture.transform, new Vector3(8.75f, 3f, 0f), new Vector3(0.5f, 6f, 12f), wall);
            Cube("ChangingRoomBackdrop", architecture.transform, new Vector3(0f, 2.4f, 3.9f), new Vector3(7f, 4.8f, 0.35f), dark);
            Cube("LeftPillar", architecture.transform, new Vector3(-3.8f, 2.4f, 3.4f), new Vector3(0.5f, 4.8f, 0.5f), accent);
            Cube("RightPillar", architecture.transform, new Vector3(3.8f, 2.4f, 3.4f), new Vector3(0.5f, 4.8f, 0.5f), accent);
            Cube("Header", architecture.transform, new Vector3(0f, 4.55f, 3.4f), new Vector3(8.1f, 0.5f, 0.5f), magenta);

            var platform = Cube("ChangingPlatform", gameplay.transform, new Vector3(0f, 0.5f, 1.2f), new Vector3(4.4f, 1f, 4.4f), platformMaterial);
            var trim = Cube("PlatformTrim", platform.transform, new Vector3(0f, 0.53f, 0f), new Vector3(1.04f, 0.08f, 1.04f), accent);
            Object.DestroyImmediate(trim.GetComponent<Collider>());

            var trigger = new GameObject("ChangingPlatformTrigger");
            trigger.transform.SetParent(gameplay.transform);
            trigger.transform.position = new Vector3(0f, 1.7f, 1.2f);
            var triggerCollider = trigger.AddComponent<BoxCollider>();
            triggerCollider.isTrigger = true;
            triggerCollider.size = new Vector3(4.2f, 1.4f, 4.2f);

            CreateDisplay("Display_Default", new Vector3(-6f, 0f, 3.7f), new Color(0.18f, 0.48f, 0.95f), architecture.transform);
            CreateDisplay("Display_Cleanup", new Vector3(-4.6f, 0f, 3.7f), new Color(0.16f, 0.72f, 0.42f), architecture.transform);
            CreateDisplay("Display_Repair", new Vector3(4.6f, 0f, 3.7f), new Color(1f, 0.45f, 0.1f), architecture.transform);
            CreateDisplay("Display_Party", new Vector3(6f, 0f, 3.7f), new Color(0.95f, 0.24f, 0.58f), architecture.transform);

            var player = new GameObject("Player");
            player.transform.SetParent(gameplay.transform);
            player.transform.position = new Vector3(0f, 0f, -4.6f);
            var characterController = player.AddComponent<CharacterController>();
            characterController.center = new Vector3(0f, 1f, 0f);
            characterController.height = 2f;
            characterController.radius = 0.45f;
            player.AddComponent<SimplePlayerController>();

            var visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            visual.name = "AvatarVisual";
            visual.transform.SetParent(player.transform);
            visual.transform.localPosition = new Vector3(0f, 1f, 0f);
            visual.transform.localScale = new Vector3(0.8f, 1f, 0.8f);
            Object.DestroyImmediate(visual.GetComponent<Collider>());
            visual.GetComponent<Renderer>().sharedMaterial = playerMaterial;
            var outfit = player.AddComponent<PlayerOutfitController>();
            SetObjectReference(outfit, "bodyRenderer", visual.GetComponent<Renderer>());

            var hudObject = new GameObject("ChangingRoomHUD");
            hudObject.transform.SetParent(gameplay.transform);
            var hud = hudObject.AddComponent<ChangingRoomHud>();
            var interaction = trigger.AddComponent<ChangingRoomPlatform>();
            SetObjectReference(interaction, "hud", hud);

            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 6.5f, -13.5f);
            var camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 55f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.025f, 0.035f, 0.06f);
            cameraObject.AddComponent<AudioListener>();
            cameraObject.AddComponent<ThirdPersonCamera>().SetTarget(player.transform);

            var directional = new GameObject("Directional Light");
            directional.transform.SetParent(lighting.transform);
            directional.transform.rotation = Quaternion.Euler(48f, -32f, 0f);
            var directionalLight = directional.AddComponent<Light>();
            directionalLight.type = LightType.Directional;
            directionalLight.intensity = 1.15f;

            var spot = new GameObject("Platform Spotlight");
            spot.transform.SetParent(lighting.transform);
            spot.transform.position = new Vector3(0f, 5.5f, 1.2f);
            spot.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            var spotLight = spot.AddComponent<Light>();
            spotLight.type = LightType.Spot;
            spotLight.color = new Color(0.3f, 0.9f, 1f);
            spotLight.intensity = 18f;
            spotLight.range = 10f;
            spotLight.spotAngle = 62f;

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.16f, 0.18f, 0.26f);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddToBuildSettings();
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = platform;
            SceneView.lastActiveSceneView?.FrameSelected();
            Debug.Log("NatureCalls changing room scene created: " + ScenePath);
        }

        private static GameObject Cube(string name, Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            var gameObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            gameObject.name = name;
            gameObject.transform.SetParent(parent);
            gameObject.transform.position = position;
            gameObject.transform.localScale = scale;
            gameObject.GetComponent<Renderer>().sharedMaterial = material;
            return gameObject;
        }

        private static void CreateDisplay(string name, Vector3 position, Color color, Transform parent)
        {
            var stand = Cube(name + "_Stand", parent, position + new Vector3(0f, 0.25f, 0f), new Vector3(1f, 0.5f, 1f), Material(name + "_Stand", new Color(0.08f, 0.1f, 0.15f)));
            var mannequin = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            mannequin.name = name;
            mannequin.transform.SetParent(stand.transform);
            mannequin.transform.localPosition = new Vector3(0f, 2.5f, 0f);
            mannequin.transform.localScale = new Vector3(0.65f, 1.55f, 0.65f);
            mannequin.GetComponent<Renderer>().sharedMaterial = Material(name, color);
            Object.DestroyImmediate(mannequin.GetComponent<Collider>());
        }

        private static void SetObjectReference(Object target, string propertyName, Object value)
        {
            var serializedObject = new SerializedObject(target);
            serializedObject.FindProperty(propertyName).objectReferenceValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Material Material(string name, Color color)
        {
            var path = MaterialFolder + "/" + name + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                existing.color = color;
                EditorUtility.SetDirty(existing);
                return existing;
            }

            var shader = Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit");
            var material = new Material(shader) { name = name, color = color };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void EnsureFolders()
        {
            var folders = new[]
            {
                "Assets/Environment/ChangingRoom",
                MaterialFolder
            };
            foreach (var folder in folders)
            {
                if (AssetDatabase.IsValidFolder(folder))
                    continue;
                var parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
                AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
            }
        }

        private static void AddToBuildSettings()
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            var existing = scenes.FirstOrDefault(item => item.path == ScenePath);
            if (existing == null)
                scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            else
                existing.enabled = true;
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
