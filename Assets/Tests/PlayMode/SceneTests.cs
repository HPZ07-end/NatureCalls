using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace NatureCalls.Tests
{
    public sealed class SceneTests
    {
        private Scene loadedScene;

        [UnityTest]
        public IEnumerator SmokeSceneLoadsWithCamera()
        {
            const string path = "Assets/Scenes/Smoke.unity";
            Assert.That(Application.CanStreamedLevelBeLoaded(path), Is.True,
                "Run setup to create and register the infrastructure smoke scene.");
            yield return SceneManager.LoadSceneAsync(path, LoadSceneMode.Additive);
            loadedScene = SceneManager.GetSceneByPath(path);
            Assert.That(loadedScene.isLoaded, Is.True);
            var cameras = 0;
            foreach (var root in loadedScene.GetRootGameObjects())
                cameras += root.GetComponentsInChildren<UnityEngine.Camera>().Length;
            Assert.That(cameras, Is.GreaterThan(0));
        }

        [UnityTearDown]
        public IEnumerator UnloadScene()
        {
            if (loadedScene.IsValid() && loadedScene.isLoaded)
                yield return SceneManager.UnloadSceneAsync(loadedScene);
        }
    }
}
