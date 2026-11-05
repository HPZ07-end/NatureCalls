using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using NatureCalls.Player;
using NatureCalls.Facility;

namespace NatureCalls.Tests
{
    public sealed class PlaygroundTests
    {
        private Scene scene;
        private Map.Playground world;
        [UnitySetUp]
        public IEnumerator Load()
        {
            yield return SceneManager.LoadSceneAsync("Assets/Scenes/Playground.unity", LoadSceneMode.Additive);
            scene = SceneManager.GetSceneByPath("Assets/Scenes/Playground.unity");
            foreach (var root in scene.GetRootGameObjects())
                if (root.TryGetComponent(out Map.Playground found)) world = found;
            yield return null;
            Assert.That(world, Is.Not.Null);
            Assert.That(world.OfflinePlayer, Is.Not.Null);
            world.OfflinePlayer.GetComponent<NatureCalls.Input.PlayerInput>().enabled = false;
            yield return new WaitForSeconds(.2f);
        }
        [UnityTearDown]
        public IEnumerator Unload()
        {
            if (world != null) world.StopPractice();
            var manager = Unity.Netcode.NetworkManager.Singleton;
            if (manager != null)
            {
                if (manager.IsListening) manager.Shutdown();
                Object.Destroy(manager.gameObject);
                yield return null;
            }
            if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
        }
        [Test]
        public void SpawnPointsStandOnOpenFloorInsideTheRoom()
        {
            // A spawn inside a wall pushes the player's capsule below the floor and it falls forever.
            var stalls = Object.FindObjectsByType<StallDoor>(FindObjectsSortMode.None)
                .Select(door => door.GetComponentsInChildren<Renderer>(true).Select(renderer => renderer.bounds)
                    .Aggregate((all, next) => { all.Encapsulate(next); return all; })).ToArray();
            var host = world.Spawns[0].position + Vector3.up;
            foreach (var spawn in world.Spawns)
            {
                var point = spawn.position;
                Assert.That(Physics.Raycast(point + Vector3.up * .5f, Vector3.down, out var floor, 1f, 1, QueryTriggerInteraction.Ignore), Is.True, spawn.name + " has floor below.");
                Assert.That(floor.point.y, Is.EqualTo(point.y).Within(.1f), spawn.name + " stands on the floor.");
                var hits = Physics.OverlapCapsule(point + Vector3.up * .37f, point + Vector3.up * 1.73f, .32f, 1, QueryTriggerInteraction.Ignore)
                    .Where(hit => hit.GetComponentInParent<Movement>() == null);
                Assert.That(hits.Select(hit => hit.name), Is.Empty, spawn.name + " keeps a standing player clear of colliders.");
                Assert.That(stalls.Any(bounds => bounds.Contains(new Vector3(point.x, bounds.center.y, point.z))), Is.False, spawn.name + " is outside the stalls.");
                Assert.That(Physics.Linecast(host, point + Vector3.up, 1, QueryTriggerInteraction.Ignore), Is.False, spawn.name + " is in the same room as Spawn 1.");
            }
        }
        [UnityTest]
        public IEnumerator LocalHostOwnsOneCameraAndLeavesCleanly()
        {
            var session = Object.FindFirstObjectByType<NatureCalls.Network.Session>();
            Assert.That(session.StartDirect(true), Is.True);
            float deadline = Time.time + 5;
            while (session.Manager.LocalClient.PlayerObject == null && Time.time < deadline) yield return null;
            var avatar = session.Manager.LocalClient.PlayerObject;
            Assert.That(avatar, Is.Not.Null);
            yield return null;
            Assert.That(world.OfflinePlayer, Is.Null);
            Assert.That(world.Camera.Target, Is.EqualTo(avatar.GetComponent<Movement>()));
            Assert.That(ScreenCameras(), Is.EqualTo(1), "Mirror cameras render to textures; only one camera renders to the screen.");
            avatar.GetComponent<Posture>().Set(PostureState.Squatting);
            yield return null;
            Assert.That(avatar.GetComponent<NatureCalls.Network.Avatar>().SharedPosture.Value, Is.EqualTo(PostureState.Squatting));
            var leave = session.LeaveAsync();
            while (!leave.IsCompleted) yield return null;
            yield return null;
            yield return null;
            Assert.That(session.Manager.IsListening, Is.False);
            Assert.That(world.Camera.Target, Is.Null);
            Assert.That(Object.FindObjectsByType<NatureCalls.Network.Avatar>(FindObjectsSortMode.None), Is.Empty);
        }

