using System.Collections;
using System.IO;
using NatureCalls.Core;
using NatureCalls.Facility;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace NatureCalls.Tests
{
    /// <summary>Close urine puddles are drawn as one continuous trail instead of overlapping translucent patches.</summary>
    public sealed class UrineTrailTests
    {
        private Map.Playground world;

        [UnitySetUp] public IEnumerator Load()
        {
            yield return SceneManager.LoadSceneAsync("Assets/Scenes/Playground.unity", LoadSceneMode.Additive);
            yield return null;
            world = Object.FindFirstObjectByType<Map.Playground>();
        }

        [UnityTearDown] public IEnumerator Unload()
        {
            world.StopPractice();
            var manager = Unity.Netcode.NetworkManager.Singleton;
            if (manager != null) { if (manager.IsListening) manager.Shutdown(); Object.Destroy(manager.gameObject); yield return null; }
            yield return SceneManager.UnloadSceneAsync("Playground");
        }

        [UnityTest] public IEnumerator ClosePuddlesJoinIntoOneTrailAndALonePuddleKeepsItsPatch()
        {
            var trails = Object.FindFirstObjectByType<UrineTrails>();
            Assert.That(trails, Is.Not.Null, "Installed on maps with a Playground.");
            var player = world.OfflinePlayer;
            var start = player.transform.position + player.transform.forward * 1.2f;
            var side = player.transform.right;
            int a = world.AddMess(MessKind.Puddle, start);
            int b = world.AddMess(MessKind.Puddle, start + side * .3f);
            int c = world.AddMess(MessKind.Puddle, start + side * .6f + player.transform.forward * .1f);
            int lone = world.AddMess(MessKind.Puddle, start + side * 4f);
            yield return null; yield return null;
            Assert.That(trails.TrailCount, Is.EqualTo(1), "The three close puddles form one trail.");
            Assert.That(PatchVisible(world.Messes[a]), Is.False, "Joined puddles hide their own patch.");
            Assert.That(PatchVisible(world.Messes[lone]), Is.True, "A lone puddle keeps its patch.");
            Capture(player, start + side * .3f, "urine-trail.png");
            world.RemoveMess(b); world.RemoveMess(c); yield return null; yield return null;
            Assert.That(trails.TrailCount, Is.EqualTo(0), "Cleaning breaks the trail apart.");
            Assert.That(PatchVisible(world.Messes[a]), Is.True);
        }

        private static bool PatchVisible(Mess mess)
        {
            var look = mess.GetComponent<MessLook>();
            return look != null && look.Look != null && look.Look.GetComponentInChildren<Renderer>().enabled;
        }

        private void Capture(Player.Movement player, Vector3 focus, string name)
        {
            var dir = System.Environment.GetEnvironmentVariable("NATURECALLS_RUN_PATH");
            if (string.IsNullOrEmpty(dir)) return;
            var go = new GameObject("Trail shot camera"); var camera = go.AddComponent<UnityEngine.Camera>();
            go.transform.position = focus + Vector3.up * 1.6f - player.transform.forward * 1.1f; go.transform.LookAt(focus);
            var texture = new RenderTexture(960, 540, 24); texture.Create(); camera.targetTexture = texture;
            var image = new Texture2D(960, 540, TextureFormat.RGB24, false); var previous = RenderTexture.active;
            try
            {
                UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera, new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest { destination = texture });
                RenderTexture.active = texture; image.ReadPixels(new Rect(0, 0, 960, 540), 0, 0); image.Apply();
                File.WriteAllBytes(Path.Combine(dir, name), image.EncodeToPNG());
            }
            finally { RenderTexture.active = previous; camera.targetTexture = null; texture.Release(); Object.Destroy(texture); Object.Destroy(image); Object.Destroy(go); }
        }
    }
}
