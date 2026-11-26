using System.Collections;
using System.Linq;
using NatureCalls.Core;
using NatureCalls.Facility;
using NatureCalls.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using PlayerInput = NatureCalls.Input.PlayerInput;

/// <summary>Other fixtures predate StoolKind and expect a soft heap; they draw Soft unless a test forces another kind.</summary>
[SetUpFixture]
public sealed class StoolKindDefault
{
    [OneTimeSetUp] public void Force() { StoolOutput.Forced = StoolKind.Soft; PlayerInput.ReleaseScale = 0; } // settle the squat at once
    [OneTimeTearDown] public void Release() { StoolOutput.Forced = null; PlayerInput.ReleaseScale = 1f; }
}

namespace NatureCalls.Tests
{
    /// <summary>GLOSSARY Mess/StoolKind: the three floor forms, kicking, pickup cost, toilet load and loose inertia.</summary>
    public sealed class StoolTests
    {
        private Map.Playground world;
        private Movement player;
        private PlayerNeeds needs;

        [UnitySetUp] public IEnumerator Load()
        {
            yield return SceneManager.LoadSceneAsync("Assets/Scenes/Playground.unity", LoadSceneMode.Additive);
            yield return null;
            world = Object.FindFirstObjectByType<Map.Playground>(); player = world.OfflinePlayer;
            player.GetComponent<PlayerInput>().enabled = false;
            needs = player.GetComponent<PlayerNeeds>(); needs.BeginRound(0);
        }
        [UnityTearDown] public IEnumerator Unload()
        {
            StoolOutput.Forced = StoolKind.Soft;
            world.StopPractice();
            yield return SceneManager.UnloadSceneAsync("Playground");
        }

        private Vector3 Ahead(float metres) => player.transform.position + player.transform.forward * metres;

        [Test] public void MessArtCatalogIsComplete()
        {
            var art = MessArt.Current;
            Assert.That(art, Is.Not.Null, "Run scripts/unity.py mess-art.");
            Assert.That(new[] { art.SoftSwirl, art.SoftSquashed, art.HardLog, art.HardPellets, art.LooseSplat, art.Vomit, art.UrinePuddle, art.ToiletSmear }, Has.None.Null);
            Assert.That(new[] { art.Stool, art.StoolDry, art.Loose, art.Urine, art.Stink, art.Sparkle, art.Splash }, Has.None.Null);
        }

        [Test] public void EachKindLandsInItsOwnForm()
        {
            int before = world.Messes.Count(m => m != null);
            StoolOutput.Floor(player, Ahead(1), StoolKind.Soft);
            var soft = world.Messes.Last();
            Assert.That(soft.Kind, Is.EqualTo(MessKind.Dropping));
            var look = soft.GetComponent<MessLook>();
            var mesh = look.Look.GetComponent<MeshFilter>().sharedMesh;
            Assert.That(mesh == MessArt.Current.SoftSwirl || mesh == MessArt.Current.SoftSquashed, Is.True, "A soft heap.");
            Assert.That(look.Odor, Is.Not.Null); Assert.That(look.OdorWeight, Is.EqualTo(StoolRules.Odor(StoolKind.Soft)));

            StoolOutput.Floor(player, Ahead(2), StoolKind.Loose);
            var loose = world.Messes.Last();
            Assert.That(loose.Kind, Is.EqualTo(MessKind.LooseStool));
            Assert.That(loose.GetComponent<MessLook>().Look.GetComponent<MeshFilter>().sharedMesh, Is.SameAs(MessArt.Current.LooseSplat));
            Assert.That(loose.GetComponent<MessLook>().OdorWeight, Is.LessThan(look.OdorWeight), "Soft smells strongest.");

            StoolOutput.Floor(player, Ahead(3), StoolKind.Hard);
            Assert.That(world.Messes.Count(m => m != null), Is.EqualTo(before + 2), "A hard stool is an item, not a mess entry.");
            var hard = StoolPool.Stools.Single(stool => stool.InPlay);
            Assert.That(Vector3.Distance(hard.transform.position, Ahead(3)), Is.LessThan(.1f));
            Assert.That(hard.DirtWeight, Is.EqualTo(StoolRules.DirtWeight(StoolKind.Hard)));
            Assert.That(DirtField.Sources, Has.Member(hard));
        }

        [Test] public void OutsideToiletingUsesTheDrawnKind()
        {
            StoolOutput.Forced = StoolKind.Hard;
            needs.BeginRound(0);
            PlayerInput.ApplyResult(player, null, null, 1, Ahead(1), -1);
            Assert.That(StoolPool.Stools.Count(stool => stool.InPlay), Is.EqualTo(1));
            StoolOutput.Forced = StoolKind.Loose;
            PlayerInput.ApplyResult(player, null, null, 1, Ahead(2), -1);
            Assert.That(world.Messes.Last().Kind, Is.EqualTo(MessKind.LooseStool));
        }