        [UnityTest]
        public IEnumerator HostPublishesDoorAndPaperRollInteractions()
        {
            var session = Object.FindFirstObjectByType<NatureCalls.Network.Session>();
            Assert.That(session.StartDirect(true), Is.True);
            float deadline = Time.time + 5;
            while (session.Manager.LocalClient.PlayerObject == null && Time.time < deadline) yield return null;
            var playerObject = session.Manager.LocalClient.PlayerObject;
            Assert.That(playerObject, Is.Not.Null);
            var avatar = playerObject.GetComponent<NatureCalls.Network.Avatar>();
            var player = playerObject.GetComponent<Movement>();
            var controller = playerObject.GetComponent<CharacterController>();
            var doors = Object.FindObjectsByType<StallDoor>(FindObjectsSortMode.None);
            var rolls = Object.FindObjectsByType<PaperRoll>(FindObjectsSortMode.None);
            Assert.That(doors.Length, Is.EqualTo(3));
            Assert.That(rolls.Length, Is.EqualTo(3));
            Assert.That(avatar.SharedDoors.Count, Is.EqualTo(doors.Length));
            Assert.That(avatar.SharedPaperRolls.Count, Is.EqualTo(rolls.Length));

            controller.enabled = false;
            player.transform.position = doors[0].DoorColliderPivot.position + doors[0].transform.forward * .4f;
            controller.enabled = true;
            Assert.That(avatar.RequestDoor(doors[0]), Is.True);
            deadline = Time.time + 2;
            while (doors[0].State == StallDoorState.Closed && Time.time < deadline) yield return null;
            Assert.That(doors[0].State, Is.Not.EqualTo(StallDoorState.Closed));
            Assert.That(Enumerable.Range(0, avatar.SharedDoors.Count)
                .Any(index => avatar.SharedDoors[index] == (byte)doors[0].State), Is.True);

            controller.enabled = false;
            player.transform.position = rolls[0].transform.position + Vector3.back * .4f;
            controller.enabled = true;
            Assert.That(avatar.RequestPickup(rolls[0]), Is.True);
            deadline = Time.time + 2;
            while (!rolls[0].IsPickedUp && Time.time < deadline) yield return null;
            Assert.That(rolls[0].PickedUpBy, Is.EqualTo(player));
            Assert.That(Enumerable.Range(0, avatar.SharedPaperRolls.Count)
                .Any(index => avatar.SharedPaperRolls[index].Mode == 1 && avatar.SharedPaperRolls[index].Carrier == avatar.OwnerClientId), Is.True);
            Assert.That(rolls[0].GetComponentsInChildren<Renderer>(true).All(renderer => !renderer.enabled), Is.True);

            Assert.That(avatar.RequestThrow(rolls[0], player.transform.forward), Is.True);
            deadline = Time.time + 2;
            while (rolls[0].IsPickedUp && Time.time < deadline) yield return null;
            Assert.That(rolls[0].IsPickedUp, Is.False);
            // The thrower's machine simulates the flight and reports it; the roll is handed back once it rests.
            var thrown = Enumerable.Range(0, avatar.SharedPaperRolls.Count).First(i => avatar.SharedPaperRolls[i].Mode == NatureCalls.Network.PaperRollSnapshot.Flying);
            Assert.That(avatar.SharedPaperRolls[thrown].Simulator, Is.EqualTo(avatar.OwnerClientId + 1), "The thrower simulates its own throw.");
            Assert.That(rolls.Any(roll => !roll.IsPickedUp && !roll.GetComponent<Rigidbody>().isKinematic), Is.True, "The thrown roll is simulated here, not followed.");
            deadline = Time.time + 6;
            while (avatar.SharedPaperRolls[thrown].Mode == NatureCalls.Network.PaperRollSnapshot.Flying && Time.time < deadline) yield return null;
            Assert.That(avatar.SharedPaperRolls[thrown].Mode, Is.EqualTo(NatureCalls.Network.PaperRollSnapshot.Landed), "Once at rest the roll is handed back.");
            Assert.That(avatar.SharedPaperRolls[thrown].Simulator, Is.Zero);
        }

