using System.Collections;
using NatureCalls.Facility;
using NatureCalls.Player;
using NatureCalls.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace NatureCalls.Tests
{
    /// <summary>The block-game crosshair: shown in standing first person, highlights reachable targets, hides when squatting.</summary>
    public sealed class CrosshairTests
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

        [UnityTest] public IEnumerator CrosshairShowsInFirstPersonAndHighlightsReachableTargets()
        {
            var crosshair = Object.FindFirstObjectByType<Crosshair>();
            Assert.That(crosshair, Is.Not.Null, "The crosshair installs itself on maps with a Playground.");
            var player = world.OfflinePlayer;
            var input = player.GetComponent<NatureCalls.Input.PlayerInput>();
            input.CaptureMouse = false; // batch runs cannot lock the cursor
            yield return null; yield return null;
            Assert.That(crosshair.Visible, Is.True, "Standing first person shows the crosshair.");

            var roll = Object.FindFirstObjectByType<PaperRoll>();
            Assert.That(roll, Is.Not.Null);
            var controller = player.GetComponent<CharacterController>();
            controller.enabled = false;
            var flat = Vector3.ProjectOnPlane(roll.transform.position - player.transform.position, Vector3.up).normalized;
            if (flat.sqrMagnitude < .01f) flat = Vector3.forward;
            player.transform.position = new Vector3(roll.transform.position.x, player.transform.position.y, roll.transform.position.z) - flat * .9f;
            controller.enabled = true; Physics.SyncTransforms();
            player.LookAt(roll.transform.position);
            yield return null; yield return null;
            input.RefreshFocus();
            yield return null;
            Assert.That(input.Focus, Is.SameAs(roll), "Looking straight at a nearby roll selects it.");
            Assert.That(crosshair.OnTarget, Is.True, "The crosshair highlights a usable target.");

            player.LookAt(roll.transform.position + Vector3.up * 3f + player.transform.right * 3f);
            yield return null; input.RefreshFocus(); yield return null;
            Assert.That(input.Focus, Is.Not.SameAs(roll), "Looking well away no longer selects the roll.");

            // Regression: aiming at the door away from its centre still opens it (the blocker collider counts).
            var door = Object.FindFirstObjectByType<StallDoor>();
            var blocker = door.DoorColliderPivot.GetComponent<Collider>();
            controller.enabled = false;
            var front = blocker.bounds.center - door.transform.forward * 1.0f; front.y = player.transform.position.y;
            player.transform.position = front; controller.enabled = true; Physics.SyncTransforms();
            var edge = blocker.bounds.center + door.transform.right * .22f + Vector3.up * .3f; // well off the centre, still on the panel
            player.LookAt(edge);
            yield return null; input.RefreshFocus();
            Assert.That(input.Focus, Is.SameAs(door), "Looking at the door off-centre still selects it.");
            yield return null; input.RefreshFocus(); yield return null;
            Assert.That(crosshair.OnTarget, Is.True);

            player.GetComponent<Posture>().Set(PostureState.Squatting);
            yield return null; yield return null;
            Assert.That(crosshair.Visible, Is.False, "No crosshair while squatting: toileting aim is by feel.");
            player.GetComponent<Posture>().Set(PostureState.Standing);
        }
    }
}
