using System.Collections;
using System.Linq;
using NUnit.Framework;
using NatureCalls.Core;
using NatureCalls.Facility;
using NatureCalls.Map;
using NatureCalls.Network;
using NatureCalls.Player;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace NatureCalls.Tests
{
    public sealed class VersusMatchTests
    {
        [UnityTest]
        public IEnumerator OfflineFlowDirtAndSaboteurWin()
        {
            Match.End(); yield return SceneManager.LoadSceneAsync("Assets/Scenes/Playground.unity", LoadSceneMode.Single);
            Match.RevealDurationSeconds = 0; Match.CleanupDurationSeconds = 0; Match.SettlementDurationSeconds = 30;
            Match.BeginOfflineInCurrentSceneForTests(); Match.StartOfflineRound();
            var world = Object.FindFirstObjectByType<Playground>(); Assert.That(world, Is.Not.Null);
            yield return null;
            Assert.That(MatchContext.Phase, Is.EqualTo(MatchPhase.Playing));
            var player = world.OfflinePlayer;
            MatchDirector.ForceSaboteur = true;
            RoleRegistry.Set(player, PlayerRole.Saboteur);
            var index = world.AddMess(MessKind.Dropping, player.transform.position, -1, player);
            yield return null;
            Assert.That(MatchContext.Tidiness, Is.LessThan(100));
            var mess = world.Messes[index]; Assert.That(mess.DirtWeight, Is.EqualTo(.3f).Within(.001f));
            world.RemoveMess(index); yield return null; Assert.That(MatchContext.Tidiness, Is.EqualTo(100).Within(.01f));
            for (var i = 0; i < 60; i++) world.AddMess(MessKind.Dropping, player.transform.position + Vector3.right * (i * .01f), -1, player);
            yield return null; yield return null;
            Assert.That(MatchContext.Phase, Is.EqualTo(MatchPhase.Ended));
            Assert.That(MatchContext.Winner, Is.EqualTo(MatchWinner.Saboteur));
            Assert.That(MatchContext.EndReason, Does.Contain("干净值归零"));
            Match.End();
        }

        [UnityTest]
        public IEnumerator OfflineCleanupEndsWithKeeperWin()
        {
            Match.End(); yield return SceneManager.LoadSceneAsync("Assets/Scenes/Playground.unity", LoadSceneMode.Single);
            Match.RevealDurationSeconds = 0; Match.CleanupDurationSeconds = 0; Match.SettlementDurationSeconds = 30;
            Match.BeginOfflineInCurrentSceneForTests(); Match.StartOfflineRound();
            Match.RoundSecondsOverrideForTests = .1f;
            yield return new WaitForSeconds(.25f);
            Assert.That(MatchContext.Phase, Is.EqualTo(MatchPhase.Ended));
            Assert.That(MatchContext.Winner, Is.EqualTo(MatchWinner.Keepers));
            Assert.That(MatchContext.Settlement.Reason, Does.Contain("保洁队"));
            Match.End();
        }

        [UnityTest]
        public IEnumerator RandomEventsApplyAllEffects()
        {
            yield return SceneManager.LoadSceneAsync("Assets/Scenes/Map2.unity", LoadSceneMode.Single);
            MatchContext.Schedule = MatchRules.Schedule(); MatchContext.Elapsed = 30;
            var pipe = Object.FindFirstObjectByType<Pipe>(); Assert.That(pipe, Is.Not.Null); pipe.Clear();
            MatchDirector.TriggerEvent(RandomEventKind.Pressure); Assert.That(pipe.Accumulation, Is.EqualTo(40));
            MatchDirector.TriggerEvent(RandomEventKind.Plumber); Assert.That(pipe.Accumulation, Is.Zero);
            MatchDirector.TriggerEvent(RandomEventKind.Blackout); Assert.That(MatchDirector.BlackoutRemaining, Is.GreaterThan(9)); Assert.That(MatchContext.LastEventText, Is.EqualTo("停电 10 秒！"));
            var count = MatchContext.Schedule.Centers.Length; MatchDirector.TriggerEvent(RandomEventKind.StomachBug); Assert.That(MatchContext.Schedule.Centers.Length, Is.EqualTo(count + 1)); Assert.That(MatchContext.Schedule.Widths.Last(), Is.EqualTo(10));
            var vending = Object.FindFirstObjectByType<PaperVendingMachine>(); Assert.That(vending, Is.Not.Null); MatchDirector.TriggerEvent(RandomEventKind.Supply); Assert.That(vending.LastRestockCount, Is.EqualTo(2)); Assert.That(MatchDirector.EventSerial, Is.EqualTo(5));
            yield return null;
        }
    }
}