        [UnityTest]
        public IEnumerator RelayFailurePreservesOfflinePractice()
        {
            var session = Object.FindFirstObjectByType<NatureCalls.Network.Session>();
            var player = world.OfflinePlayer;
            var join = session.ConnectRelayAsync(false, "");
            while (!join.IsCompleted) yield return null;
            Assert.That(session.Status, Does.Contain("Enter a Session join code"));
            Assert.That(session.Busy, Is.False);
            Assert.That(session.Active, Is.False);
            Assert.That(world.Camera.Target, Is.EqualTo(player));
            if (!System.Guid.TryParse(Application.cloudProjectId, out _))
            {
                var host = session.ConnectRelayAsync(true);
                while (!host.IsCompleted) yield return null;
                Assert.That(session.Status, Does.Contain("Unity Cloud is not linked"));
                Assert.That(session.Busy, Is.False);
                Assert.That(session.Active, Is.False);
                Assert.That(world.OfflinePlayer, Is.EqualTo(player));
                Assert.That(world.Camera.Target, Is.EqualTo(player));
            }
        }
        [UnityTest]
        public IEnumerator MovementStopsAtWallAndSquattingStopsMovement()
        {
            var player = world.OfflinePlayer;
            var controller = player.GetComponent<CharacterController>();
            Assert.That(controller.minMoveDistance, Is.Zero, "Small high-frame-rate steps must not be discarded.");
            var initial = player.transform.position;
            for (int step = 0; step < 1000; step++) player.Move(Vector2.right, .0001f);
            Assert.That(player.transform.position.x - initial.x, Is.GreaterThan(.3f), "High-frame-rate movement must accumulate.");
            // Walk backwards from the first spawn into whatever stands behind it, measured from the scene.
            var spawn = world.Spawns[0];
            controller.enabled = false;
            player.transform.SetPositionAndRotation(spawn.position, spawn.rotation);
            controller.enabled = true;
            var backward = -player.transform.forward;
            Assert.That(Physics.SphereCast(player.transform.position + Vector3.up, controller.radius * .9f, backward, out var wall, 20, 1,
                QueryTriggerInteraction.Ignore), Is.True, "Nothing solid behind the spawn point.");
            for (int i = 0; i < 400; i++) { player.Move(Vector2.down, .05f); yield return null; }
            var travelled = Vector3.Dot(player.transform.position - spawn.position, backward);
            Assert.That(travelled, Is.LessThanOrEqualTo(wall.distance + .05f), "Physical wall must stop movement.");
            Assert.That(travelled, Is.GreaterThan(wall.distance - .3f), "Movement stopped well before the wall.");
            player.GetComponent<Posture>().Set(PostureState.Squatting);
            var start = player.transform.position;
            for (int i = 0; i < 20; i++) { player.Move(Vector2.up, .05f); yield return null; }
            Assert.That(Vector2.Distance(new Vector2(start.x, start.z), new Vector2(player.transform.position.x, player.transform.position.z)), Is.LessThan(.01f));
        }

