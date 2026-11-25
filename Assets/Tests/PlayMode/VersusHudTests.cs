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
        private Func<(bool, string, string)> outcome;

        [UnitySetUp] public IEnumerator Load()
        {
            endurance = VersusHudFeed.Endurance; vomit = VersusHudFeed.Vomit; comfort = VersusHudFeed.Comfort;
            bowel = VersusHudFeed.Bowel; bladder = VersusHudFeed.Bladder; composure = VersusHudFeed.Composure;
            latest = VersusHudFeed.LatestEvent; outcome = VersusHudFeed.Outcome;
            yield return SceneManager.LoadSceneAsync("Assets/Scenes/Playground.unity", LoadSceneMode.Additive);
            yield return null;
            world = Object.FindFirstObjectByType<Map.Playground>();
        }

        [UnityTearDown] public IEnumerator Unload()
        {
            VersusHudFeed.Endurance = endurance; VersusHudFeed.Vomit = vomit; VersusHudFeed.Comfort = comfort;
            VersusHudFeed.Bowel = bowel; VersusHudFeed.Bladder = bladder; VersusHudFeed.Composure = composure;
            VersusHudFeed.LatestEvent = latest; VersusHudFeed.Outcome = outcome;
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
            Assert.That(Find(hud, "Endurance pulse").GetComponent<Image>().enabled, Is.True);
            Assert.That(Find(hud, "Toast").GetComponent<CanvasGroup>().alpha, Is.GreaterThan(.5f));
            yield return Capture("hud-playing.png");

            VersusHudFeed.Endurance = _ => 0f; VersusHudFeed.Vomit = _ => .6f; VersusHudFeed.Comfort = _ => .8f;
            for (int i = 0; i < 5; i++) yield return null;
            Assert.That(Find(hud, "Nausea").GetComponent<Image>().enabled, Is.True);
            Assert.That(Find(hud, "Comfort glow").GetComponent<Image>().enabled, Is.True);
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

        private static IEnumerator Capture(string name)
        {
            yield return new WaitForEndOfFrame();
            var directory = Environment.GetEnvironmentVariable("NATURECALLS_RUN_PATH");
            if (string.IsNullOrEmpty(directory)) yield break;
            var texture = ScreenCapture.CaptureScreenshotAsTexture();
            try { File.WriteAllBytes(Path.Combine(directory, name), texture.EncodeToPNG()); }
            finally { Object.Destroy(texture); }
        }
    }
}
