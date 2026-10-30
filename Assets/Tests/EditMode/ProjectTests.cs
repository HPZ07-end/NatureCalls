using NUnit.Framework;
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