        [UnityTest]
        public IEnumerator StandingVisualTouchesTheFloor()
        {
            var player = world.OfflinePlayer;
            var controller = player.GetComponent<CharacterController>();
            player.Move(Vector2.zero, .05f);
            yield return null;

            var rayOrigin = player.transform.position + Vector3.up * .5f;
            var worldLayers = ~(1 << player.gameObject.layer);
            Assert.That(Physics.Raycast(rayOrigin, Vector3.down, out var floor, 2, worldLayers, QueryTriggerInteraction.Ignore),
                Is.True, "No floor was found under the standing player.");
            var visualBottom = player.Body.GetComponentsInChildren<Renderer>()
                .Min(renderer => renderer.bounds.min.y);
            Assert.That(Mathf.Abs(visualBottom - floor.point.y), Is.LessThan(.015f),
                $"The standing visual is {visualBottom - floor.point.y:F3}m above the floor; " +
                $"controller root gap is {player.transform.position.y - floor.point.y:F3}m and skin width is {controller.skinWidth:F3}m.");
        }
        [UnityTest]
        public IEnumerator CSitsOnTheStallsOwnToiletAndESeatedOperatesTheDoor()
        {
            var player = world.OfflinePlayer;
            var input = player.GetComponent<NatureCalls.Input.PlayerInput>();
            var posture = player.GetComponent<Posture>();
            input.enabled = true; // PlayerInput times the toileting; batchmode has no real keyboard to interfere.

            // Outside any stall C squats where the player stands, for a fixed time.
            Assert.That(StallDoor.Containing(player), Is.Null);
            Assert.That(input.TryToilet(), Is.True);
            Assert.That(posture.State, Is.EqualTo(PostureState.Squatting));
            Assert.That(player.Seat, Is.Null, "Squatting outside a stall uses no toilet.");
            Assert.That(input.TryToilet(), Is.False, "C is one press: it cannot cut the toileting short.");
            Assert.That(posture.State, Is.EqualTo(PostureState.Squatting));
            yield return new WaitForSeconds(NatureCalls.Core.NeedsRules.ToiletingSeconds + .2f);
            Assert.That(posture.State, Is.EqualTo(PostureState.Standing), "The player stands up by themselves after the toileting time.");
            Assert.That(input.TryInteract(), Is.False, "E no longer squats or sits.");

            // Inside each stall C picks that stall's own toilet, never a neighbour's.
            var doors = Object.FindObjectsByType<StallDoor>(FindObjectsSortMode.None);
            Assert.That(doors.Length, Is.GreaterThanOrEqualTo(2));
            foreach (var stall in doors)
            {
                var toilet = stall.Toilet;
                Assert.That(toilet, Is.Not.Null, stall.name + " has a toilet.");
                yield return OpenStallAndStandAt(player, toilet);
                // Anywhere between just inside the door and the seat, and to either side, counts as this stall.
                var outward = -toilet.Anchor.forward;
                var side = Vector3.Cross(Vector3.up, outward).normalized;
                var depth = Vector3.Dot(new Vector3(stall.DoorLeaf.position.x, 0, stall.DoorLeaf.position.z) - new Vector3(toilet.Anchor.position.x, 0, toilet.Anchor.position.z), outward);
                for (float along = 0; along <= depth - .2f; along += .2f)
                for (float across = -.25f; across <= .25f; across += .25f)
                {
                    yield return Teleport(player, toilet.Anchor.position + outward * along + side * across);
                    Assert.That(StallDoor.Containing(player), Is.SameAs(stall), $"{stall.name}: {along:F1}m from the seat, {across:F2}m to the side");
                }
                yield return Teleport(player, toilet.Anchor.position + outward * .25f);
                var before = player.transform.position;
                Assert.That(input.TryToilet(), Is.True);
                Assert.That(player.Seat, Is.EqualTo(toilet.Anchor), "C sits on the toilet of the stall the player is in.");
                Assert.That(toilet.Occupant, Is.EqualTo(player));
                yield return null;

                // Seated, E reaches the door: close, then lock from inside, then unlock and open.
                Assert.That(input.TryInteract(), Is.True);
                Assert.That(stall.State, Is.EqualTo(StallDoorState.Closed));
                Assert.That(input.TryInteract(), Is.True);
                Assert.That(stall.State, Is.EqualTo(StallDoorState.Locked), "A seated player can lock the stall.");
                Assert.That(posture.State, Is.EqualTo(PostureState.Squatting), "E does not stand the player up.");
                Assert.That(input.TryInteract(), Is.True);
                Assert.That(stall.State, Is.EqualTo(StallDoorState.Open));

                yield return new WaitForSeconds(NatureCalls.Core.NeedsRules.ToiletingSeconds + .2f);
                Assert.That(posture.State, Is.EqualTo(PostureState.Standing), "The player stands up after the toileting time.");
                Assert.That(player.Seat, Is.Null);
                Assert.That(toilet.IsOccupied, Is.False, "Standing up frees the toilet.");
                Assert.That(Vector3.Distance(player.transform.position, before), Is.LessThan(.01f), "Standing up returns to where the player sat down from.");
            }

            // The last stall: after standing up the player can walk out.
            var last = doors[doors.Length - 1].Toilet;
            var exit = -last.Anchor.forward;
            var start = player.transform.position;
            for (int i = 0; i < 20; i++) { player.Move(Vector2.down, .05f); yield return null; }
            Assert.That(Vector3.Dot(player.transform.position - start, exit), Is.GreaterThan(.3f), "After standing up the player can walk out of the stall.");
        }
        [UnityTest]
        public IEnumerator NeedsRiseClearOnToiletingAndOutsideAStallLeaveADropping()
        {
            var player = world.OfflinePlayer;
            var needs = player.GetComponent<PlayerNeeds>();
            var input = player.GetComponent<NatureCalls.Input.PlayerInput>();
            input.enabled = true; // PlayerInput times the toileting.
            Assert.That(needs, Is.Not.Null, "The Player prefab carries PlayerNeeds.");
            needs.BeginRound(0);
            Assert.That(needs.Urgency, Is.Zero);
            yield return new WaitForSeconds(1);
            Assert.That(needs.Urgency, Is.EqualTo(NatureCalls.Core.NeedsRules.UrgencyPerSecond).Within(.5f), "Urgency fills once a minute.");

            // Outside a stall: Urgency pauses while toileting, then clears; Disgust +10 and a dropping stays behind.
            Assert.That(input.TryToilet(), Is.True);
            var paused = needs.Urgency;
            yield return new WaitForSeconds(1);
            Assert.That(needs.Urgency, Is.EqualTo(paused), "Urgency does not rise while toileting.");
            var posture = player.GetComponent<Posture>();
            float deadline = Time.time + NatureCalls.Core.NeedsRules.ToiletingSeconds + 1;
            while (posture.IsSquatting && Time.time < deadline) yield return null;
            Assert.That(posture.IsSquatting, Is.False);
            Assert.That(needs.Urgency, Is.LessThan(.5f), "Finishing a toileting clears Urgency.");
            Assert.That(needs.Disgust, Is.EqualTo(NatureCalls.Core.NeedsRules.OutsideStallDisgust));
            Assert.That(world.DroppingCount, Is.EqualTo(1), "A black ball is left where the player toileted.");

            // In a stall: Urgency clears, no Disgust and no dropping.
            var toilet = FindToilet();
            yield return OpenStallAndStandAt(player, toilet);
            yield return Teleport(player, toilet.Anchor.position - toilet.Anchor.forward * .25f);
            Assert.That(input.TryToilet(), Is.True);
            Assert.That(player.Seat, Is.EqualTo(toilet.Anchor));
            deadline = Time.time + NatureCalls.Core.NeedsRules.ToiletingSeconds + 1;
            while (posture.IsSquatting && Time.time < deadline) yield return null;
            Assert.That(needs.Disgust, Is.EqualTo(NatureCalls.Core.NeedsRules.OutsideStallDisgust));
            Assert.That(world.DroppingCount, Is.EqualTo(1));
            Assert.That(needs.Urgency, Is.LessThan(.5f), "Toileting in a stall clears Urgency too.");
        }
        [UnityTest]
        public IEnumerator AnOccupiedToiletRefusesASecondPlayer()
        {
            var toilet = FindToilet();
            var other = Object.Instantiate(world.PlayerPrefab, world.Spawns[1].position, world.Spawns[1].rotation).GetComponent<Movement>();
            other.SetControl(true);
            yield return null;
            Assert.That(toilet.Seat(world.OfflinePlayer), Is.True);
            Assert.That(toilet.CanSeat(other), Is.False, "One player at a time.");
            Assert.That(toilet.Seat(other), Is.False);
            Assert.That(other.GetComponent<Posture>().State, Is.EqualTo(PostureState.Standing));
            world.OfflinePlayer.GetComponent<Posture>().Set(PostureState.Standing);
            toilet.Release(world.OfflinePlayer);
            Assert.That(toilet.Seat(other), Is.True, "The toilet is free again.");
            Object.Destroy(other.gameObject);
        }
        [UnityTest]
        public IEnumerator HostSeatIsGrantedAndSharedThroughTheHostAvatar()
        {
            var session = Object.FindFirstObjectByType<NatureCalls.Network.Session>();
            Assert.That(session.StartDirect(true), Is.True);
            float deadline = Time.time + 5;
            while (session.Manager.LocalClient.PlayerObject == null && Time.time < deadline) yield return null;
            var avatar = session.Manager.LocalClient.PlayerObject.GetComponent<NatureCalls.Network.Avatar>();
            var player = avatar.GetComponent<Movement>();
            var toilet = FindToilet();
            player.GetComponent<NatureCalls.Input.PlayerInput>().enabled = true;
            yield return OpenStallAndStandAt(player, toilet);
            yield return Teleport(player, toilet.Anchor.position - toilet.Anchor.forward * .25f);
            Assert.That(player.GetComponent<NatureCalls.Input.PlayerInput>().TryToilet(), Is.True);
            deadline = Time.time + 2;
            while (player.Seat == null && Time.time < deadline) yield return null;
            Assert.That(player.Seat, Is.EqualTo(toilet.Anchor), "The host grants the free seat.");
            Assert.That(avatar.SharedToilets.Contains(avatar.OwnerClientId + 1), Is.True, "The occupant is shared.");
            deadline = Time.time + NatureCalls.Core.NeedsRules.ToiletingSeconds + 2;
            while (avatar.SharedToilets.Contains(avatar.OwnerClientId + 1) && Time.time < deadline) yield return null;
            Assert.That(avatar.SharedToilets.Contains(avatar.OwnerClientId + 1), Is.False, "Standing up frees the shared seat.");
            Assert.That(toilet.IsOccupied, Is.False);
        }
        [UnityTest]
        public IEnumerator CameraBlendsAndInteriorIsVisible()
        {
            var player = world.OfflinePlayer;
            var toilet = FindToilet();
            yield return OpenStallAndStandAt(player, toilet);
            var controller = player.GetComponent<CharacterController>();
            player.Look(new Vector2(40, -50));
            yield return null;
            var heading = player.ViewRotation;
            var camera = world.Camera.Output;
            Capture(camera, "standing.png");
            Assert.That(toilet.Seat(player), Is.True);
            Assert.That(player.GetComponent<Posture>().State, Is.EqualTo(PostureState.Squatting));
            var last = camera.transform.position;
            float deadline = Time.time + 1;
            int intermediateFrames = 0;
            while (Time.time < deadline)
            {
                yield return null;
                Assert.That(Vector3.Distance(last, camera.transform.position), Is.LessThan(1), "Blend jumped in one frame.");
                last = camera.transform.position;
                if (world.Camera.Brain.IsBlending) intermediateFrames++;
            }
            Assert.That(intermediateFrames, Is.GreaterThan(1), "Expected a real multi-frame Cinemachine blend.");
            Assert.That(world.Camera.ThirdPerson.Priority.Value, Is.GreaterThan(world.Camera.FirstPerson.Priority.Value));
            Capture(camera, "squatting.png");
            var offset = player.transform.InverseTransformPoint(camera.transform.position);
            Assert.That(Mathf.Abs(offset.x), Is.LessThan(.03f), "Seated camera must stay centered on the player.");
            var lookFrom = world.Camera.Shoulder.position;
            var blocked = Physics.SphereCast(lookFrom, world.Camera.CameraRadius, -world.Camera.Shoulder.forward, out var blocker,
                world.Camera.SeatedDistance, world.Camera.CameraCollisionLayers) ? blocker.collider.name + " at " + blocker.point : "nothing";
            blocked += "; overlapping " + string.Join(",", Physics.OverlapSphere(lookFrom, world.Camera.CameraRadius, world.Camera.CameraCollisionLayers)
                .Select(item => item.name + "(" + item.GetType().Name + (item.isTrigger ? ",trigger" : "") + ")"))
                + "; camera " + camera.transform.position + " shoulder " + lookFrom + " pitch " + world.Camera.Shoulder.eulerAngles.x;
            Assert.That(offset.y, Is.GreaterThan(1.5f), "Seated on a toilet the camera looks down from above the stall; blocked by " + blocked);
            Assert.That(offset.z, Is.InRange(-.8f, 0f), "Top-down camera stays just behind the player.");
            Assert.That(camera.transform.eulerAngles.x, Is.InRange(60f, 89.9f), "Top-down camera looks steeply down.");
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(camera.transform.eulerAngles.y, player.transform.eulerAngles.y)), Is.LessThan(.1f));
            Assert.That(camera.WorldToViewportPoint(player.Body.position).x, Is.EqualTo(.5f).Within(.02f));
            var viewport = camera.WorldToViewportPoint(toilet.Interior.position);
            Assert.That(viewport.x, Is.InRange(.05f, .95f));
            Assert.That(viewport.y, Is.InRange(.05f, .95f));
            Assert.That(viewport.z, Is.GreaterThan(0));
            var ray = new Ray(camera.transform.position, toilet.Interior.position - camera.transform.position);
            Assert.That(Physics.Raycast(ray, out var hit, 20, 1), Is.True);
            // The WC bowl's own box collider encloses its opening; anything else in between is an obstruction.
            Assert.That(hit.transform == toilet.Interior || hit.transform == toilet.transform, Is.True,
                "World geometry obscures the toilet interior: " + hit.transform.name);
            foreach (var renderer in player.Body.GetComponentsInChildren<Renderer>())
            {
                Assert.That(renderer.forceRenderingOff, Is.False, "Local third-person body must be visible.");
                var bounds = renderer.bounds;
                for (int corner = 0; corner < 8; corner++)
                {
                    var point = bounds.center + Vector3.Scale(bounds.extents, new Vector3((corner & 1) == 0 ? -1 : 1,
                        (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1));
                    var screen = camera.WorldToViewportPoint(point);
                    Assert.That(screen.z, Is.GreaterThan(0));
                    Assert.That(screen.x, Is.InRange(.02f, .98f), "Body clipped horizontally: " + renderer.name);
                    Assert.That(screen.y, Is.InRange(.02f, .98f), "Body clipped vertically: " + renderer.name);
                }
                Assert.That(Obscures(renderer, ray, Vector3.Distance(ray.origin, toilet.Interior.position)), Is.False,
                    "Local body obscures the center of the placeholder: " + renderer.name);
            }
            Capture(camera, "squatting.png");
            player.GetComponent<Posture>().Set(PostureState.Standing);
            yield return new WaitForSeconds(1);
            Assert.That(Quaternion.Angle(player.ViewRotation, heading), Is.LessThan(.1f), "Return must preserve standing look direction.");
            Assert.That(Vector3.Distance(camera.transform.position, player.transform.position + Vector3.up * world.Camera.EyeHeight), Is.LessThan(.05f));
            // First person hides the local body from the screen camera only (mirrors still render it).
            Assert.That(camera.cullingMask & (1 << NatureCalls.Camera.CameraController.LocalBodyLayer), Is.Zero, "First-person output must hide the local body.");
            Capture(camera, "returned.png");
        }
        // Ray test against the body's actual (baked) mesh; bounding boxes are too coarse for the top-down view.
        private static bool Obscures(Renderer renderer, Ray ray, float maxDistance)
        {
            var mesh = new Mesh();
            var probe = new GameObject("Occlusion probe");
            try
            {
                if (renderer is SkinnedMeshRenderer skinned)
                {
                    skinned.BakeMesh(mesh, true);
                    probe.transform.SetPositionAndRotation(renderer.transform.position, renderer.transform.rotation);
                }
                else if (renderer.TryGetComponent(out MeshFilter filter) && filter.sharedMesh != null)
                {
                    mesh = Object.Instantiate(filter.sharedMesh);
                    probe.transform.SetPositionAndRotation(renderer.transform.position, renderer.transform.rotation);
                    probe.transform.localScale = renderer.transform.lossyScale;
                }
                else return renderer.bounds.IntersectRay(ray, out var distance) && distance < maxDistance;
                var collider = probe.AddComponent<MeshCollider>();
                collider.sharedMesh = mesh;
                return collider.Raycast(ray, out _, maxDistance);
            }
            finally { Object.Destroy(probe); Object.Destroy(mesh); }
        }

