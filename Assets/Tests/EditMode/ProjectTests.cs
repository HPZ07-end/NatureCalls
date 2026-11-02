using System.Linq;
using NUnit.Framework;
using NatureCalls.Map;
using NatureCalls.Network;
using NatureCalls.Player;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NatureCalls.Tests
{
    public sealed class ProjectTests
    {
        [Test]
        public void SerializationIsText()
        {
            Assert.That(EditorSettings.serializationMode, Is.EqualTo(SerializationMode.ForceText));
        }

        [Test]
        public void MetadataIsVisible()
        {
            Assert.That(VersionControlSettings.mode, Is.EqualTo("Visible Meta Files"));
        }

        [Test]
        public void PrefabsHaveNoMissingReferences()
        {
            var guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" });
            Assert.That(guids.Length, Is.GreaterThanOrEqualTo(2), "Player and Toilet prefabs must be generated.");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    foreach (var component in root.GetComponentsInChildren<Component>(true))
                    {
                        Assert.That(component, Is.Not.Null, "Missing script: " + path);
                        using (var serialized = new SerializedObject(component))
                        {
                            var property = serialized.GetIterator();
                            while (property.Next(true))
                            {
                                if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
                                Assert.That(property.objectReferenceValue != null || property.objectReferenceInstanceIDValue == 0,
                                    Is.True, path + ": " + component.name + "." + property.propertyPath);
                            }
                        }
                    }
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
        }

        [Test]
        public void DressingRoomPlayerUsesSharedPlayerContract()
        {
            var player = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Characters/Player.prefab");
            var dressingRoomPlayer = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Characters/DressingRoomPlayer.prefab");

            Assert.That(player, Is.Not.Null, "Shared Player prefab is missing.");
            Assert.That(dressingRoomPlayer, Is.Not.Null, "DressingRoom Player variant is missing.");
            Assert.That(PrefabUtility.GetCorrespondingObjectFromSource(dressingRoomPlayer), Is.EqualTo(player));
            Assert.That(dressingRoomPlayer.GetComponent<PlayerOutfitController>(), Is.Not.Null);
            Assert.That(dressingRoomPlayer.GetComponent<NetworkObject>(), Is.Null, "The DressingRoom player is local only.");

            var root = PrefabUtility.LoadPrefabContents("Assets/Prefabs/Characters/Player.prefab");
            try
            {
                var movement = root.GetComponent<Movement>();
                var appearance = root.GetComponent<PlayerAppearance>();
                Assert.That(movement, Is.Not.Null);
                Assert.That(appearance, Is.Not.Null);
                Assert.That(movement.Body, Is.Not.Null);
                Assert.That(appearance.Body, Is.EqualTo(movement.Body));
                Assert.That(appearance.Catalog, Is.Not.Null, "Player appearance needs the outfit catalog to show synced outfits.");
                Assert.That(appearance.Catalog.Count, Is.GreaterThan(0));
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        [Test]
        public void LobbyFlowAssetsAreWired()
        {
            var lobbyRoom = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Network/LobbyRoom.prefab");
            var network = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Network/Network.prefab");
            var prefabs = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>("Assets/DefaultNetworkPrefabs.asset");
            var maps = AssetDatabase.LoadAssetAtPath<MapCatalog>("Assets/Map/MapCatalog.asset");
            Assert.That(lobbyRoom, Is.Not.Null);
            Assert.That(network, Is.Not.Null);
            Assert.That(prefabs.Contains(lobbyRoom), Is.True, "LobbyRoom must be a registered network prefab.");
            foreach (var entry in prefabs.PrefabList)
                Assert.That(entry.Prefab != null && entry.Prefab.GetComponent<NetworkObject>() != null, Is.True, "Every registered network prefab needs a NetworkObject.");
            var session = network.GetComponent<Session>();
            Assert.That(session.LobbyRoomPrefab, Is.EqualTo(lobbyRoom));
            Assert.That(session.World, Is.Null, "The Lobby network object must not bind a map.");
            Assert.That(network.GetComponent<NetworkManager>().NetworkConfig.Prefabs.NetworkPrefabsLists, Does.Contain(prefabs));

            var buildScenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray();
            Assert.That(buildScenes.First(), Is.EqualTo("Assets/Scenes/DressingRoom.unity"), "The game opens in the DressingRoom.");
            Assert.That(maps.Count, Is.GreaterThan(0));
            for (int index = 0; index < maps.Count; index++)
                Assert.That(buildScenes, Does.Contain(maps[index].ScenePath), maps[index].DisplayName + " is not in Build Settings.");
        }

        [Test]
        public void ScenesHaveNoMissingReferences()
        {
            // Project scenes only; third-party demo scenes (e.g. Assets/WC/DemoScene) reference packages we do not use.
            var guids = AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes" });
            Assert.That(guids.Length, Is.GreaterThan(0), "No scene assets were found.");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var scene = SceneManager.GetSceneByPath(path);
                var wasLoaded = scene.IsValid() && scene.isLoaded;
                if (!wasLoaded) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                try
                {
                    foreach (var root in scene.GetRootGameObjects())
                    foreach (var component in root.GetComponentsInChildren<Component>(true))
                    {
                        Assert.That(component, Is.Not.Null, "Missing script in " + path);
                        using (var serialized = new SerializedObject(component))
                        {
                            var property = serialized.GetIterator();
                            while (property.Next(true))
                            {
                                if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
                                Assert.That(property.objectReferenceValue != null || property.objectReferenceInstanceIDValue == 0,
                                    Is.True, path + ": " + component.name + "." + property.propertyPath);
                            }
                        }
                    }
                }
                finally
                {
                    if (!wasLoaded) EditorSceneManager.CloseScene(scene, true);
                }
            }
        }
    }
}
