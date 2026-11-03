using System;
using System.IO;
using NatureCalls.Facility;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace NatureCalls.Editor
{
    public static class DressingRoomSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/DressingRoom.unity";
        private const string RootName = "DressingRoomBlockout";
        private const string PropsRootName = "DressingRoomProps";
        private const string MaterialsFolder = "Assets/Materials";
        private const string MirrorTexturePath = MaterialsFolder + "/DressingRoomMirror.renderTexture";
        private const string SinkPrefabPath = "Assets/WC/Prefabs/props/SM_sink Variant.prefab";
        private const string SoapPrefabPath = "Assets/WC/Prefabs/props/SM_soap Variant.prefab";
        private const string DryerPrefabPath = "Assets/WC/Prefabs/props/SM_dryer Variant.prefab";
        private const string TrashPrefabPath = "Assets/WC/Prefabs/props/SM_trash_can Variant.prefab";
        private const string SignPrefabPath = "Assets/WC/Prefabs/props/SM_sign_2 Variant.prefab";
        private const string LampPrefabPath = "Assets/WC/Prefabs/props/SM_lamp Variant.prefab";

        [MenuItem("NatureCalls/Build Dressing Room Blockout")]
        public static void Build()
        {
            if (!File.Exists(ScenePath))
                throw new FileNotFoundException("Create and save the DressingRoom scene before building its blockout.", ScenePath);

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (GameObject.Find(RootName) != null)
                throw new InvalidOperationException("DressingRoomBlockout already exists. Refusing to overwrite existing blockout work.");

            if (!AssetDatabase.IsValidFolder(MaterialsFolder))
                AssetDatabase.CreateFolder("Assets", "Materials");

            var root = new GameObject(RootName);
            var floor = Material("DressingRoomFloor", new Color(.24f, .28f, .31f), .25f, .05f);
            var wall = Material("DressingRoomWall", new Color(.68f, .72f, .73f), .12f, .05f);
            var mirror = Material("DressingRoomMirror", new Color(.16f, .34f, .43f), .92f, .8f);
            var frame = Material("DressingRoomFrame", new Color(.07f, .11f, .13f), .4f, .15f);
            var door = Material("DressingRoomDoor", new Color(.30f, .18f, .12f), .18f, .1f);

            ConfigureSceneLighting();
            ConfigureCamera();
            CreateFloor(root.transform, floor);
            CreateRoomShell(root.transform, wall);
            var mirrorRoot = CreateMirror(root.transform, mirror, frame);
            ConfigureMirrorReflection(mirrorRoot, mirror);
            CreateDoor(root.transform, door);
            CreateAnchor(root.transform, "MirrorStandingPoint", new Vector3(1.1f, 0f, 1.65f));
            CreateAnchor(root.transform, "DoorInteractionPoint", new Vector3(-4.25f, 0f, 2.7f));
            ConfigurePublicWashroomProps();

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new IOException("Could not save " + ScenePath);
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = root;
            Debug.Log("NatureCalls DressingRoom blockout created: " + ScenePath);
        }

        [MenuItem("NatureCalls/Configure Dressing Room Mirror Reflection")]
        public static void ConfigureExistingMirrorReflection()
        {
            if (Application.isPlaying)
                throw new InvalidOperationException("Exit Play Mode before configuring the DressingRoom mirror.");
            if (!File.Exists(ScenePath))
                throw new FileNotFoundException("DressingRoom scene is missing.", ScenePath);
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var root = GameObject.Find(RootName);
            var mirrorRoot = root != null ? root.transform.Find("Mirror")?.gameObject : null;
            if (mirrorRoot == null)
                throw new InvalidOperationException("DressingRoomBlockout/Mirror was not found.");

            var surface = mirrorRoot.transform.Find("Mirror Surface")?.GetComponent<Renderer>();
            if (surface == null)
                throw new InvalidOperationException("DressingRoom mirror surface was not found.");

            var mirrorMaterial = surface.sharedMaterial;
            if (mirrorMaterial == null)
                throw new InvalidOperationException("DressingRoom mirror surface has no material.");

            ConfigureMirrorReflection(mirrorRoot, mirrorMaterial);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new IOException("Could not save " + ScenePath);
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = mirrorRoot;
            Debug.Log("NatureCalls DressingRoom mirror reflection configured.");
        }

        [MenuItem("NatureCalls/Configure Dressing Room Public Washroom Props")]
        public static void ConfigureExistingPublicWashroomProps()
        {
            if (Application.isPlaying)
                throw new InvalidOperationException("Exit Play Mode before configuring DressingRoom props.");
            if (!File.Exists(ScenePath))
                throw new FileNotFoundException("DressingRoom scene is missing.", ScenePath);
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (GameObject.Find(RootName) == null)
                throw new InvalidOperationException("DressingRoomBlockout was not found.");

            var props = ConfigurePublicWashroomProps();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new IOException("Could not save " + ScenePath);
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = props;
            Debug.Log("NatureCalls DressingRoom public washroom props configured.");
        }

        private static void ConfigureSceneLighting()
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.38f, .42f, .46f);

            var directional = GameObject.Find("Directional Light");
            if (directional == null) directional = new GameObject("Directional Light");
            var light = directional.GetComponent<Light>() ?? directional.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.15f;
            light.shadows = LightShadows.Soft;
            directional.transform.rotation = Quaternion.Euler(48f, -30f, 0f);

            var roomLight = new GameObject("Room Light").AddComponent<Light>();
            roomLight.type = LightType.Point;
            roomLight.color = new Color(.85f, .94f, 1f);
            roomLight.intensity = 4.5f;
            roomLight.range = 12f;
            roomLight.transform.position = new Vector3(0f, 2.55f, .5f);
        }

        private static void ConfigureCamera()
        {
            var cameraObject = GameObject.Find("Main Camera");
            if (cameraObject == null) cameraObject = new GameObject("Main Camera");
            var camera = cameraObject.GetComponent<UnityEngine.Camera>() ?? cameraObject.AddComponent<UnityEngine.Camera>();
            cameraObject.tag = "MainCamera";
            camera.fieldOfView = 65f;
            camera.nearClipPlane = .05f;
            camera.transform.position = new Vector3(0f, 1.6f, -3.25f);
            camera.transform.rotation = Quaternion.identity;
            if (cameraObject.GetComponent<AudioListener>() == null)
                cameraObject.AddComponent<AudioListener>();
        }

        private static void CreateFloor(Transform parent, Material material)
        {
            var defaultPlane = GameObject.Find("Plane");
            if (defaultPlane != null && defaultPlane.transform.position == Vector3.zero && defaultPlane.transform.localScale == Vector3.one)
            {
                defaultPlane.name = "Floor";
                defaultPlane.transform.SetParent(parent, true);
                defaultPlane.transform.localScale = new Vector3(1.2f, 1f, .8f);
                defaultPlane.GetComponent<Renderer>().sharedMaterial = material;
                return;
            }

            Box(parent, "Floor", new Vector3(0f, -.1f, 0f), new Vector3(12f, .2f, 8f), material);
        }

        private static void CreateRoomShell(Transform parent, Material material)
        {
            Box(parent, "Left Wall", new Vector3(-6f, 1.5f, 0f), new Vector3(.2f, 3f, 8f), material);
            Box(parent, "Right Wall", new Vector3(6f, 1.5f, 0f), new Vector3(.2f, 3f, 8f), material);
            Box(parent, "Front Wall", new Vector3(0f, 1.5f, -4f), new Vector3(12f, 3f, .2f), material);
            Box(parent, "Back Wall Left", new Vector3(-5.5f, 1.5f, 4f), new Vector3(1f, 3f, .2f), material);
            Box(parent, "Door Header", new Vector3(-4.25f, 2.65f, 4f), new Vector3(1.5f, .7f, .2f), material);
            Box(parent, "Mirror Wall", new Vector3(1.25f, 1.5f, 4f), new Vector3(9.5f, 3f, .2f), material);
        }

        private static GameObject CreateMirror(Transform parent, Material mirror, Material frame)
        {
            var mirrorRoot = new GameObject("Mirror");
            mirrorRoot.transform.SetParent(parent, false);
            Box(mirrorRoot.transform, "Mirror Surface", new Vector3(1.1f, 1.5f, 3.87f), new Vector3(4.8f, 2.35f, .05f), mirror);
            Box(mirrorRoot.transform, "Mirror Frame Top", new Vector3(1.1f, 2.72f, 3.84f), new Vector3(5.05f, .1f, .1f), frame);
            Box(mirrorRoot.transform, "Mirror Frame Bottom", new Vector3(1.1f, .28f, 3.84f), new Vector3(5.05f, .1f, .1f), frame);
            Box(mirrorRoot.transform, "Mirror Frame Left", new Vector3(-1.35f, 1.5f, 3.84f), new Vector3(.1f, 2.55f, .1f), frame);
            Box(mirrorRoot.transform, "Mirror Frame Right", new Vector3(3.55f, 1.5f, 3.84f), new Vector3(.1f, 2.55f, .1f), frame);
            return mirrorRoot;
        }

        private static void ConfigureMirrorReflection(GameObject mirrorRoot, Material mirrorMaterial)
        {
            var surface = mirrorRoot.transform.Find("Mirror Surface")?.GetComponent<Renderer>();
            if (surface == null)
                throw new InvalidOperationException("DressingRoom mirror surface was not found.");

            var sourceTexture = AssetDatabase.LoadAssetAtPath<RenderTexture>(MirrorTexturePath);
            if (sourceTexture == null)
            {
                sourceTexture = new RenderTexture(new RenderTextureDescriptor(1024, 512, RenderTextureFormat.ARGB32, 24))
                {
                    name = "DressingRoomMirror"
                };
                AssetDatabase.CreateAsset(sourceTexture, MirrorTexturePath);
            }

            if (mirrorMaterial.HasProperty("_BaseColor")) mirrorMaterial.SetColor("_BaseColor", Color.white);
            if (mirrorMaterial.HasProperty("_BaseMap"))
            {
                mirrorMaterial.SetTexture("_BaseMap", sourceTexture);
                mirrorMaterial.SetTextureScale("_BaseMap", new Vector2(1f, -1f));
                mirrorMaterial.SetTextureOffset("_BaseMap", new Vector2(0f, 1f));
            }
            if (mirrorMaterial.HasProperty("_MainTex"))
            {
                mirrorMaterial.SetTexture("_MainTex", sourceTexture);
                mirrorMaterial.SetTextureScale("_MainTex", new Vector2(1f, -1f));
                mirrorMaterial.SetTextureOffset("_MainTex", new Vector2(0f, 1f));
            }
            EditorUtility.SetDirty(mirrorMaterial);

            var cameraTransform = mirrorRoot.transform.Find("Reflection Camera");
            if (cameraTransform == null)
            {
                var cameraObject = new GameObject("Reflection Camera");
                cameraObject.transform.SetParent(mirrorRoot.transform, false);
                cameraTransform = cameraObject.transform;
            }

            var reflectionCamera = cameraTransform.GetComponent<UnityEngine.Camera>();
            if (reflectionCamera == null)
                reflectionCamera = cameraTransform.gameObject.AddComponent<UnityEngine.Camera>();
            reflectionCamera.enabled = true;
            reflectionCamera.targetTexture = sourceTexture;
            reflectionCamera.clearFlags = CameraClearFlags.SolidColor;
            reflectionCamera.backgroundColor = new Color(.10f, .12f, .15f, 1f);
            reflectionCamera.fieldOfView = 48f;
            reflectionCamera.nearClipPlane = .05f;
            reflectionCamera.farClipPlane = 30f;
            reflectionCamera.cullingMask = ~0;
            cameraTransform.position = new Vector3(1.1f, 1.5f, 3.78f);
            cameraTransform.rotation = Quaternion.LookRotation(new Vector3(0f, -.12f, -1f), Vector3.up);

            var listener = cameraTransform.GetComponent<AudioListener>();
            if (listener != null) UnityEngine.Object.DestroyImmediate(listener);

            var planarMirror = mirrorRoot.GetComponent<PlanarMirror>();
            if (planarMirror == null)
                planarMirror = mirrorRoot.AddComponent<PlanarMirror>();
            planarMirror.ReflectionCamera = reflectionCamera;
            planarMirror.Surface = surface;
            EditorUtility.SetDirty(reflectionCamera);
            EditorUtility.SetDirty(planarMirror);
        }

        private static GameObject ConfigurePublicWashroomProps()
        {
            var props = GameObject.Find(PropsRootName);
            if (props == null)
                props = new GameObject(PropsRootName);

            // Keep the mirror wall and its standing point clear. The two sinks face into the room from the right wall.
            EnsureProp(props.transform, "Sink A", SinkPrefabPath, new Vector3(5.89f, 0f, 1.10f), Quaternion.Euler(0f, -90f, 0f), Vector3.one * 1.25f);
            EnsureProp(props.transform, "Sink B", SinkPrefabPath, new Vector3(5.89f, 0f, -0.55f), Quaternion.Euler(0f, -90f, 0f), Vector3.one * 1.25f);
            EnsureProp(props.transform, "Soap A", SoapPrefabPath, new Vector3(5.42f, .98f, 1.10f), Quaternion.Euler(0f, -90f, 0f), Vector3.one);
            EnsureProp(props.transform, "Soap B", SoapPrefabPath, new Vector3(5.42f, .98f, -0.55f), Quaternion.Euler(0f, -90f, 0f), Vector3.one);
            EnsureProp(props.transform, "Hand Dryer", DryerPrefabPath, new Vector3(5.84f, 1.35f, -2.15f), Quaternion.Euler(0f, -90f, 0f), Vector3.one);
            EnsureProp(props.transform, "Waste Bin", TrashPrefabPath, new Vector3(5.38f, 0f, 2.45f), Quaternion.Euler(0f, -90f, 0f), Vector3.one * 1.25f);
            var signPosition = new Vector3(-2.55f, 1.60f, 3.84f);
            var sign = EnsureProp(props.transform, "Restroom Sign", SignPrefabPath, signPosition, Quaternion.Euler(0f, 180f, 0f), Vector3.one * 1.5f);
            if (Vector3.Distance(sign.transform.position, new Vector3(-3.35f, 1.60f, 3.84f)) < .001f)
                sign.transform.position = signPosition;
            EnsureProp(props.transform, "Lamp A", LampPrefabPath, new Vector3(-1.30f, 2.74f, 3.65f), Quaternion.Euler(0f, 180f, 0f), Vector3.one);
            EnsureProp(props.transform, "Lamp B", LampPrefabPath, new Vector3(3.50f, 2.74f, 3.65f), Quaternion.Euler(0f, 180f, 0f), Vector3.one);
            return props;
        }

        private static GameObject EnsureProp(Transform parent, string name, string prefabPath, Vector3 position, Quaternion rotation, Vector3 scale)
        {
            var existing = parent.Find(name);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
                throw new FileNotFoundException("DressingRoom prop prefab is missing.", prefabPath);

            if (existing != null && PrefabUtility.GetCorrespondingObjectFromSource(existing.gameObject) == prefab)
            {
                if (existing.localScale == Vector3.one && scale != Vector3.one)
                    existing.localScale = scale;
                return existing.gameObject;
            }

            if (existing != null)
            {
                position = existing.position;
                rotation = existing.rotation;
                UnityEngine.Object.DestroyImmediate(existing.gameObject);
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.name = name;
            instance.transform.SetPositionAndRotation(position, rotation);
            instance.transform.localScale = scale;
            return instance;
        }

        private static void CreateDoor(Transform parent, Material material)
        {
            var doorRoot = new GameObject("Door");
            doorRoot.transform.SetParent(parent, false);
            Box(doorRoot.transform, "Door Placeholder", new Vector3(-4.25f, 1.1f, 3.86f), new Vector3(1.4f, 2.2f, .08f), material);
            Box(doorRoot.transform, "Door Handle", new Vector3(-3.78f, 1.1f, 3.79f), new Vector3(.08f, .08f, .08f), Material("DressingRoomHandle", new Color(.82f, .64f, .18f), .75f, .25f));
        }

        private static void CreateAnchor(Transform parent, string name, Vector3 position)
        {
            var anchor = new GameObject(name);
            anchor.transform.SetParent(parent, false);
            anchor.transform.position = position;
        }

        private static GameObject Box(Transform parent, string name, Vector3 position, Vector3 size, Material material)
        {
            var result = GameObject.CreatePrimitive(PrimitiveType.Cube);
            result.name = name;
            result.transform.SetParent(parent, false);
            result.transform.position = position;
            result.transform.localScale = size;
            result.GetComponent<Renderer>().sharedMaterial = material;
            return result;
        }

        private static Material Material(string name, Color color, float smoothness, float metallic)
        {
            var path = MaterialsFolder + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", metallic);
            return material;
        }
    }
}
