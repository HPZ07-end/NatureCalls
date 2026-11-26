using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using NatureCalls.Core;
using NatureCalls.Facility;
using NatureCalls.Player;

namespace NatureCalls.Tests
{
    public sealed class VersusBodyTests
    {
        private GameObject root;
        private PlayerNeeds needs;
        private TestDirt dirt;

        [SetUp] public void SetUp()
        {
            MatchContext.Reset(); MatchContext.Phase = MatchPhase.Playing;
            root = new GameObject("versus-body-test");
            root.AddComponent<CharacterController>(); root.AddComponent<Posture>(); root.AddComponent<Movement>(); needs = root.AddComponent<PlayerNeeds>(); needs.BeginRound(0);
        }
        [TearDown] public void TearDown() { if (dirt != null) DirtField.Unregister(dirt); MatchContext.Reset(); RoleRegistry.Clear(); if (root != null) Object.DestroyImmediate(root); }

        [UnityTest] public IEnumerator RestrictionBandsAreApplied() { needs.SetVersusState(40, 0); yield return null; var effect = MovementEffectRegistry.Combine(root.GetComponent<Movement>()); Assert.That(effect.BlocksJump, Is.True); needs.SetVersusState(70, 0); effect = MovementEffectRegistry.Combine(root.GetComponent<Movement>()); Assert.That(effect.SpeedMultiplier, Is.EqualTo(.8f).Within(.01)); }
        [UnityTest] public IEnumerator EnduranceStartsAtFullNeedAndExposesNoTimer() { needs.SetVersusState(100, 0); yield return null; Assert.That(needs.InEndurance, Is.True); Assert.That(needs.GetModifier(root.GetComponent<Movement>()).SpeedMultiplier, Is.EqualTo(1f).Within(.01)); Assert.That(needs.EnduranceIntensity, Is.EqualTo(1)); var effect = MovementEffectRegistry.Combine(root.GetComponent<Movement>()); Assert.That(effect.SpeedMultiplier, Is.EqualTo(1f).Within(.01)); }
        [UnityTest] public IEnumerator SplashAndKnockUpdateBodyState() { needs.SetComposure(100); BodyEvents.Splash(root.GetComponent<Movement>(), BodyEvents.SplashKind.OtherMess); Assert.That(needs.Composure, Is.EqualTo(65)); needs.AddComfort(.5f); BodyEvents.Knock(root.GetComponent<Movement>()); Assert.That(needs.Comfort, Is.EqualTo(.48f).Within(.01)); yield return null; }
        [UnityTest] public IEnumerator VomitPausesNeedsAndCompletes() { dirt = new TestDirt(root.transform.position, 10); DirtField.Register(dirt); needs.SetComposure(0); yield return null; Assert.That(needs.Vomiting, Is.True); float before = needs.Urgency; yield return new WaitForSeconds(3f); Assert.That(needs.Vomiting, Is.False); Assert.That(needs.Composure, Is.EqualTo(50).Within(1)); Assert.That(needs.Urgency, Is.EqualTo(before).Within(5)); }
        [UnityTest] public IEnumerator SaboteurBowelRateIsHalf() { RoleRegistry.Set(root.GetComponent<Movement>(), PlayerRole.Saboteur); float before = needs.Bowel; yield return new WaitForSeconds(1); float gained = needs.Bowel - before; Assert.That(gained, Is.EqualTo(NeedsRules.BowelPerSecond * .5f).Within(.15f)); }

        private sealed class TestDirt : IDirtSource { public Vector3 DirtPosition { get; } public float DirtWeight { get; } public TestDirt(Vector3 position, float weight) { DirtPosition = position; DirtWeight = weight; } }
    }
}
