#if UNITY_EDITOR
using System.Collections;
using System.Diagnostics;
using System.Linq;
using NatureCalls.Facility;
using NatureCalls.Map;
using NatureCalls.Player;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace NatureCalls.Tests
{
    public sealed class Map3PlayModeTests
    {
        private const string ScenePath = "Assets/Scenes/Map3.unity";
        private Playground world;
        private Movement player;

        [UnitySetUp]
        public IEnumerator OpenMap3()
        {
            Match.End();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            world = Object.FindFirstObjectByType<Playground>();
            Assert.That(world, Is.Not.Null);
            world.Practice();
            yield return null;
            player = world.OfflinePlayer;
            Assert.That(player, Is.Not.Null);
            player.GetComponent<NatureCalls.Input.PlayerInput>().enabled = false;
            player.GetComponent<CharacterController>().enabled = false;
        }

        [UnityTest]
        public IEnumerator Map3HasFourGroundedSpawnsAndConfiguredFacilities()
        {
            Assert.That(world.Spawns, Has.Length.EqualTo(4));
            foreach (var spawn in world.Spawns)
            {
                Assert.That(Physics.Raycast(spawn.position + Vector3.up, Vector3.down, out var hit, 3f), Is.True, spawn.name);
                Assert.That(spawn.position.y, Is.EqualTo(hit.point.y).Within(.12f), spawn.name);
            }
            Assert.That(Object.FindObjectsByType<StallDoor>(FindObjectsSortMode.None), Has.Length.EqualTo(56));
            Assert.That(Object.FindObjectsByType<Toilet>(FindObjectsSortMode.None).Length, Is.GreaterThanOrEqualTo(40));
            Assert.That(Object.FindObjectsByType<Portal>(FindObjectsSortMode.None), Has.Length.EqualTo(2));
            Assert.That(Object.FindObjectsByType<PaperRoll>(FindObjectsSortMode.None).Length, Is.GreaterThan(20));
            yield return null;
        }

        [UnityTest]
        public IEnumerator PortalTeleportsPlayerAndCarriedPaperExactly()
        {
            var portal = Object.FindObjectsByType<Portal>(FindObjectsSortMode.None).First(p => p.CrossingDirectionX > 0);
            var roll = Object.FindObjectsByType<PaperRoll>(FindObjectsSortMode.None).First();
            player.transform.position = portal.transform.position + portal.transform.right * .2f;
            roll.ApplySharedPickup(player);
            var before = player.transform.position;
            var paperBefore = roll.transform.position;
            var collider = player.gameObject.GetComponent<CharacterController>();
            portal.SendMessage("OnTriggerEnter", collider, SendMessageOptions.RequireReceiver);
            Assert.That((player.transform.position - (before + portal.Offset)).sqrMagnitude, Is.LessThan(.0001f));
            Assert.That((roll.transform.position - (paperBefore + portal.Offset)).sqrMagnitude, Is.LessThan(.0001f));
            yield return null;
        }

        [UnityTest]
        public IEnumerator PortalOwnerUsesNetworkTransformTeleportBoundary()
        {
            Assert.That(player.GetComponent<NetworkTransform>(), Is.Not.Null);
            var portal = Object.FindFirstObjectByType<Portal>();
            player.transform.position = portal.transform.position + portal.transform.right * .2f;
            var before = player.transform.position;
            portal.SendMessage("OnTriggerEnter", player.GetComponent<CharacterController>(), SendMessageOptions.RequireReceiver);
            Assert.That((player.transform.position - (before + portal.Offset)).sqrMagnitude, Is.LessThan(.0001f));
            yield return null;
        }

        [UnityTest]
        public IEnumerator EngagedAndOutOfOrderDoorsRemainLocked()
        {
            foreach (var door in Object.FindObjectsByType<StallDoor>(FindObjectsSortMode.None).Where(d => d.name.Contains("Engaged") || d.name.Contains("OutOfOrder")))
                Assert.That(door.State, Is.EqualTo(StallDoorState.Locked), door.name);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ContainingOneFrameIsMeasuredForMap3()
        {
            var watch = Stopwatch.StartNew();
            StallDoor found = null;
            for (var i = 0; i < 100; i++) found = StallDoor.Containing(player);
            watch.Stop();
            UnityEngine.Debug.Log($"MAP3_STALL_CONTAINING_100_CALLS_MS={watch.Elapsed.TotalMilliseconds:F3}|LAST={(found != null ? found.name : "none")}");
            yield return null;
        }

    }
}
#endif
