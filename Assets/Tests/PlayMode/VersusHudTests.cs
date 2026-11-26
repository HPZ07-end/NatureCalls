using System;
using System.Collections;
using System.IO;
using NatureCalls.Core;
using NatureCalls.Player;
using NatureCalls.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace NatureCalls.Tests
{
    /// <summary>Drives the versus HUD through reveal, play (with body signals) and the end card, and saves screenshots.</summary>
    public sealed class VersusHudTests
    {
        private Map.Playground world;
        private Func<Movement, float> endurance, vomit, comfort, bowel, bladder, composure;
        private Func<(string, int)> latest;
        private Func<Movement, StoolKind> nextStool;
        private Func<Movement, float> speed;
        private Func<(bool, string, string)> outcome;

        [UnitySetUp] public IEnumerator Load()
        {
            endurance = VersusHudFeed.Endurance; vomit = VersusHudFeed.Vomit; comfort = VersusHudFeed.Comfort;
            bowel = VersusHudFeed.Bowel; bladder = VersusHudFeed.Bladder; composure = VersusHudFeed.Composure;
            latest = VersusHudFeed.LatestEvent; outcome = VersusHudFeed.Outcome;
            nextStool = VersusHudFeed.NextStool; speed = VersusHudFeed.Speed;
            yield return SceneManager.LoadSceneAsync("Assets/Scenes/Playground.unity", LoadSceneMode.Additive);
            yield return null;
            world = Object.FindFirstObjectByType<Map.Playground>();
        }

        [UnityTearDown] public IEnumerator Unload()
        {
            VersusHudFeed.Endurance = endurance; VersusHudFeed.Vomit = vomit; VersusHudFeed.Comfort = comfort;
            VersusHudFeed.Bowel = bowel; VersusHudFeed.Bladder = bladder; VersusHudFeed.Composure = composure;
            VersusHudFeed.LatestEvent = latest; VersusHudFeed.Outcome = outcome;
            VersusHudFeed.NextStool = nextStool; VersusHudFeed.Speed = speed;
            MatchContext.Reset(); RoleRegistry.Clear();
            world.StopPractice();
            var manager = Unity.Netcode.NetworkManager.Singleton;
            if (manager != null) { if (manager.IsListening) manager.Shutdown(); Object.Destroy(manager.gameObject); yield return null; }
            yield return SceneManager.UnloadSceneAsync("Playground");
        }

        [UnityTest] public IEnumerator HudFollowsPhasesAndBodySignals()
        {
            var hud = Object.FindFirstObjectByType<VersusHud>();
            Assert.That(hud, Is.Not.Null, "The HUD installs itself on maps with a Playground.");
            yield return null;
            Assert.That(hud.GetComponent<Canvas>().enabled, Is.False, "Hidden outside versus rounds.");

            var me = world.OfflinePlayer;
            RoleRegistry.Set(me, PlayerRole.Saboteur);
            MatchContext.Schedule = new UrgeSchedule(new[] { 120f, 264f, 408f }, new[] { .6f, 1.3f, 2f }, 20f, 480f);
            MatchContext.Phase = MatchPhase.Reveal;
            yield return null; yield return null;
            Assert.That(hud.GetComponent<Canvas>().enabled, Is.True);
            Assert.That(Find(hud, "Reveal").activeSelf, Is.True);
            Assert.That(Find(hud, "Reveal").GetComponentInChildren<Text>().text, Does.Contain("捣蛋者"));
            yield return Capture("hud-reveal.png");

            MatchContext.Phase = MatchPhase.Playing; MatchContext.Elapsed = 250; MatchContext.Tidiness = 34;
            VersusHudFeed.Bowel = _ => .95f; VersusHudFeed.Bladder = _ => .5f; VersusHudFeed.Composure = _ => .3f;
            VersusHudFeed.Endurance = _ => .7f; VersusHudFeed.LatestEvent = () => ("停电了！灯光暗下来 10 秒", 1);
            for (int i = 0; i < 20; i++) yield return null;
            Assert.That(Find(hud, "Tidiness").GetComponentInChildren<Text>().text, Does.Contain("34"));
            var pulse = Find(hud, "Endurance pulse").GetComponent<RawImage>();
            Assert.That(pulse.enabled, Is.True);
            var rim = (Texture2D)pulse.texture;
            Assert.That(rim.GetPixel(rim.width / 2, rim.height / 2).a, Is.LessThan(.05f), "The vignette leaves the centre clear.");
            Assert.That(rim.GetPixel(2, rim.height / 2).a, Is.GreaterThan(.8f), "The vignette darkens the rim.");
            Assert.That(Find(hud, "Toast").GetComponent<CanvasGroup>().alpha, Is.GreaterThan(.5f));
            yield return Capture("hud-playing.png");
            var gut = Find(hud, "Intestine").GetComponent<IntestineGauge>();
            Assert.That(gut.Fill, Is.EqualTo(.95f).Within(.01f), "The intestine fills with the bowel urge.");
            Assert.That(Find(hud, "Waves").GetComponent<Text>().text, Is.Empty, "No wave text: urge is shown by the organs.");
            VersusHudFeed.Endurance = _ => 0f;
            foreach (var kind in new[] { StoolKind.Soft, StoolKind.Hard, StoolKind.Loose })
            {
                VersusHudFeed.NextStool = _ => kind; VersusHudFeed.Speed = _ => kind == StoolKind.Loose ? 1f : 0f;
                VersusHudFeed.Bowel = _ => .6f;
                for (int i = 0; i < 6; i++) yield return null;
                Assert.That(gut.Kind, Is.EqualTo(kind));
                yield return Capture("hud-gauge-" + kind.ToString().ToLowerInvariant() + ".png");
            }

            VersusHudFeed.Endurance = _ => 0f; VersusHudFeed.Vomit = _ => .6f; VersusHudFeed.Comfort = _ => .8f;
            for (int i = 0; i < 5; i++) yield return null;
            Assert.That(Find(hud, "Nausea").GetComponent<Graphic>().enabled, Is.True);
            Assert.That(Find(hud, "Comfort glow").GetComponent<Graphic>().enabled, Is.True);
            yield return Capture("hud-nausea-comfort.png");

            VersusHudFeed.Vomit = _ => 0f; VersusHudFeed.Comfort = _ => 0f;
            VersusHudFeed.Outcome = () => (true, "撑过了最后一波高峰，干净值还剩 34", "产生污物 12 · 清理 9\n失禁 1 · 呕吐 2 · 倒灌 1");
            MatchContext.Phase = MatchPhase.Ended;
            yield return null; yield return null;
            Assert.That(Find(hud, "End").activeSelf, Is.True);
            Assert.That(Find(hud, "End").GetComponentInChildren<Text>().text, Is.EqualTo("保洁队胜利"));
            yield return Capture("hud-end.png");
        }

        private static GameObject Find(VersusHud hud, string name)
        {
            foreach (var t in hud.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t.gameObject;
            Assert.Fail("Missing HUD element " + name);
            return null;
        }

        // Batch-mode runs have no window, so WaitForEndOfFrame never fires and ScreenCapture is empty: render the
        // map camera with the HUD canvas temporarily attached to it instead.
        private IEnumerator Capture(string name)
        {
            yield return null;
            var directory = Environment.GetEnvironmentVariable("NATURECALLS_RUN_PATH");
            var camera = UnityEngine.Camera.main;
            if (string.IsNullOrEmpty(directory) || camera == null) yield break;
            var canvas = Object.FindFirstObjectByType<VersusHud>().GetComponent<Canvas>();
            var mode = canvas.renderMode;
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = camera.nearClipPlane + .05f;
            Canvas.ForceUpdateCanvases();
            var texture = new RenderTexture(1280, 720, 24); texture.Create();
            var previous = RenderTexture.active;
            var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            try
            {
                UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera, new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest { destination = texture });
                RenderTexture.active = texture;
                image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); image.Apply();
                File.WriteAllBytes(Path.Combine(directory, name), image.EncodeToPNG());
            }
            finally { RenderTexture.active = previous; texture.Release(); Object.Destroy(texture); Object.Destroy(image); canvas.renderMode = mode; }
        }
    }
}
