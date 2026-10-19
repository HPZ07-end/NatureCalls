using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using NatureCalls.Player;
using NatureCalls.Facility;

namespace NatureCalls.Tests
{
    public sealed class PlaygroundTests
    {
        private Scene scene;
        private Map.Playground world;
        [UnitySetUp]
        public IEnumerator Load()
        {
            yield return SceneManager.LoadSceneAsync("Assets/Scenes/Playground.unity", LoadSceneMode.Additive);
            scene = SceneManager.GetSceneByPath("Assets/Scenes/Playground.unity");
            foreach (var root in scene.GetRootGameObjects())
                if (root.TryGetComponent(out Map.Playground found)) world = found;
            yield return null;
            Assert.That(world, Is.Not.Null);
            Assert.That(world.OfflinePlayer, Is.Not.Null);
            world.OfflinePlayer.GetComponent<NatureCalls.Input.PlayerInput>().enabled = false;
            yield return new WaitForSeconds(.2f);
        }
        [UnityTearDown]
        public IEnumerator Unload()
        {
            if (world != null) world.StopPractice();
            if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
        }
        [UnityTest]
        public IEnumerator MovementStopsAtWallAndSquattingStopsMovement()
        {
            var player = world.OfflinePlayer;
            var controller = player.GetComponent<CharacterController>();
            controller.enabled = false;
            player.transform.position = new Vector3(4, 0, -5);
            controller.enabled = true;
            for (int i = 0; i < 50; i++) { player.Move(Vector2.down, .05f); yield return null; }
            Assert.That(player.transform.position.z, Is.InRange(-6.7f, -6.3f), "Physical wall must stop movement.");
            player.GetComponent<Posture>().Set(PostureState.Squatting);
            var start = player.transform.position;
            for (int i = 0; i < 20; i++) { player.Move(Vector2.up, .05f); yield return null; }
            Assert.That(Vector2.Distance(new Vector2(start.x, start.z), new Vector2(player.transform.position.x, player.transform.position.z)), Is.LessThan(.01f));
        }
        [UnityTest]
        public IEnumerator CameraBlendsAndInteriorIsVisible()
        {
            var player = world.OfflinePlayer;
            Toilet toilet = null;
            foreach (var root in scene.GetRootGameObjects()) if (root.TryGetComponent(out Toilet found)) toilet = found;
            Assert.That(toilet, Is.Not.Null);
            var controller = player.GetComponent<CharacterController>();
            controller.enabled = false;
            player.transform.position = toilet.Anchor.position - Vector3.forward * .3f;
            controller.enabled = true;
            player.Look(new Vector2(40, -50));
            yield return null;
            var heading = player.ViewRotation;
            var camera = world.Camera.Output;
            Capture(camera, "standing.png");
            Assert.That(toilet.Enter(player), Is.True);
            Assert.That(player.GetComponent<Posture>().State, Is.EqualTo(PostureState.Squatting));
            var last = camera.transform.position;
            float deadline = Time.time + 1;
            int intermediateFrames = 0;
            while (Time.time < deadline)
            {
                yield return null;
                Assert.That(Vector3.Distance(last, camera.transform.position), Is.LessThan(1), "Blend jumped in one frame.");
                last = camera.transform.position;
                if (world.Camera.Brain.IsBlending) intermediateFrames++;
            }
            Assert.That(intermediateFrames, Is.GreaterThan(1), "Expected a real multi-frame Cinemachine blend.");
            Assert.That(world.Camera.ThirdPerson.Priority.Value, Is.GreaterThan(world.Camera.FirstPerson.Priority.Value));
            Capture(camera, "squatting.png");
            var viewport = camera.WorldToViewportPoint(toilet.Interior.position);
            Assert.That(viewport.x, Is.InRange(.05f, .95f));
            Assert.That(viewport.y, Is.InRange(.05f, .95f));
            Assert.That(viewport.z, Is.GreaterThan(0));
            var ray = new Ray(camera.transform.position, toilet.Interior.position - camera.transform.position);
            Assert.That(Physics.Raycast(ray, out var hit, 20, 1), Is.True);
            Assert.That(hit.transform, Is.EqualTo(toilet.Interior), "World geometry obscures the interior placeholder.");
            foreach (var renderer in player.Body.GetComponentsInChildren<Renderer>())
            {
                Assert.That(renderer.forceRenderingOff, Is.False, "Local third-person body must be visible.");
                var bounds = renderer.bounds;
                for (int corner = 0; corner < 8; corner++)
                {
                    var point = bounds.center + Vector3.Scale(bounds.extents, new Vector3((corner & 1) == 0 ? -1 : 1,
                        (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1));
                    var screen = camera.WorldToViewportPoint(point);
                    Assert.That(screen.z, Is.GreaterThan(0));
                    Assert.That(screen.x, Is.InRange(.02f, .98f), "Body clipped horizontally: " + renderer.name);
                    Assert.That(screen.y, Is.InRange(.02f, .98f), "Body clipped vertically: " + renderer.name);
                }
                Assert.That(renderer.bounds.IntersectRay(ray, out var distance) && distance < Vector3.Distance(ray.origin, toilet.Interior.position), Is.False,
                    "Local body obscures the center of the placeholder: " + renderer.name);
            }
            Capture(camera, "squatting.png");
            player.GetComponent<Posture>().Set(PostureState.Standing);
            yield return new WaitForSeconds(1);
            Assert.That(Quaternion.Angle(player.ViewRotation, heading), Is.LessThan(.1f), "Return must preserve standing look direction.");
            Assert.That(Vector3.Distance(camera.transform.position, player.transform.position + Vector3.up * world.Camera.EyeHeight), Is.LessThan(.05f));
            foreach (var renderer in player.Body.GetComponentsInChildren<Renderer>()) Assert.That(renderer.forceRenderingOff, Is.True);
            Capture(camera, "returned.png");
        }
        private static void Capture(UnityEngine.Camera camera, string name)
        {
            var directory = System.Environment.GetEnvironmentVariable("NATURECALLS_RUN_PATH");
            if (string.IsNullOrEmpty(directory)) return;
            var texture = new RenderTexture(1100, 760, 24);
            texture.Create();
            var previous = RenderTexture.active;
            var image = new Texture2D(1100, 760, TextureFormat.RGB24, false);
            try
            {
                RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = texture });
                RenderTexture.active = texture;
                image.ReadPixels(new Rect(0, 0, 1100, 760), 0, 0);
                image.Apply();
                File.WriteAllBytes(Path.Combine(directory, name), image.EncodeToPNG());
            }
            finally { RenderTexture.active = previous; texture.Release(); Object.Destroy(texture); Object.Destroy(image); }
        }
    }
}
