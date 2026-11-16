using System.Collections;
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
}
