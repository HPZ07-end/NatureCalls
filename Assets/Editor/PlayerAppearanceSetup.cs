using System;
using System.IO;
using System.Linq;
using NatureCalls.Core;
using NatureCalls.Player;
using NatureCalls.UI;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NatureCalls.Editor
{
    public static class PlayerAppearanceSetup
    {
        internal const string PlayerPath = "Assets/Prefabs/Characters/Player.prefab";
        private const string Art = "Assets/Art/";
        private const string Animations = Art + "Animations/Characters/";
        private const string Meshes = Art + "Models/Characters/";
        private const string Materials = Art + "Materials/Characters/";
        private const string Textures = Art + "Textures/Characters/";

        [MenuItem("NatureCalls/Configure Player Appearance")]
        public static void Apply()
        {
            var types = (PlayerBodyType[])Enum.GetValues(typeof(PlayerBodyType));
            foreach (var type in types) ConfigureArt("Characters-" + type);
            EditPrefab(PlayerPath, root =>
            {
                var body = root.GetComponent<Movement>().Body;
                foreach (var child in body.Cast<Transform>().ToArray()) Object.DestroyImmediate(child.gameObject);
                body.localScale = Vector3.one;
                using var appearance = new SerializedObject(root.GetComponent<PlayerAppearance>());
                var rigs = appearance.FindProperty("rigs");
                rigs.arraySize = types.Length;
                foreach (var type in types)
                {
                    var asset = AssetDatabase.LoadAssetAtPath<GameObject>(VisualPath("Characters-" + type));
                    var visual = (GameObject)PrefabUtility.InstantiatePrefab(asset, body.gameObject.scene);
                    visual.transform.SetParent(body, false);
                    var animator = visual.GetComponentInChildren<Animator>(true);
                    animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                    visual.SetActive(type == PlayerBodyType.Balanced);
                    rigs.GetArrayElementAtIndex((int)type).objectReferenceValue = animator;
                }
                appearance.ApplyModifiedPropertiesWithoutUndo();
            });
            EditPrefab(LobbyFlowBuilder.CanvasPath, root => LobbyFlowBuilder.ConfigureOutfitBodyType(root.GetComponent<LobbyView>()));
            AssetDatabase.SaveAssets();
            Debug.Log("PLAYER_APPEARANCE_SETUP_OK");
        }

        private static string VisualPath(string name) => Art + "Prefabs/Characters/" + name + ".prefab";

        private static void ConfigureArt(string name)
        {
            var modelPath = Meshes + name + ".fbx";
            var importer = (ModelImporter)AssetImporter.GetAtPath(modelPath);
            var wasReadable = importer.isReadable;
            if (!wasReadable) { importer.isReadable = true; importer.SaveAndReimport(); }
            try
            {
                var coat = CoatMaterial(name);
                EditPrefab(VisualPath(name), root =>
                {
                    var animator = root.GetComponentInChildren<Animator>(true);
                    var renderer = root.GetComponentInChildren<SkinnedMeshRenderer>(true);
                    var source = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath).GetComponentInChildren<SkinnedMeshRenderer>().sharedMesh;
                    var mesh = Object.Instantiate(source);
                    var uv = mesh.uv;
                    var triangles = mesh.triangles;
                    // Coat is the bottom-left tile of the 4 × 4 painted atlas.
                    bool Coat(int triangle) => uv[triangles[triangle]].x < .25f && uv[triangles[triangle]].y < .25f;
                    var starts = Enumerable.Range(0, triangles.Length / 3).Select(i => i * 3).ToArray();
                    int[] Indices(bool cloth) => starts.Where(i => Coat(i) == cloth)
                        .SelectMany(i => new[] { triangles[i], triangles[i + 1], triangles[i + 2] }).ToArray();
                    mesh.subMeshCount = 2;
                    mesh.SetTriangles(Indices(false), 0, false);
                    mesh.SetTriangles(Indices(true), 1, false);
                    renderer.sharedMesh = SaveData(mesh, Meshes + name + "-CoatSlots.asset");
                    renderer.sharedMaterials = new[] { AssetDatabase.LoadAssetAtPath<Material>(Materials + name + "-Paint.mat"), coat };
                    PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                    ConfigurePostures(name, animator, renderer.bones);
                });
            }
            finally
            {
                if (!wasReadable) { importer.isReadable = false; importer.SaveAndReimport(); }
            }
        }

        private static Material CoatMaterial(string name)
        {
            var source = new Texture2D(2, 2);
            if (!ImageConversion.LoadImage(source, File.ReadAllBytes(Textures + name + ".png")))
                throw new InvalidOperationException("Invalid paint atlas for " + name);
            int size = source.width / 4;
            var paint = source.GetPixels(0, 0, size, size);
            float average = paint.Average(p => p.grayscale);
            var brush = new Texture2D(size, size, TextureFormat.RGB24, false);
            brush.SetPixels(paint.Select(p =>
            {
                var value = Mathf.Clamp01(.82f + (p.grayscale - average) * 1.2f);
                return new Color(value, value, value);
            }).ToArray());
            brush.Apply();
            var path = Textures + name + "-Coat.png";
            File.WriteAllBytes(path, brush.EncodeToPNG());
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            Object.DestroyImmediate(source);
            Object.DestroyImmediate(brush);
            var materialPath = Materials + name + "-Coat.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                material = new Material(AssetDatabase.LoadAssetAtPath<Material>(Materials + name + "-Paint.mat"));
                AssetDatabase.CreateAsset(material, materialPath);
            }
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(path));
            material.SetTextureScale("_BaseMap", Vector2.one * 4);
            var original = paint[0];
            original.r = paint.Average(p => p.r);
            original.g = paint.Average(p => p.g);
            original.b = paint.Average(p => p.b);
            material.SetColor("_BaseColor", original);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void ConfigurePostures(string name, Animator animator, Transform[] bones)
        {
            var controller = (AnimatorController)animator.runtimeAnimatorController;
            foreach (string parameter in new[] { "Grounded", "Squatting", "Crawling" })
                if (controller.parameters.All(p => p.name != parameter))
                {
                    controller.AddParameter(new AnimatorControllerParameter
                    {
                        name = parameter, type = AnimatorControllerParameterType.Bool, defaultBool = parameter == "Grounded"
                    });
                }

            var machine = controller.layers[0].stateMachine;
            var idle = machine.states.Single(s => s.state.name == "Idle").state;
            var reach = machine.anyStateTransitions.FirstOrDefault(t => t.destinationState.name == "Reach");
            if (reach != null)
            {
                foreach (string parameter in new[] { "Grounded", "Squatting", "Crawling" })
                    if (reach.conditions.All(c => c.parameter != parameter))
                        reach.AddCondition(parameter == "Grounded" ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0, parameter);
            }
            AddPosture("Jump", "Grounded", false, 22, 42, 24);
            AddPosture("SquatHold", "Squatting", true, 72, 112, 30);
            AddPosture("Crawl", "Crawling", true, 40, 65, 48);

            void AddPosture(string stateName, string parameter, bool whenTrue, float thigh, float knee, float shoulder)
            {
                var state = machine.states.Select(s => s.state).FirstOrDefault(s => s.name == stateName);
                if (state == null)
                {
                    state = machine.AddState(stateName);
                    var enter = machine.AddAnyStateTransition(state);
                    enter.hasExitTime = false;
                    enter.canTransitionToSelf = false;
                    enter.hasFixedDuration = true;
                    enter.duration = .09f;
                    enter.AddCondition(whenTrue ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0, parameter);
                    if (stateName == "Jump")
                    {
                        enter.AddCondition(AnimatorConditionMode.IfNot, 0, "Squatting");
                        enter.AddCondition(AnimatorConditionMode.IfNot, 0, "Crawling");
                    }
                    var exit = state.AddTransition(idle);
                    exit.hasExitTime = false;
                    exit.hasFixedDuration = true;
                    exit.duration = .1f;
                    exit.AddCondition(whenTrue ? AnimatorConditionMode.IfNot : AnimatorConditionMode.If, 0, parameter);
                }
                state.motion = Pose(name, stateName, animator.transform, bones, thigh, knee, shoulder);
            }
        }

        private static AnimationClip Pose(string name, string state, Transform root, Transform[] bones, float thigh, float knee, float shoulder)
        {
            var startPositions = bones.Select(b => b.localPosition).ToArray();
            var startRotations = bones.Select(b => b.localRotation).ToArray();
            Transform Bone(string key) => bones.Single(b => b.name == key);
            void Bend(string key, float angle) => Bone(key).rotation = Quaternion.AngleAxis(angle, root.right) * Bone(key).rotation;

            foreach (var side in new[] { ".L", ".R" })
            {
                Bend("Thigh" + side, -thigh);
                Bend("Calf" + side, knee);
                Bend("Foot" + side, thigh - knee);
                Bend("UpperArm" + side, -shoulder);
                Bend("Forearm" + side, -35);
            }
            Bend("Chest", state == "Crawl" ? 30 : 10);
            var pelvis = Bone("Pelvis");
            var foot = Mathf.Min(Bone("Foot.L").position.y, Bone("Foot.R").position.y);
            if (state != "Jump")
                pelvis.position -= Vector3.up * (foot - root.position.y - .06f);

            var clip = new AnimationClip { frameRate = 30 };
            for (int i = 0; i < bones.Length; i++)
            {
                var path = AnimationUtility.CalculateTransformPath(bones[i], root);
                var position = bones[i].localPosition;
                var rotation = bones[i].localRotation;
                Curve("localPosition.x", position.x);
                Curve("localPosition.y", position.y);
                Curve("localPosition.z", position.z);
                Curve("localRotation.x", rotation.x);
                Curve("localRotation.y", rotation.y);
                Curve("localRotation.z", rotation.z);
                Curve("localRotation.w", rotation.w);
                void Curve(string field, float value) =>
                    clip.SetCurve(path, typeof(Transform), field, AnimationCurve.Constant(0, .25f, value));
            }
            for (int i = 0; i < bones.Length; i++)
            {
                bones[i].localPosition = startPositions[i];
                bones[i].localRotation = startRotations[i];
            }
            return SaveData(clip, Animations + name + "-" + state + ".anim");
        }

        private static T SaveData<T>(T value, string path) where T : Object
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null)
            {
                EditorUtility.CopySerialized(value, existing);
                Object.DestroyImmediate(value);
                return existing;
            }
            value.name = Path.GetFileNameWithoutExtension(path);
            AssetDatabase.CreateAsset(value, path);
            return value;
        }

        private static void EditPrefab(string path, Action<GameObject> change)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                change(root);
                PrefabUtility.SaveAsPrefabAsset(root, path, out bool saved);
                if (!saved) throw new InvalidOperationException("Could not save " + path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
