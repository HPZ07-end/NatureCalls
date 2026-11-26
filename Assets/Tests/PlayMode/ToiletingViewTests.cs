using System.Collections;
using System.IO;
using System.Linq;
using NatureCalls.Facility;
using NatureCalls.Input;
using NatureCalls.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using PlayerInput = NatureCalls.Input.PlayerInput;

namespace NatureCalls.Tests
{
    /// <summary>GLOSSARY Squatting/统一如厕过程: timed look-down, the falling stool and splash, the urine arc and rebound.</summary>
    public sealed class ToiletingViewTests
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
            MatchContext.Reset(); ToiletingFx.ButtSplashChance = .5f;
            world.StopPractice();
            yield return SceneManager.UnloadSceneAsync("Playground");
        }
        private void Place(Vector3 position, Vector3 forward)
        {
            player.GetComponent<CharacterController>().enabled = false;
            player.transform.SetPositionAndRotation(position, Quaternion.LookRotation(forward));
        }
        private Toilet Sit()
        {
            var toilet = Object.FindFirstObjectByType<Toilet>();
            Place(toilet.Anchor.position - toilet.Anchor.forward * .15f, toilet.Anchor.forward);
            Assert.That(input.TryToilet(), Is.True);
            player.SetPitch(50); Assert.That(input.TryToilet(), Is.True);
            Assert.That(input.Phase, Is.EqualTo(PlayerInput.Stage.Aiming));
            return toilet;
        }

        [Test] public void ViewIsAttachedToThePlayer()
        {
            Assert.That(player.GetComponent<ToiletGaze>(), Is.Not.Null);
            Assert.That(player.GetComponent<ToiletingFx>(), Is.Not.Null);
            Assert.That(player.GetComponent<ComfortSparkle>(), Is.Not.Null);
        }

        [UnityTest] public IEnumerator LookingDownIsTimedShowsTheLandingAndEarnsComfort()
        {
            MatchContext.Phase = MatchPhase.Playing; needs.SetVersusState(0, 80);
            Sit();
            var gaze = player.GetComponent<ToiletGaze>();
            yield return null;
            Assert.That(gaze.Looking, Is.False);
            float comfort = needs.Comfort;
            for (float t = 0; t < .6f; t += Time.deltaTime) { gaze.Push(1); yield return null; }
            Assert.That(gaze.Looking, Is.True);
            Assert.That(player.Pitch, Is.GreaterThan(45), "Head bowed (positive pitch looks down).");
            Capture("toileting-defecation-aim.png");
            Assert.That(gaze.Marker == null || !gaze.Marker.activeSelf, Is.True, "Blind aim has no landing marker.");
            Assert.That(gaze.DriftScale, Is.EqualTo(ToiletGaze.DriftWhileLooking));
            Assert.That(world.Camera.Gaze, Is.GreaterThan(.6f));
            for (float t = 0; t < ToiletGaze.Budget + 1.5f; t += Time.deltaTime) { gaze.Push(1); yield return null; }
            Assert.That(gaze.Exhausted, Is.True);
            Assert.That(gaze.Looking, Is.False, "After about 3 s the view is pulled back up.");
            Assert.That(gaze.Amount, Is.LessThan(.1f));
            Assert.That(gaze.Marker == null || !gaze.Marker.activeSelf, Is.True);
            Assert.That(needs.Comfort, Is.GreaterThan(comfort), "PlayerNeeds rewards the bowed head.");
        }

        [UnityTest] public IEnumerator StoolFallsIntoTheWaterAndCanSplashTheButtocks()
        {
            Sit();
            ToiletingFx.ButtSplashChance = 1;
            var delta = input.TargetCenter - input.CurrentLanding; input.TickAim(.01f, new Vector2(delta.x, delta.z));
            var fx = player.GetComponent<ToiletingFx>();
            bool? inBowl = null, butt = null; fx.Dropped += (bowl, splash) => { inBowl = bowl; butt = splash; };
            fx.BeginDrop();
            Assert.That(fx.Dropping, Is.True);
            for (float t = 0; t < 2 && inBowl == null; t += Time.deltaTime) yield return null;
            Assert.That(inBowl, Is.True, "Seen falling into the bowl.");
            Assert.That(butt, Is.True);
            Assert.That(SplashOverlay.Current != null && SplashOverlay.Current.gameObject.activeSelf, Is.True, "The splash is obvious on screen.");
        }

        [Test] public void ReboundFollowsTheAngleOfIncidence()
        {
            var wall = Vector3.back;
            Assert.That(ToiletingFx.Incidence(Vector3.forward, wall), Is.EqualTo(1).Within(1e-4), "Head-on.");
            Assert.That(ToiletingFx.Rebound(Vector3.forward * 2, wall), Is.EqualTo(Vector3.back * 2 * ToiletingFx.Restitution));
            var grazing = new Vector3(1, 0, .2f);
            Assert.That(ToiletingFx.Incidence(grazing, wall), Is.LessThan(ToiletingFx.SelfSplashCosine));
            Assert.That(Vector3.Dot(ToiletingFx.Rebound(grazing, wall), Vector3.right), Is.GreaterThan(0), "A glancing hit carries on sideways.");
            var origin = new Vector3(0, 1, 0); var target = new Vector3(0, .8f, 1);
            var v = ToiletingFx.Launch(origin, target, out float time);
            Assert.That(origin + v * time + .5f * Physics.gravity * time * time, Is.EqualTo(target).Using(Vector3EqualityComparerWithin(.001f)));
        }

        [UnityTest] public IEnumerator UrinatingFacesTheUrinalDrawsAnArcAndSelfSplashCostsComposure()
        {
            var urinal = Object.FindFirstObjectByType<Urinal>();
            Place(urinal.FrontPosition, Vector3.right); needs.BeginRound(1);
            Assert.That(input.TryUrinate(), Is.True); Assert.That(input.Phase, Is.EqualTo(PlayerInput.Stage.Urinating));
            Capture("toileting-urinal-aim.png");
            yield return null;
            var toUrinal = Vector3.ProjectOnPlane(urinal.TargetCenter - player.transform.position, Vector3.up).normalized;
            Assert.That(Vector3.Dot(player.transform.forward, toUrinal), Is.GreaterThan(.95f), "The view turns to face the urinal.");
            var fx = player.GetComponent<ToiletingFx>();
            fx.TickStream(.02f);
            Assert.That(fx.Stream.enabled, Is.True);
            var end = fx.Stream.GetPosition(fx.Stream.positionCount - 1);
            Assert.That(Vector3.Distance(end, input.CurrentLanding), Is.LessThan(.02f), "The arc lands on the aim point.");
            var mid = fx.Stream.GetPosition(fx.Stream.positionCount / 2);
            var straight = Vector3.Lerp(fx.Stream.GetPosition(0), end, .5f);
            Assert.That(mid.y, Is.GreaterThan(straight.y - .005f), "A parabola bows above the chord.");
            float before = needs.Disgust;
            bool splashed = false; fx.SelfSplashed += () => splashed = true;
            fx.SplashSelf();
            Assert.That(splashed, Is.True);
            Assert.That(needs.Disgust - before, Is.EqualTo(15), "BodyEvents.Splash(OwnRebound) costs 15 Composure.");
        }

        [UnityTest] public IEnumerator ComfortBoostSparkles()
        {
            var sparkle = player.GetComponent<ComfortSparkle>();
            yield return null;
            needs.CompleteToileting(true);
            yield return null;
            Assert.That(sparkle.Bursts, Is.EqualTo(1));
        }

        [UnityTest] public IEnumerator LossOfControlAutoToiletConfirmsImmediately()
        {
            var toilet = Object.FindFirstObjectByType<Toilet>();
            player.GetComponent<CharacterController>().enabled = false;
            player.transform.position = toilet.TargetCenter - toilet.transform.forward * .4f;
            player.GetComponent<CharacterController>().enabled = true;
            var begin = typeof(PlayerNeeds).GetMethod("BeginLossOfControl", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.That(begin, Is.Not.Null);
            begin.Invoke(needs, null);
            needs.SendMessage("TickLossOfControl", 1f, SendMessageOptions.DontRequireReceiver);
            yield return null;
            Assert.That(input.Phase, Is.EqualTo(PlayerInput.Stage.None));
            Assert.That(toilet.IsOccupied, Is.False);
        }

        private static System.Collections.Generic.IEqualityComparer<Vector3> Vector3EqualityComparerWithin(float tolerance) => new Vector3Within(tolerance);
        private void Capture(string name)
        {
            var dir = System.Environment.GetEnvironmentVariable("NATURECALLS_RUN_PATH");
            if (!string.IsNullOrEmpty(dir)) { Directory.CreateDirectory(dir); ScreenCapture.CaptureScreenshot(Path.Combine(dir, name)); }
        }
        private sealed class Vector3Within : System.Collections.Generic.IEqualityComparer<Vector3>
        {
            private readonly float tolerance; public Vector3Within(float value) => tolerance = value;
            public bool Equals(Vector3 a, Vector3 b) => Vector3.Distance(a, b) <= tolerance;
            public int GetHashCode(Vector3 v) => 0;
        }
    }
}
