using System.Collections;
using NatureCalls.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace NatureCalls.Tests
{
    public sealed class LobbyUiTests
    {
        private const string LobbyScenePath = "Assets/UI/Lobby/LobbyUI.unity";
        private const string DressingRoomScenePath = "Assets/Scenes/DressingRoom.unity";
        private Scene scene;

        [UnityTest]
        public IEnumerator LobbyHasReturnToDressingRoomAndNoTemporaryDressingButton()
        {
            yield return SceneManager.LoadSceneAsync(LobbyScenePath, LoadSceneMode.Additive);
            scene = SceneManager.GetSceneByPath(LobbyScenePath);
            yield return null;

            LobbyUIController controller = null;
            Button returnButton = null;
            var hasLegacyDressingNavigation = false;
            var hasLegacyDressingPanel = false;
            var hasLegacyNavigation = false;
            foreach (var root in scene.GetRootGameObjects())
            {
                controller ??= root.GetComponentInChildren<LobbyUIController>(true);
                foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                {
                    hasLegacyDressingNavigation |= transform.name == "DressingNav";
                    hasLegacyDressingPanel |= transform.name == "DressingRoomPanel";
                    hasLegacyNavigation |= transform.name == "Navigation";
                }
                foreach (var button in root.GetComponentsInChildren<Button>(true))
                {
                    Assert.That(button.name, Is.Not.EqualTo("TestOpneButton"));
                    if (button.name == "ReturnToDressingRoom") returnButton = button;
                }
            }

            Assert.That(controller, Is.Not.Null);
            Assert.That(returnButton, Is.Not.Null);
            Assert.That(returnButton.transform.parent.name, Is.EqualTo("LobbyCanvas"));
            Assert.That(hasLegacyDressingNavigation, Is.False);
            Assert.That(hasLegacyDressingPanel, Is.False);
            Assert.That(hasLegacyNavigation, Is.False);
            Assert.That(Application.CanStreamedLevelBeLoaded(DressingRoomScenePath), Is.True);
            Assert.That(HasReturnListener(returnButton, controller), Is.True);
        }

        [UnityTearDown]
        public IEnumerator Unload()
        {
            if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
        }

        private static bool HasReturnListener(Button button, LobbyUIController controller)
        {
            for (var index = 0; index < button.onClick.GetPersistentEventCount(); index++)
            {
                if (button.onClick.GetPersistentTarget(index) == controller &&
                    button.onClick.GetPersistentMethodName(index) == nameof(LobbyUIController.ReturnToDressingRoom))
                    return true;
            }
            return false;
        }
    }
}
