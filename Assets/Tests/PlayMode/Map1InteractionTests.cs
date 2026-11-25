#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NatureCalls.Core;
using NatureCalls.Facility;
using NatureCalls.Map;
using NatureCalls.Player;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using PlayerInput = NatureCalls.Input.PlayerInput;

namespace NatureCalls.Tests
{
    public sealed class Map1InteractionTests
    {
        private const string ScenePath = "Assets/Scenes/Map1.unity";
        private Playground world;
        private Movement player;
        private PlayerInput input;

        [UnitySetUp]
        public IEnumerator OpenMap()
        {
            Match.End();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            world = Object.FindFirstObjectByType<Playground>();
            Assert.That(world, Is.Not.Null);
            player = world.OfflinePlayer;
            Assert.That(player, Is.Not.Null);
            input = player.GetComponent<PlayerInput>();
            Assert.That(input.enabled, Is.True);
            Assert.That(Match.Mode, Is.EqualTo(MatchMode.Offline));
            Assert.That(Match.OfflineRemaining, Is.GreaterThan(0));
            Assert.That(world.Camera.Target, Is.SameAs(player));
            Assert.That(Object.FindObjectsByType<UnityEngine.Camera>(FindObjectsSortMode.None)
                .Count(camera => camera.enabled && camera.targetTexture == null), Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator DoorsAllowStandingPassageAndOwnToilet()
        {
            var controller = player.GetComponent<CharacterController>();
            var doors = Object.FindObjectsByType<StallDoor>(FindObjectsSortMode.None);
            Assert.That(doors, Has.Length.EqualTo(3));
            foreach (var door in doors)
            {
                var blocker = door.DoorColliderPivot.GetComponent<BoxCollider>();
                var center = blocker.bounds.center;
                center.y = .015f;
                var outside = center - door.transform.forward * .55f;
                Place(outside, door.transform.forward);
                Assert.That(input.TryInteract(), Is.True, door.transform.parent.name);
                Assert.That(door.State, Is.EqualTo(StallDoorState.Open));
                controller.Move(door.transform.forward * .95f);
                var inside = player.transform.position;
                Assert.That(Vector3.Dot(inside - outside, door.transform.forward), Is.GreaterThan(.75f),
                    door.transform.parent.name + " blocks the standing player");
                Assert.That(StallDoor.Containing(player), Is.SameAs(door));
                Assert.That(door.Toilet, Is.Not.Null);
                Place(new Vector3(inside.x, -2.58f, inside.z), door.transform.forward);
                Assert.That(StallDoor.Containing(player), Is.Null, "A player on another floor cannot be inside an upstairs stall.");
                Place(inside, door.transform.forward);
                Assert.That(input.TryInteract(), Is.True);
                Assert.That(door.State, Is.EqualTo(StallDoorState.Closed));
                Assert.That(input.TryInteract(), Is.True);
                Assert.That(door.State, Is.EqualTo(StallDoorState.Locked));
                Place(outside, door.transform.forward);
                input.TryInteract();
                Assert.That(door.State, Is.EqualTo(StallDoorState.Locked));
                Place(inside, door.transform.forward);
                Assert.That(input.TryInteract(), Is.True);
                Assert.That(door.State, Is.EqualTo(StallDoorState.Open));
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator BothDuctsAllowTravelInBothDirectionsAndRestoreStanding()
        {
            var passages = Object.FindObjectsByType<VentPassage>(FindObjectsSortMode.None);
            Assert.That(passages, Has.Length.EqualTo(2));
            var controller = player.GetComponent<CharacterController>();
            foreach (var passage in passages)
            {
                var route = passage.transform;
                var heading = route.right;
                Place(route.TransformPoint(new Vector3(4, .015f, 0)), heading);
                player.Move(Vector2.zero, .02f);
                Assert.That(player.IsCrawling, Is.False, "The upper floor above a duct remains standing height.");

                Travel(route, true);
                Assert.That(player.transform.position.y, Is.EqualTo(passage.Exit.position.y).Within(.14f));
                Assert.That(controller.height, Is.EqualTo(player.StandingHeight).Within(.001f));

                Travel(route, false);
                Assert.That(player.transform.position.y, Is.EqualTo(route.position.y).Within(.14f));
                Assert.That(controller.height, Is.EqualTo(player.StandingHeight).Within(.001f));
            }
            yield return null;
        }

        private void Travel(Transform route, bool downward)
        {
            var start = downward ? new Vector3(-1.05f, .015f, 0) : new Vector3(8.58f, -2.585f, 0);
            Place(route.TransformPoint(start), downward ? route.right : -route.right);
            bool crawled = false;
            var capsule = player.GetComponent<CharacterController>();
            for (int step = 0; step < 330; step++)
            {
                player.Move(Vector2.up, .02f);
                float along = route.InverseTransformPoint(player.transform.position).x;
                if (along > .23f && along < 7.30f)
                {
                    Assert.That(player.IsCrawling, Is.True, "The player must remain low inside the duct.");
                    Assert.That(capsule.height, Is.EqualTo(.85f).Within(.001f));
                    if (!crawled)
                    {
                        Assert.That(player.Jump(), Is.False);
                        Assert.That(input.TryToilet(), Is.False);
                        crawled = true;
                    }
                }
                if (crawled && !player.IsCrawling && (downward ? along > 8.10f : along < -.66f))
                    return;
            }
            Assert.Fail($"{route.name} blocked walking {(downward ? "down" : "up")}: " +
                $"{route.InverseTransformPoint(player.transform.position):F3}, crawling={player.IsCrawling}");
        }

        [UnityTest]
        public IEnumerator PaperToiletUrinalAndMopUseExistingSettlement()
        {
            var rolls = Object.FindObjectsByType<PaperRoll>(FindObjectsSortMode.None);
            Assert.That(rolls, Has.Length.EqualTo(3));
            var backup = rolls.Single(roll => roll.name == "Backup Roll");
            var supply = backup.transform.position;
            Place(new Vector3(supply.x, .015f, supply.z - .55f), Vector3.forward);
            Assert.That(input.TryInteract(), Is.True);
            Assert.That(input.HeldItem, Is.SameAs(backup));

            var door = Object.FindObjectsByType<StallDoor>(FindObjectsSortMode.None)
                .Single(stall => stall.transform.parent.name == "Stall A1");
            var toilet = door.Toilet;
            var standing = toilet.TargetCenter - door.transform.forward * NeedsRules.ButtOffset;
            standing.y = .015f;
            Place(standing, -door.transform.forward);
            Assert.That(input.TryToilet(), Is.True);
            Assert.That(input.Phase, Is.EqualTo(PlayerInput.Stage.Aligning));
            Assert.That(input.TryToilet(), Is.True);
            Assert.That(toilet.Occupant, Is.SameAs(player));
            input.TickAim(NeedsRules.ToiletingSeconds, Vector2.zero);
            Assert.That(toilet.IsOccupied, Is.False);
            Assert.That(input.Unwiped, Is.True);
            Assert.That(input.TryAction(), Is.True, "F wipes with the held paper roll in the unified interaction flow.");
            Assert.That(input.Unwiped, Is.False);
            Assert.That(backup.RemainingUses, Is.EqualTo(NeedsRules.PaperUses - 1));
            Place(world.Spawns[0].position, Vector3.forward);
            Assert.That(backup.Throw(player, Vector3.forward), Is.True);
            Assert.That(backup.GetComponent<Rigidbody>().isKinematic, Is.False);
            Assert.That(input.HeldItem, Is.Null);

            var urinals = Object.FindObjectsByType<Urinal>(FindObjectsSortMode.None);
            Assert.That(urinals, Has.Length.EqualTo(2));
            foreach (var urinal in urinals)
            {
                Place(urinal.FrontPosition, urinal.TargetCenter - urinal.FrontPosition);
                Assert.That(input.TryToilet(), Is.True);
                Assert.That(input.Phase, Is.EqualTo(PlayerInput.Stage.Urinating));
                Assert.That(urinal.Occupant, Is.SameAs(player));
                input.TickAim(NeedsRules.UrinatingSeconds, new Vector2(2, 2));
                Assert.That(urinal.IsOccupied, Is.False);
            }
            Assert.That(world.Messes.Count(mess => mess != null && mess.Kind == MessKind.Puddle), Is.EqualTo(2));

            var box = Object.FindFirstObjectByType<Toolbox>();
            Assert.That(box.Mop.Stored, Is.True);
            Place(box.transform.position + Vector3.back * .55f, Vector3.forward);
            Assert.That(input.TryInteract(), Is.True);
            Assert.That(input.HeldItem, Is.SameAs(box.Mop));
            Assert.That(rolls.Any(roll => roll.CanPickup(player)), Is.False);
            var puddle = world.Messes.First(mess => mess != null && mess.Kind == MessKind.Puddle);
            Place(new Vector3(puddle.transform.position.x, .015f, puddle.transform.position.z), Vector3.forward);
            Assert.That(input.TryAction(), Is.True, "F starts cleaning while holding the mop.");
            input.ContinueCleaning(false, .1f);
            Assert.That(world.Messes[puddle.Index], Is.SameAs(puddle));
            Assert.That(input.BeginCleaning(puddle), Is.True, "F resumes cleaning the selected puddle while holding the mop.");
            input.ContinueCleaning(true, NeedsRules.CleaningSeconds);
            Assert.That(world.Messes[puddle.Index], Is.Null);
            Place(box.transform.position + Vector3.back * .55f, Vector3.forward);
            Assert.That(input.TryInteract(), Is.True);
            Assert.That(box.Mop.Stored, Is.True);
            yield return null;
        }

        private void Place(Vector3 position, Vector3 direction)
        {
            var controller = player.GetComponent<CharacterController>();
            controller.enabled = false;
            player.transform.SetPositionAndRotation(position,
                Quaternion.LookRotation(Vector3.ProjectOnPlane(direction, Vector3.up)));
            controller.enabled = true;
            Physics.SyncTransforms();
        }

        [UnityTearDown]
        public IEnumerator UnloadMap()
        {
            Match.End();
            var scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.isLoaded) yield break;
            SceneManager.SetActiveScene(SceneManager.CreateScene("Map1TestEmpty"));
            yield return SceneManager.UnloadSceneAsync(scene);
        }
    }
}
#endif
