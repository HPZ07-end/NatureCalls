using System.Collections;
using System.Linq;
using NatureCalls.Core;
using NatureCalls.Facility;
using NatureCalls.Network;
using NatureCalls.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using PlayerInput = NatureCalls.Input.PlayerInput;
namespace NatureCalls.Tests
{
    public sealed class ToiletingTests
    {
        private Map.Playground world;
        private Movement player;
        private PlayerInput input;
        private PlayerNeeds needs;
        [UnitySetUp] public IEnumerator Load()
        {
            yield return SceneManager.LoadSceneAsync("Assets/Scenes/Playground.unity", LoadSceneMode.Additive);
            yield return null;
            world = Object.FindFirstObjectByType<Map.Playground>(); player = world.OfflinePlayer;
            input = player.GetComponent<PlayerInput>(); input.enabled = false;
            needs = player.GetComponent<PlayerNeeds>(); needs.BeginRound(0);
        }
        [UnityTearDown] public IEnumerator Unload()
        {
            world.StopPractice();
            var manager = Unity.Netcode.NetworkManager.Singleton;
            if (manager != null) { if (manager.IsListening) manager.Shutdown(); Object.Destroy(manager.gameObject); yield return null; }
            yield return SceneManager.UnloadSceneAsync("Playground");
        }
        private void Place(Vector3 position, Vector3 forward)
        {
            player.GetComponent<CharacterController>().enabled = false;
            player.transform.SetPositionAndRotation(position, Quaternion.LookRotation(forward));
        }
        private Toilet Align()
        {
            var toilet = Object.FindFirstObjectByType<Toilet>();
            var alignedPosition = toilet.Anchor.position - toilet.Anchor.forward * .15f;
            Place(alignedPosition, toilet.Anchor.forward);
            Assert.That(input.TryToilet(), Is.True);
            Assert.That(input.Phase, Is.EqualTo(PlayerInput.Stage.Aligning));
            Assert.That(player.Seat, Is.Null);
            Assert.That(input.TryToilet(), Is.True);
            Assert.That(input.Phase, Is.EqualTo(PlayerInput.Stage.Holding));
            Assert.That(player.transform.position, Is.EqualTo(alignedPosition), "No anchor snap.");
            return toilet;
        }
        private void Score(float insideSeconds, float duration)
        {
            var delta = input.TargetCenter - input.CurrentLanding;
            if (insideSeconds > 0) input.TickAim(insideSeconds, new Vector2(delta.x, delta.z));
            if (insideSeconds < duration) input.TickAim(duration - insideSeconds, new Vector2(5, 5));
        }
        [TestCase(5f, MessKind.None)] [TestCase(2f, MessKind.DirtyToilet)] [TestCase(0f, MessKind.Dropping)]
        public void AlignHoldAndSettleThreeAccuracyBands(float inside, MessKind expected)
        {
            var toilet = Align(); var before = player.transform.position;
            Assert.That(input.TryToilet(), Is.False);
            Score(inside, 5);
            Assert.That(input.Phase, Is.EqualTo(PlayerInput.Stage.None));
            Assert.That(player.GetComponent<Posture>().IsSquatting, Is.False);
            Assert.That(player.transform.position, Is.EqualTo(before)); Assert.That(toilet.IsOccupied, Is.False);
            Assert.That(needs.Urgency, Is.Zero); Assert.That(input.Unwiped, Is.True);
            if (expected == MessKind.None) Assert.That(world.Messes.Where(m => m != null), Is.Empty);
            else Assert.That(world.Messes.Where(m => m != null).Select(m => m.Kind), Is.EquivalentTo(new[] { expected }));
            if (expected == MessKind.DirtyToilet)
            {
                PlayerInput.ApplyResult(player, toilet, null, 1, before, PlayerInput.ToiletIndex(toilet));
                Assert.That(needs.Disgust, Is.EqualTo(10));
            }
        }
        [UnityTest] public IEnumerator ToiletingHudShowsPromptTargetAndUnwipedWarning()
        {
            var hud = Object.FindFirstObjectByType<UI.ToiletingHud>();
            Assert.That(hud, Is.Not.Null, "MatchHud carries the toileting HUD.");
            GameObject Find(string name) => hud.GetComponentsInChildren<Transform>(true).First(item => item.name == name).gameObject;
            yield return null;
            Assert.That(Find("ToiletingPanel").activeSelf, Is.False, "Nothing is shown before toileting.");
            Align();
            yield return null;
            Assert.That(Find("ToiletingPanel").activeSelf, Is.True);
            Assert.That(Find("PhaseLabel").GetComponent<UnityEngine.UI.Text>().text, Does.Contain("保持"));
            Assert.That(Find("AccuracyLabel").activeSelf, Is.True);
            Assert.That(Find("TargetOverlay").activeSelf, Is.True, "The target circle is drawn while holding.");
            Assert.That(Find("LandingMarker").activeSelf, Is.True);
            Score(5, 5);
            yield return null;
            Assert.That(Find("ToiletingPanel").activeSelf, Is.False, "The prompt hides when the toileting ends.");
            Assert.That(Find("TargetOverlay").activeSelf, Is.False);
            Assert.That(Find("UnwipedWarning").activeSelf, Is.True, "After defecating the player is warned to wipe.");
        }
        [Test] public void UrinalOccupancyHalvingAndPuddle()
        {
            Assert.That(Object.FindObjectsByType<Urinal>(FindObjectsSortMode.None).Length, Is.EqualTo(3));
            var urinal = Object.FindFirstObjectByType<Urinal>();
            Place(urinal.FrontPosition, Vector3.forward); needs.BeginRound(1);
            Assert.That(input.TryToilet(), Is.True); Assert.That(input.Phase, Is.EqualTo(PlayerInput.Stage.Urinating));
            var other = new GameObject("other").AddComponent<Movement>();
            try { other.transform.position = urinal.FrontPosition; Assert.That(urinal.Occupy(other), Is.False); }
            finally { Object.DestroyImmediate(other.gameObject); }
            Assert.That(player.GetComponent<Posture>().IsSquatting, Is.False);
            Score(0, 3);
            Assert.That(urinal.IsOccupied, Is.False); Assert.That(needs.Urgency, Is.EqualTo(20));
            Assert.That(world.Messes.Count(m => m != null && m.Kind == MessKind.Puddle), Is.EqualTo(1));
        }
        [Test] public void UnwipedCanPickupThenWipeThreeTimesAndConsumeRoll()
        {
            var toilet = Align(); Score(5, 5);
            var paper = Object.FindObjectsByType<PaperRoll>(FindObjectsSortMode.None).First();
            paper.transform.position = player.transform.position;
            Assert.That(input.TryInteract(), Is.True); Assert.That(input.Unwiped, Is.True); Assert.That(needs.Disgust, Is.Zero);
            Assert.That(input.HeldItem, Is.SameAs(paper));
            for (int i = 2; i >= 0; i--)
            {
                needs.MarkUnwiped(toilet.GetComponentInParent<StallDoor>());
                Assert.That(input.TryInteract(), Is.True); Assert.That(input.Unwiped, Is.False);
                Assert.That(paper.RemainingUses, Is.EqualTo(i));
            }
            Assert.That(paper.gameObject.activeSelf, Is.False); Assert.That(input.HeldItem, Is.Null);
        }
        [UnityTest] public IEnumerator UnwipedDoorAndWalkingOutChargeOnceEach()
        {
            var toilet = Align(); Score(5, 5); var stall = toilet.GetComponentInParent<StallDoor>();
            Place(stall.DoorColliderPivot.position + stall.transform.forward * .2f, stall.transform.forward);
            Assert.That(input.TryInteract(), Is.True); Assert.That(needs.Disgust, Is.EqualTo(15)); Assert.That(input.Unwiped, Is.False);
            needs.MarkUnwiped(stall); Place(world.Spawns[0].position, Vector3.forward);
            yield return null; Assert.That(needs.Disgust, Is.EqualTo(30)); Assert.That(input.Unwiped, Is.False);
            yield return null; Assert.That(needs.Disgust, Is.EqualTo(30));
        }
        [Test] public void ToolboxCapacityThrowPickupAndReturn()
        {
            var box = Object.FindFirstObjectByType<Toolbox>(); Place(box.transform.position, Vector3.forward);
            Assert.That(StallDoor.Containing(player), Is.Null, "Toolbox must be in the public area.");
            Assert.That(Object.FindObjectsByType<Mop>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
            Assert.That(input.TryInteract(), Is.True); Assert.That(input.HeldItem, Is.SameAs(box.Mop));
            var paper = Object.FindFirstObjectByType<PaperRoll>(); paper.transform.position = player.transform.position;
            Assert.That(paper.Pickup(player), Is.False);
            Assert.That(box.Mop.Throw(player, Vector3.forward), Is.True);
            box.Mop.ApplySharedRest(player.transform.position, Quaternion.identity);
            paper.transform.position += Vector3.right;
            Assert.That(input.TryInteract(), Is.True); Assert.That(input.HeldItem, Is.SameAs(box.Mop), "Nearest dropped item wins.");
            Assert.That(input.TryInteract(), Is.True); Assert.That(box.Mop.Stored, Is.True); Assert.That(input.HeldItem, Is.Null);
        }
        [Test] public void CleaningRequiresContinuousHoldAndRange()
        {
            var box = Object.FindFirstObjectByType<Toolbox>(); Place(box.transform.position, Vector3.forward); box.Interact(player);
            world.Drop(player.transform.position + Vector3.forward * .5f); var mess = world.Messes.Last();
            Assert.That(input.BeginCleaning(mess), Is.True); input.ContinueCleaning(true, 2); input.ContinueCleaning(false, 1);
            Assert.That(world.DroppingCount, Is.EqualTo(1)); Assert.That(input.CleaningProgress, Is.Zero);
            input.BeginCleaning(mess); input.ContinueCleaning(true, 2); Place(player.transform.position + Vector3.right * 3, Vector3.forward); input.ContinueCleaning(true, 1);
            Assert.That(world.DroppingCount, Is.EqualTo(1)); Assert.That(input.CleaningProgress, Is.Zero);
            Place(mess.transform.position - Vector3.forward * .5f, Vector3.forward);
            input.BeginCleaning(mess); input.ContinueCleaning(true, 3);
            Assert.That(world.DroppingCount, Is.Zero); Assert.That(needs.Disgust, Is.EqualTo(5));
        }
        [UnityTest] public IEnumerator PuddleAddsDisgustOnlyNearby()
        {
            world.AddMess(MessKind.Puddle, player.transform.position);
            yield return new WaitForSeconds(1);
            Assert.That(needs.Disgust, Is.EqualTo(2).Within(.5));
            Place(player.transform.position + Vector3.right * 3, Vector3.forward); float before = needs.Disgust;
            yield return new WaitForSeconds(.3f); Assert.That(needs.Disgust, Is.EqualTo(before));
        }
        [UnityTest] public IEnumerator HostValidatesUrinalResultBeforeApplyingIt()
        {
            var session = Object.FindFirstObjectByType<Session>(); Assert.That(session.StartDirect(true), Is.True);
            float deadline = Time.time + 5;
            while (session.Manager.LocalClient.PlayerObject == null && Time.time < deadline) yield return null;
            player = session.Manager.LocalClient.PlayerObject.GetComponent<Movement>();
            input = player.GetComponent<PlayerInput>(); input.enabled = false;
            needs = player.GetComponent<PlayerNeeds>(); needs.BeginRound(1);
            var sync = player.GetComponent<FacilitySync>(); var urinal = Object.FindFirstObjectByType<Urinal>();
            Place(urinal.FrontPosition, Vector3.forward);
            Assert.That(input.TryToilet(), Is.True); yield return null;
            Assert.That(urinal.Occupant, Is.SameAs(player));
            float initial = needs.Urgency;
            sync.ReportResult(0, player.transform.position); yield return null;
            Assert.That(needs.Urgency, Is.EqualTo(initial), "Early completion is rejected.");
            yield return new WaitForSeconds(NeedsRules.UrinatingSeconds);
            sync.ReportResult(float.NaN, player.transform.position); yield return null;
            Assert.That(needs.Urgency, Is.EqualTo(initial), "Non-finite accuracy is rejected.");
            sync.ReportResult(0, player.transform.position + Vector3.right * 50); yield return null;
            Assert.That(needs.Urgency, Is.EqualTo(initial), "Remote result position is rejected.");
            input.TickAim(3, new Vector2(5, 5)); yield return null;
            Assert.That(urinal.IsOccupied, Is.False);
            Assert.That(needs.Urgency, Is.EqualTo(initial * .5f).Within(.2f));
            Assert.That(Facilities.Current.SharedMesses.Count, Is.EqualTo(1));
            Assert.That(Facilities.Current.SharedMesses[0].Kind, Is.EqualTo(MessKind.Puddle));
            float after = needs.Urgency; sync.ReportResult(0, player.transform.position); yield return null;
            Assert.That(needs.Urgency, Is.GreaterThanOrEqualTo(after), "A repeated completion cannot halve twice.");
        }
        [UnityTest] public IEnumerator HostPublishesMopMessAndCleaning()
        {
            var session = Object.FindFirstObjectByType<Session>(); Assert.That(session.StartDirect(true), Is.True);
            float deadline = Time.time + 5;
            while (session.Manager.LocalClient.PlayerObject == null && Time.time < deadline) yield return null;
            player = session.Manager.LocalClient.PlayerObject.GetComponent<Movement>();
            input = player.GetComponent<PlayerInput>(); input.enabled = false; needs = player.GetComponent<PlayerNeeds>(); needs.BeginRound(0);
            var sync = player.GetComponent<FacilitySync>(); var box = Object.FindFirstObjectByType<Toolbox>();
            Place(box.transform.position, Vector3.forward); Assert.That(sync.RequestToolbox(box), Is.True);
            yield return null; Assert.That(Enumerable.Range(0, Facilities.Current.SharedItems.Count).Any(i => Facilities.Current.SharedItems[i].Mode == ItemSnapshot.Carried), Is.True);
            Facilities.Current.AddMess(MessKind.Dropping, player.transform.position + Vector3.forward * .5f);
            yield return null; Assert.That(world.DroppingCount, Is.EqualTo(1));
            var mess = world.Messes.Last(); input.BeginCleaning(mess);
            float end = Time.time + 3.4f;
            while (Time.time < end) { input.ContinueCleaning(true, Time.deltaTime); yield return null; }
            Assert.That(Facilities.Current.SharedMesses[mess.Index].Kind, Is.EqualTo(MessKind.None));
            Assert.That(world.DroppingCount, Is.Zero); Assert.That(needs.Disgust, Is.EqualTo(5));
            Assert.That(sync.RequestToolbox(box), Is.True); yield return null;
            Assert.That(Enumerable.Range(0, Facilities.Current.SharedItems.Count).Any(i => Facilities.Current.SharedItems[i].Mode == ItemSnapshot.Stored), Is.True);
        }
    }
}
