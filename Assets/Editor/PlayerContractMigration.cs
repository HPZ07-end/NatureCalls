using System;
using System.IO;
using NatureCalls.Input;
using NatureCalls.Player;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NatureCalls.Editor
{
    public static class PlayerContractMigration
    {
        private const string PlayerPrefabPath = "Assets/Prefabs/Player.prefab";
        private const string ChangingRoomPlayerPrefabPath = "Assets/Prefabs/ChangingRoomPlayer.prefab";
        private const string LegacyEnvironmentPath = "Assets/Environment";
        private const string FacilityChangingRoomPath = "Assets/Facility/ChangingRoom";

        [MenuItem("NatureCalls/Player/Migrate Shared Contract")]
        public static void Run()
        {
            MoveChangingRoomAssets();
            EnsurePlayerContract();
            EnsureChangingRoomVariant();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("NatureCalls shared Player contract migrated.");
        }

        public static void BuildChangingRoom()
        {
            ChangingRoomSceneBuilder.BuildSceneForAutomation();
            AssetDatabase.SaveAssets();
            Debug.Log("NatureCalls changing room rebuilt from the shared Player prefab.");
        }

        private static void MoveChangingRoomAssets()
        {
            MoveAsset(LegacyEnvironmentPath + "/ChangingRoom", FacilityChangingRoomPath);
            MoveAsset(LegacyEnvironmentPath + "/ChangingRoomPlatform.cs", "Assets/Facility/ChangingRoomPlatform.cs");

            if (AssetDatabase.IsValidFolder(LegacyEnvironmentPath))
            {
                var remaining = AssetDatabase.FindAssets(string.Empty, new[] { LegacyEnvironmentPath });
                if (remaining.Length == 0 && !AssetDatabase.DeleteAsset(LegacyEnvironmentPath))
                    throw new InvalidOperationException("Could not remove the empty legacy Environment folder.");
            }
        }

        private static void MoveAsset(string source, string destination)
        {
            if (AssetDatabase.LoadAssetAtPath<Object>(destination) != null || AssetDatabase.IsValidFolder(destination))
                return;
            if (AssetDatabase.LoadAssetAtPath<Object>(source) == null && !AssetDatabase.IsValidFolder(source))
                throw new FileNotFoundException("Migration source is missing.", source);

            var error = AssetDatabase.MoveAsset(source, destination);
            if (!string.IsNullOrEmpty(error))
                throw new InvalidOperationException(error);
        }

        private static void EnsurePlayerContract()
        {
            var root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            try
            {
                var movement = root.GetComponent<Movement>();
                if (movement == null || movement.Body == null)
                    throw new InvalidOperationException("Player.prefab must expose Movement.Body.");

                var appearance = root.GetComponent<PlayerAppearance>() ?? root.AddComponent<PlayerAppearance>();
                SetObjectReference(appearance, "body", movement.Body);
                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void EnsureChangingRoomVariant()
        {
            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (playerPrefab == null)
                throw new FileNotFoundException("Shared Player prefab is missing.", PlayerPrefabPath);

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
            try
            {
                instance.name = "ChangingRoomPlayer";
                SetBoolean(instance.GetComponent<Movement>(), "startControlled", true);

                var input = instance.GetComponent<PlayerInput>();
                if (input != null)
                    input.CaptureMouse = false;

                var appearance = instance.GetComponent<PlayerAppearance>();
                var outfit = instance.GetComponent<PlayerOutfitController>() ?? instance.AddComponent<PlayerOutfitController>();
                SetObjectReference(outfit, "appearance", appearance);

                var saved = PrefabUtility.SaveAsPrefabAsset(instance, ChangingRoomPlayerPrefabPath, out var success);
                if (!success || saved == null)
                    throw new InvalidOperationException("Could not save the ChangingRoomPlayer prefab variant.");
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        private static void SetObjectReference(Object target, string propertyName, Object value)
        {
            var serializedObject = new SerializedObject(target);
            serializedObject.FindProperty(propertyName).objectReferenceValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetBoolean(Object target, string propertyName, bool value)
        {
            var serializedObject = new SerializedObject(target);
            serializedObject.FindProperty(propertyName).boolValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