        private static int ScreenCameras() => UnityEngine.Camera.allCameras.Count(camera => camera.targetTexture == null);

        private Toilet FindToilet()
        {
            var toilet = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Toilet>()).FirstOrDefault();
            Assert.That(toilet, Is.Not.Null, "The map has no Toilet interaction.");
            return toilet;
        }

        // Opens the stall from outside, as a player would, then stands at the posture anchor.
        private static IEnumerator OpenStallAndStandAt(Movement player, Toilet toilet)
        {
            var door = toilet.GetComponentInParent<StallDoor>();
            var controller = player.GetComponent<CharacterController>();
            if (door != null && door.State != StallDoorState.Open)
            {
                // The anchor faces the bowl, so its back points out of the stall.
                var outward = -toilet.Anchor.forward;
                controller.enabled = false;
                player.transform.position = new Vector3(door.DoorColliderPivot.position.x, 0, door.DoorColliderPivot.position.z) + outward * .6f;
                controller.enabled = true;
                Assert.That(door.IsInside(player), Is.False);
                Assert.That(door.Interact(player), Is.True);
                Assert.That(door.State, Is.EqualTo(StallDoorState.Open));
            }
            controller.enabled = false;
            player.transform.SetPositionAndRotation(toilet.Anchor.position, toilet.Anchor.rotation);
            controller.enabled = true;
            yield return null;
        }

        private static IEnumerator Teleport(Movement player, Vector3 position)
        {
            var controller = player.GetComponent<CharacterController>();
            controller.enabled = false;
            player.transform.position = position;
            controller.enabled = true;
            yield return null;
        }

        private static void Capture(UnityEngine.Camera camera, string name)
        {
            var directory = System.Environment.GetEnvironmentVariable("NATURECALLS_RUN_PATH");
            if (string.IsNullOrEmpty(directory)) return;
            var texture = new RenderTexture(1100, 760, 24);
            texture.Create();
            var previous = RenderTexture.active;
            var image = new Texture2D(1100, 760, TextureFormat.RGB24, false);
            try
            {
                RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = texture });
                RenderTexture.active = texture;
                image.ReadPixels(new Rect(0, 0, 1100, 760), 0, 0);
                image.Apply();
                File.WriteAllBytes(Path.Combine(directory, name), image.EncodeToPNG());
            }
            finally { RenderTexture.active = previous; texture.Release(); Object.Destroy(texture); Object.Destroy(image); }
        }
    }
}
