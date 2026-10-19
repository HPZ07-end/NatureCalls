using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

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
            ceramic = Material("Ceramic", new Color(.92f, .94f, .89f));
            water = Material("Bowl", new Color(.12f, .25f, .27f));
            brown = Material("Placeholder", new Color(.28f, .10f, .035f));
            stain = Material("Stain", new Color(.25f, .32f, .10f));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.65f, .7f, .75f);
            var light = new GameObject("Daylight").AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.6f;
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
            Box("Entrance left", new Vector3(-1.45f, 1.15f, 1.5f), new Vector3(.8f, 2.3f, .15f), wall);
            Box("Entrance right", new Vector3(1.45f, 1.15f, 1.5f), new Vector3(.8f, 2.3f, .15f), wall);
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
            var camera = new GameObject("Preview camera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.transform.position = new Vector3(5, 6, -5);
            camera.transform.LookAt(new Vector3(0, 0, 3));
            camera.gameObject.AddComponent<AudioListener>();
            PrefabUtility.SaveAsPrefabAsset(toilet, "Assets/Prefabs/Toilet.prefab");
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