        [UnityTest] public IEnumerator LooseStoolIsSlipperyLikeAWetZone()
        {
            var before = MovementEffectRegistry.Combine(player);
            StoolOutput.Floor(player, player.transform.position, StoolKind.Loose);
            yield return null;
            var on = MovementEffectRegistry.Combine(player);
            Assert.That(on.Acceleration, Is.EqualTo(StoolRules.LooseInertia));
            Assert.That(before.Acceleration, Is.Not.EqualTo(StoolRules.LooseInertia).Or.EqualTo(0));
            world.RemoveMess(world.Messes.Count - 1);
            yield return null;
            Assert.That(MovementEffectRegistry.Combine(player).Acceleration, Is.EqualTo(before.Acceleration), "Cleaning removes the slip.");
        }

        [UnityTest] public IEnumerator WalkingIntoAHardStoolKicksIt()
        {
            Assert.That(HardStool.Spawn(Ahead(.9f), player), Is.True);
            var stool = StoolPool.Stools.Single(s => s.InPlay);
            var start = stool.transform.position;
            bool kicked = false; stool.Kicked += _ => kicked = true;
            for (float t = 0; t < 3 && !kicked; t += Time.deltaTime)
            {
                player.Move(Vector2.up, Time.deltaTime);
                yield return null;
            }
            Assert.That(kicked, Is.True, "Walking into it kicks it.");
            Assert.That(stool.GetComponent<Rigidbody>().isKinematic, Is.False);
            Assert.That(Vector3.Dot(stool.GetComponent<Rigidbody>().linearVelocity, player.transform.forward), Is.GreaterThan(1), "Kicked forward.");
            yield return new WaitForSeconds(.25f);
            Assert.That(Vector3.ProjectOnPlane(stool.transform.position - start, Vector3.up).magnitude, Is.GreaterThan(.2f), "It flew.");
        }

        [UnityTest] public IEnumerator PickingUpCostsComposureAndItThrowsLikeAnItem()
        {
            Assert.That(HardStool.Spawn(Ahead(.5f), player), Is.True);
            var stool = StoolPool.Stools.Single(s => s.InPlay);
            float before = needs.Disgust;
            Assert.That(stool.Pickup(player), Is.True);
            yield return null;
            Assert.That(needs.Disgust - before, Is.EqualTo(StoolRules.PickupComposureCost), "Picking it up is disgusting.");
            Assert.That(Item.FindCarriedBy(player), Is.SameAs(stool));
            Assert.That(stool.Throw(player, player.transform.forward), Is.True);
            Assert.That(stool.IsPickedUp, Is.False);
            yield return null;
            Assert.That(needs.Disgust - before, Is.EqualTo(StoolRules.PickupComposureCost), "Charged once.");
        }

        [UnityTest] public IEnumerator HardStoolDroppedIntoAToiletLoadsItsPipe()
        {
            var toilet = Object.FindObjectsByType<Toilet>(FindObjectsSortMode.None).First(t => t.ConnectedPipe != null);
            int load = toilet.ConnectedPipe.Accumulation;
            Assert.That(HardStool.Spawn(Ahead(.5f), player), Is.True);
            var stool = StoolPool.Stools.Single(s => s.InPlay);
            bool flushed = false; stool.Flushed += _ => flushed = true;
            stool.ApplySharedThrow(toilet.TargetCenter + Vector3.up * .15f, Quaternion.identity, Vector3.down * .5f, true);
            for (int i = 0; i < 30 && !flushed; i++) yield return null;
            Assert.That(flushed, Is.True);
            Assert.That(stool.Stored, Is.True, "Back in the pool.");
            Assert.That(toilet.ConnectedPipe.Accumulation - load, Is.EqualTo(StoolRules.PipeLoad(StoolKind.Hard)));
        }

        [Test] public void LooseStoolIntoABlockedToiletBackflows()
        {
            var toilet = Object.FindObjectsByType<Toilet>(FindObjectsSortMode.None).First(t => t.ConnectedPipe != null);
            var pipe = toilet.ConnectedPipe;
            StoolOutput.Bowl(toilet, StoolKind.Soft);
            Assert.That(toilet.IsBackflow, Is.False);
            pipe.AddLoad(PlumbingRules.BlockedThreshold);
            StoolOutput.Bowl(toilet, StoolKind.Loose);
            Assert.That(toilet.IsBackflow, Is.True, "Loose stool acts like an over-flush.");
            Assert.That(world.Messes.Last().Kind, Is.EqualTo(MessKind.Backflow));
        }
    }
}
