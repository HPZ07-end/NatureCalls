using NUnit.Framework;
using NatureCalls.Player;
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
        public void ChangingRoomUsesSharedPlayerContract()
        {
            var player = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab");
            var changingRoomPlayer = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/ChangingRoomPlayer.prefab");

            Assert.That(player, Is.Not.Null, "Shared Player prefab is missing.");
            Assert.That(changingRoomPlayer, Is.Not.Null, "Changing-room Player variant is missing.");
            Assert.That(PrefabUtility.GetCorrespondingObjectFromSource(changingRoomPlayer), Is.EqualTo(player));

            var root = PrefabUtility.LoadPrefabContents("Assets/Prefabs/Player.prefab");
            try
            {
                var movement = root.GetComponent<Movement>();
                var appearance = root.GetComponent<PlayerAppearance>();
                Assert.That(movement, Is.Not.Null);
                Assert.That(appearance, Is.Not.Null);
                Assert.That(movement.Body, Is.Not.Null);
                Assert.That(appearance.Body, Is.EqualTo(movement.Body));
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        [Test]
        public void ScenesHaveNoMissingReferences()
        {
            var guids = AssetDatabase.FindAssets("t:Scene", new[] { "Assets" });
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
