using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using NatureCalls.Facility;
using NatureCalls.Map;
using NatureCalls.Player;
using PlayerInput = NatureCalls.Input.PlayerInput;

namespace NatureCalls.Tests
{
    public sealed class FlushButtonTests
    {
        private static readonly string[] Scenes = { "Playground", "Map1", "Map2", "Map3" };

        [UnityTest] public IEnumerator PlaygroundButtonIsOnlyFlushTarget() => Check("Assets/Scenes/Playground.unity", "flush-playground.png");
        [UnityTest] public IEnumerator Map1ButtonIsOnlyFlushTarget() => Check("Assets/Scenes/Map1.unity", "flush-map1.png");
        [UnityTest] public IEnumerator Map2ButtonIsOnlyFlushTarget() => Check("Assets/Scenes/Map2.unity", "flush-map2.png");
        [UnityTest] public IEnumerator Map3ButtonIsOnlyFlushTarget() => Check("Assets/Scenes/Map3.unity", "flush-map3.png");

        private static IEnumerator Check(string path, string screenshot)
        {
            yield return SceneManager.LoadSceneAsync(path, LoadSceneMode.Additive);
            yield return null;
            var scene = SceneManager.GetSceneByPath(path);
            var world = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Playground>(true)).FirstOrDefault();
            Assert.That(world, Is.Not.Null, path);
            var player = world.OfflinePlayer; Assert.That(player, Is.Not.Null, path);
            var input = player.GetComponent<PlayerInput>(); input.enabled = false;
            var button = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<ToiletFlushButton>(true)).FirstOrDefault();
            Assert.That(button, Is.Not.Null, path + " has a flush button");
            var controller = player.GetComponent<CharacterController>(); controller.enabled = false;
            var approach = Vector3.ProjectOnPlane(world.Spawns[0].position - button.transform.position, Vector3.up).normalized;
            if (approach.sqrMagnitude < .01f) approach = -button.transform.forward;
            player.transform.SetPositionAndRotation(button.transform.position + approach * .95f, Quaternion.LookRotation(-approach));
            controller.enabled = true; Physics.SyncTransforms();
            TestAim.AimAt(player, button); input.RefreshFocus();
            var hit = Physics.Raycast(player.ViewRay, out var hitInfo, 3f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
            Assert.That(input.Focus, Is.SameAs(button), path + $" button focus active={button.gameObject.activeInHierarchy} dist={Vector3.Distance(player.transform.position, button.transform.position):F2} can={button.CanInteract(new InteractionContext(player, 0, false, null))} ray={hit} hit={(hit ? hitInfo.collider.name : "none")}");
            Capture(world.Camera.Output, screenshot);
            Assert.That(input.TryInteract(), Is.True, path + " flush button performs");

            TestAim.AimAt(player, button.GetComponentInParent<Toilet>()); input.RefreshFocus();
            Assert.That(input.Focus, Is.Not.SameAs(button), path + " toilet body is not flush target");
            controller.enabled = false; player.transform.position = button.transform.position - button.transform.forward * 3f; controller.enabled = true;
            TestAim.AimAt(player, button); input.RefreshFocus();
            Assert.That(input.Focus, Is.Null, path + " distant button is out of reach");
            world.StopPractice();
            yield return SceneManager.UnloadSceneAsync(scene);
        }

        private static void Capture(UnityEngine.Camera camera, string name)
        {
            var dir = System.Environment.GetEnvironmentVariable("NATURECALLS_RUN_PATH");
            if (string.IsNullOrEmpty(dir) || camera == null) return;
            Directory.CreateDirectory(dir);
            ScreenCapture.CaptureScreenshot(Path.Combine(dir, name));
        }
    }
}
