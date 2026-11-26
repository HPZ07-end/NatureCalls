using System.Collections;
using System.Linq;
using NatureCalls.Core;
using NatureCalls.Facility;
using NatureCalls.Map;
using NatureCalls.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace NatureCalls.Tests
{
    public sealed class SabotageTests
    {
        private Playground world;
        private Movement player;
        private Toilet toilet;
        private Pipe pipe;
        private Toolbox toolbox;

        [UnitySetUp]
        public IEnumerator Load()
        {
            yield return SceneManager.LoadSceneAsync("Assets/Scenes/Playground.unity", LoadSceneMode.Additive);
            yield return null;
            world = Object.FindFirstObjectByType<Playground>();
            player = world.OfflinePlayer;
            toilet = Object.FindObjectsByType<Toilet>(FindObjectsSortMode.None).First();
            pipe = toilet.ConnectedPipe != null ? toilet.ConnectedPipe : Object.FindFirstObjectByType<Pipe>();
            toolbox = Object.FindFirstObjectByType<Toolbox>();
            RoleRegistry.Clear();
        }

        [UnityTearDown]
        public IEnumerator Unload()
        {
            RoleRegistry.Clear();
            world.StopPractice();
            yield return SceneManager.UnloadSceneAsync("Playground");
        }

        private InteractionContext Context() => new InteractionContext(player, 0, false, null);
        private void Saboteur() => RoleRegistry.Set(player, PlayerRole.Saboteur);
        private static void Carry(Item item, Movement owner) { item.gameObject.SetActive(true); item.ApplySharedPickup(owner); }

        [Test]
        public void KeeperCannotUseSabotageAction()
        {
            var paper = Object.FindFirstObjectByType<PaperRoll>();
            Carry(paper, player);
            Assert.That(toilet.Prompt(Context()), Does.Not.Contain("冲水"));
            var before = pipe.Accumulation;
            Assert.That(toilet.Perform(Context()), Is.False);
            Assert.That(pipe.Accumulation, Is.EqualTo(before));
            Assert.That(paper.gameObject.activeSelf, Is.True);
        }

        [Test]
        public void SabotagePaperAddsExactlyFortyAndConsumesRoll()
        {
            Saboteur();
            var paper = Object.FindFirstObjectByType<PaperRoll>();
            Carry(paper, player);
            pipe.Clear();
            Assert.That(toilet.Perform(Context()), Is.True);
            Assert.That(pipe.Accumulation, Is.EqualTo(40));
            Assert.That(paper.gameObject.activeSelf, Is.False);
            var second = new GameObject("second paper").AddComponent<PaperRoll>();
            Carry(second, player);
            Assert.That(toilet.Perform(Context()), Is.False, "The 20 second cooldown must reject a repeat.");
        }

        [Test]
        public void BreakRepairRestoresToiletAvailability()
        {
            Saboteur();
            var broken = toilet.GetComponentsInChildren<Breakable>(true).FirstOrDefault() ?? Object.FindFirstObjectByType<Breakable>();
            Assert.That(broken, Is.Not.Null);
            broken.Break(player);
            Assert.That(toilet.IsAvailable, Is.False);
            var wrench = toolbox.Wrench != null ? toolbox.Wrench : Object.FindFirstObjectByType<Wrench>();
            if (wrench == null) wrench = new GameObject("test wrench").AddComponent<Wrench>();
            Carry(wrench, player);
            Assert.That(RepairTools.Repair(player, broken), Is.True);
            Assert.That(toilet.IsAvailable, Is.True);
        }

        [Test]
        public void PlungerClearsBlockedPipe()
        {
            pipe.AddSabotagePaper(); pipe.AddSabotagePaper(); pipe.AddSabotagePaper();
            Assert.That(pipe.IsBlocked, Is.True);
            var plunger = toolbox.Plunger != null ? toolbox.Plunger : Object.FindFirstObjectByType<Plunger>();
            if (plunger == null) plunger = new GameObject("test plunger").AddComponent<Plunger>();
            Carry(plunger, player);
            Assert.That(RepairTools.Clear(player, toilet), Is.True);
            Assert.That(pipe.Accumulation, Is.Zero);
        }

        [Test]
        public void SplashCreatesPuddleAhead()
        {
            Saboteur();
            var sink = Object.FindFirstObjectByType<Sink>();
            player.transform.SetPositionAndRotation(sink.transform.position - Vector3.forward, Quaternion.identity);
            Assert.That(sink.Perform(Context()), Is.True, "接水");
            Assert.That(sink.Perform(Context()), Is.True, "洒水");
            Assert.That(world.Messes.Any(m => m != null && m.Kind == MessKind.Puddle &&
                Vector3.Distance(m.transform.position, player.transform.position + player.transform.forward * 1.5f) < .2f), Is.True);
        }

        [Test]
        public void KnockRaisesBodyEvent()
        {
            Saboteur();
            var door = Object.FindObjectsByType<StallDoor>(FindObjectsSortMode.None).First(d => d.Toilet != null);
            door.ApplySharedState(StallDoorState.Closed);
            door.Toilet.ApplySharedOccupant(player);
            var raised = false;
            System.Action<Movement> handler = target => raised = target == player;
            BodyEvents.Knocked += handler;
            try { Assert.That(door.Perform(Context()), Is.True); Assert.That(raised, Is.True); }
            finally { BodyEvents.Knocked -= handler; }
        }

        [Test]
        public void BreakableSharedValueMirrorsHostDamage()
        {
            var source = Object.FindFirstObjectByType<Breakable>();
            source.Break(player);
            var value = source.ReadShared(0);
            var replica = new GameObject("Breakable replica").AddComponent<Breakable>();
            try { replica.ApplyShared(0, value); Assert.That(replica.IsBroken, Is.True); }
            finally { Object.DestroyImmediate(replica.gameObject); }
        }
    }
}
