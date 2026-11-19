using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NatureCalls.Facility;
using NatureCalls.Map;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace NatureCalls.Tests
{
    public sealed class MapLayoutTests
    {
        [UnityTest]
        public IEnumerator LayoutHasSafeSpawnsAndWorkingDoorsToiletsAndSupplies()
        {
            Match.End();
            yield return SceneManager.LoadSceneAsync("Assets/Scenes/MapLayoutPrototype.unity", LoadSceneMode.Single);
            yield return null;
            var world = Object.FindFirstObjectByType<Playground>();
            var player = world.OfflinePlayer;
            Assert.That(player, Is.Not.Null);
            player.GetComponent<NatureCalls.Input.PlayerInput>().enabled = false;
            player.GetComponent<CharacterController>().enabled = false;
            Assert.That(world.Spawns.Length, Is.EqualTo(4));
            Physics.SyncTransforms();
            foreach (var spawn in world.Spawns)
            {
                Assert.That(Physics.Raycast(spawn.position + Vector3.up * .5f, Vector3.down, 1, 1), Is.True);
                Assert.That(Physics.CheckCapsule(spawn.position + Vector3.up * .4f,
                    spawn.position + Vector3.up * 1.65f, .35f, 1, QueryTriggerInteraction.Ignore), Is.False, spawn.name);
            }
            var doors = Object.FindObjectsByType<StallDoor>(FindObjectsSortMode.None);
            Assert.That(doors.Length, Is.EqualTo(3));
            foreach (var door in doors)
            {
                player.transform.position = door.DoorColliderPivot.position - door.transform.forward * .35f;
                Assert.That(door.Interact(player), Is.True);
                Assert.That(door.State, Is.EqualTo(StallDoorState.Open));
                player.transform.position = door.transform.TransformPoint(new Vector3(0, .15f, 0));
                Assert.That(StallDoor.Containing(player), Is.SameAs(door));
                Assert.That(door.Toilet, Is.Not.Null);
                Assert.That(door.Toilet.Anchor, Is.Not.Null);
                Assert.That(door.Toilet.Interior, Is.Not.Null);
                Assert.That(door.Toilet.Seat(player), Is.True);
                Assert.That(door.Toilet.IsOccupied, Is.True);
                player.GetComponent<NatureCalls.Player.Posture>().Set(NatureCalls.Player.PostureState.Standing);
                door.Toilet.Release(player);
            }
            var rolls = Object.FindObjectsByType<PaperRoll>(FindObjectsSortMode.None);
            Assert.That(rolls.Length, Is.GreaterThanOrEqualTo(3));
            var roll = rolls.First();
            player.transform.position = roll.transform.position + Vector3.right * .3f;
            Assert.That(roll.Pickup(player), Is.True);
            Assert.That(roll.Throw(player, Vector3.forward), Is.True);
            var boxes = Object.FindObjectsByType<Toolbox>(FindObjectsSortMode.None);
            Assert.That(boxes.Length, Is.EqualTo(1));
            Assert.That(Object.FindObjectsByType<Mop>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
            player.transform.position = boxes[0].transform.position;
            Assert.That(boxes[0].Interact(player), Is.True);
            Assert.That(boxes[0].Mop.IsPickedUp, Is.True);
            Assert.That(boxes[0].Interact(player), Is.True);
            Assert.That(boxes[0].Mop.Stored, Is.True);
        }

        [UnityTest]
        public IEnumerator LayoutPlayerInteractSelectsNearbyDoorAndPaperRoll()
        {
            Match.End();
            yield return SceneManager.LoadSceneAsync("Assets/Scenes/MapLayoutPrototype.unity", LoadSceneMode.Single);
            yield return null;

            var world = Object.FindFirstObjectByType<Playground>();
            var player = world.OfflinePlayer;
            var input = player.GetComponent<NatureCalls.Input.PlayerInput>();
            var door = Object.FindObjectsByType<StallDoor>(FindObjectsSortMode.None).First();
            player.transform.position = door.DoorColliderPivot.position - door.transform.forward * .35f;
            var panel = door.DoorLeaf.Find("Interactive door panel");
            Assert.That(panel, Is.Not.Null, "The map door needs a visible panel below its rotating hinge.");
            var closedForward = panel.forward;
            var closedDoorCenter = panel.position;
            Assert.That(input.TryInteract(), Is.True, "E should select the nearby stall door.");
            Assert.That(door.State, Is.EqualTo(StallDoorState.Open));
            Assert.That(Vector3.Angle(closedForward, panel.forward), Is.GreaterThan(45f), "Opening a door must visibly rotate its panel.");
            Assert.That(door.DoorColliderPivot.GetComponent<Collider>().enabled, Is.False,
                "The opened panel must not retain a blocking collider in the narrow doorway.");

            var controller = player.GetComponent<CharacterController>();
            var throughDirection = door.transform.forward;
            // DoorColliderPivot is intentionally at the hinge (the left edge), not at the middle of the doorway.
            // A passage test must begin at the closed panel's horizontal center.
            var outside = new Vector3(closedDoorCenter.x, door.transform.position.y, closedDoorCenter.z) - throughDirection * 1f;
            Assert.That(Physics.Raycast(outside + Vector3.up * 3f, Vector3.down, out var floor, 4f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore), Is.True, "The doorway exterior needs a floor.");
            outside.y = floor.point.y + .01f;
            controller.enabled = false;
            player.transform.position = outside;
            Physics.SyncTransforms();
            var bottom = outside + Vector3.up * (controller.radius + .01f);
            var top = outside + Vector3.up * (controller.height - controller.radius - .01f);
            var blockers = Physics.CapsuleCastAll(bottom, top, controller.radius, throughDirection, 1.25f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                .Select(hit => hit.collider.name + "<" + (hit.collider.transform.parent != null ? hit.collider.transform.parent.name : "root") +
                    "<" + (hit.collider.transform.parent != null && hit.collider.transform.parent.parent != null ? hit.collider.transform.parent.parent.name : "root") +
                    "@" + hit.distance.ToString("F2"));
            controller.enabled = true;
            var hitRecorder = player.gameObject.AddComponent<ControllerHitRecorder>();
            var flags = controller.Move(throughDirection * 1.25f);
            Assert.That(Vector3.Dot(player.transform.position - outside, throughDirection), Is.GreaterThan(1f),
                "A standing player must be able to pass through an opened stall doorway. Blockers: " + string.Join(", ", blockers) +
                "; Controller flags: " + flags + "; Player top: " + (player.transform.position.y + controller.height).ToString("F2") +
                "; Controller hits: " + string.Join(", ", hitRecorder.Hits));

            Assert.That(StallDoor.Containing(player), Is.SameAs(door), "The player should now be inside the stall.");
            Assert.That(input.TryInteract(), Is.True, "E should select and close the player's own door from inside the stall.");
            Assert.That(door.State, Is.EqualTo(StallDoorState.Closed));

            var roll = Object.FindObjectsByType<PaperRoll>(FindObjectsSortMode.None).First();
            player.transform.position = roll.transform.position + Vector3.right * .3f;
            Assert.That(input.TryInteract(), Is.True, "E should select the nearby paper roll.");
            Assert.That(roll.IsPickedUp, Is.True);
        }

        [UnityTearDown]
        public IEnumerator Unload()
        {
            Match.End();
            var scene = SceneManager.GetSceneByPath("Assets/Scenes/MapLayoutPrototype.unity");
            if (scene.isLoaded)
            {
                SceneManager.SetActiveScene(SceneManager.CreateScene("MapLayoutTestEmpty"));
                yield return SceneManager.UnloadSceneAsync(scene);
            }
        }
    }

    internal sealed class ControllerHitRecorder : MonoBehaviour
    {
        public readonly List<string> Hits = new();
        private void OnControllerColliderHit(ControllerColliderHit hit)
        {
            var parent = hit.collider.transform.parent;
            Hits.Add(hit.collider.name + "<" + (parent != null ? parent.name : "root") +
                "@y=" + hit.collider.bounds.min.y.ToString("F2") + "-" + hit.collider.bounds.max.y.ToString("F2"));
        }
    }
}
