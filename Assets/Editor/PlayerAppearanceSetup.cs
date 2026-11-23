using System;
using System.Linq;
using NatureCalls.Player;
using NatureCalls.UI;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NatureCalls.Editor
{
    /// <summary>Wires the shared Player and the existing mirror panel without rebuilding scenes.</summary>
    public static class PlayerAppearanceSetup
    {
        private const string PlayerPath = "Assets/Prefabs/Characters/Player.prefab";
        private const string CharacterFolder = "Assets/Art/Models/Characters/";
        private const string ControllerPath = "Assets/Art/Animations/Characters/Characters-Locomotion.controller";

        [MenuItem("NatureCalls/Configure Player Appearance")]
        public static void Apply()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null) throw new InvalidOperationException("Missing imported character animation controller.");
            var male = LoadVisual("Male");
            var female = LoadVisual("Female");
            ConfigureLanding(controller);

            var root = PrefabUtility.LoadPrefabContents(PlayerPath);
            try
            {
                var movement = root.GetComponent<Movement>();
                var body = movement.Body;
                if (body == null) throw new InvalidOperationException("Shared Player has no Body.");
                foreach (var oldName in new[] { "Legacy placeholder body (inactive)", "Peasant Nolant Brown visual", "Peasant Nolant Blue visual" })
                    if (body.Find(oldName) is { } old) Object.DestroyImmediate(old.gameObject);
                body.localScale = Vector3.one;
                var maleRig = Install(body, male, controller, true);
                var femaleRig = Install(body, female, controller, false);
                using var appearance = new SerializedObject(root.GetComponent<PlayerAppearance>());
                appearance.FindProperty("body").objectReferenceValue = body;
                appearance.FindProperty("male").objectReferenceValue = maleRig;
                appearance.FindProperty("female").objectReferenceValue = femaleRig;
                appearance.FindProperty("maleShirt").objectReferenceValue = Shirt(maleRig);
                appearance.FindProperty("femaleShirt").objectReferenceValue = Shirt(femaleRig);
                appearance.ApplyModifiedPropertiesWithoutUndo();
                Save(root, PlayerPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }

            var canvas = PrefabUtility.LoadPrefabContents(LobbyFlowBuilder.CanvasPath);
            try
            {
                LobbyFlowBuilder.ConfigureOutfitGender(canvas.GetComponent<LobbyView>());
                Save(canvas, LobbyFlowBuilder.CanvasPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(canvas); }
            AssetDatabase.SaveAssets();
            Debug.Log("PLAYER_APPEARANCE_SETUP_OK");
        }

        private static GameObject LoadVisual(string kind)
        {
            var path = CharacterFolder + "Characters-" + kind + ".fbx";
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var avatar = asset != null ? asset.GetComponent<Animator>()?.avatar : null;
            if (avatar == null || !avatar.isHuman || !avatar.isValid)
                throw new InvalidOperationException("Missing Humanoid model: " + path);
            return asset;
        }

        private static void ConfigureLanding(AnimatorController controller)
        {
            var jump = controller.layers[0].stateMachine.states.Single(item => item.state.name == "Jump").state;
            var landing = jump.transitions.Single(transition => transition.destinationState.name == "Idle");
            // Physics decides when the player lands, which can be later than the clip's exit time.
            if (!landing.hasExitTime) return;
            landing.hasExitTime = false;
            EditorUtility.SetDirty(landing);
        }

        private static Animator Install(Transform body, GameObject asset, RuntimeAnimatorController controller, bool active)
        {
            var visual = body.Find(asset.name)?.gameObject;
            if (visual == null)
            {
                visual = (GameObject)PrefabUtility.InstantiatePrefab(asset, body.gameObject.scene);
                visual.transform.SetParent(body, false);
            }
            visual.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            visual.transform.localScale = Vector3.one;
            var animator = visual.GetComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            visual.SetActive(active);
            return animator;
        }

        private static Renderer Shirt(Animator animator) =>
            animator.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single(renderer => renderer.name == "T-shirt");

        private static void Save(GameObject root, string path)
        {
            PrefabUtility.SaveAsPrefabAsset(root, path, out bool saved);
            if (!saved) throw new InvalidOperationException("Could not save " + path);
        }
    }
}
