using NatureCalls.Core;
using NatureCalls.Facility;
using UnityEngine;
using System;
using System.Collections.Generic;

namespace NatureCalls.Player
{
    /// <summary>
    /// The player's Urgency and Disgust. Only the authority (the offline player, or the host for every networked
    /// player) changes them; other peers receive the values. They change only while a round is running.
    /// </summary>
    [RequireComponent(typeof(Posture))]
    public sealed class PlayerNeeds : MonoBehaviour, IMovementEffectSource, IForcedInputSource
    {
        public float Urgency { get; private set; }
        public float Bladder { get; private set; }
        public float Bowel { get; private set; }
        public float Composure { get; private set; } = NeedsRules.ComposureMax;
        [Obsolete("Use Composure; retained for legacy HUD and saves.")] public float Disgust { get; private set; }
        public float Comfort { get; private set; }
        public float ComfortIntensity => Mathf.Clamp01(Comfort);
        public bool InEndurance { get; private set; }
        public float EnduranceIntensity => InEndurance ? 1f : 0f;
        public bool Vomiting { get; private set; }
        public float VomitProgress { get; private set; }
        public float ComfortBoostRemaining { get; private set; }
        public int BladderBand => NeedsBand(Bladder);
        public int BowelBand => NeedsBand(Bowel);
        public float BladderUrgency => Bladder;
        public float BowelUrgency => Bowel;
        public float NarrowingIntensity => NeedsRules.ViewNarrowing(Urgency);
        public float ViewNarrowingIntensity => NarrowingIntensity;
        public float AimJitterIntensity => Mathf.Clamp01((Urgency - 70f) / 30f) + (InEndurance ? .5f : 0f);
        public bool Nauseous => Vomiting;
        public float VomitCountdownProgress => Mathf.Clamp01(VomitProgress / 8f);
        public float ComfortPresentationIntensity => ComfortIntensity;
        public bool ComfortBoostActive => ComfortBoostRemaining > 0;
        public bool IsOutOfControl { get; private set; }
        public float OutOfControlIntensity => IsOutOfControl ? 1f : 0f;
        public event Action<Toilet> AutoToiletRequested;
        public event Action<MessKind> OnAccident;
        public event Action OnRelieved;
        public event Action OnVomit;
        /// <summary>Set by whoever owns the round (offline Playground or the host); peers leave it off.</summary>
        public bool Ticking { get; private set; }
        public bool Failed => !MatchContext.Versus && NeedsRules.Failed(Urgency, Disgust);

        private Posture posture;
        private StallDoor unwipedStall;
        public bool Unwiped { get; private set; }
        public bool HandsDirty { get; private set; }
        public int PaperRequired { get; private set; }
        public Pipe UnwipedPipe { get; private set; }
        public bool Toileting { get; set; }
        /// <summary>Owner-only read flag used by needs ticking and HUD.</summary>
        public bool Aiming { get; set; }
        public StoolKind NextStoolKind { get; private set; } = StoolKind.Soft;
        // PlayMode hook: lets tests isolate contact and cleaning costs from the vision channel.
        public bool VisionSuppressedForTests { get; set; }
        public void MarkUnwiped(StallDoor stall) { unwipedStall = stall; Unwiped = true; }
        public void Wipe() { Unwiped = false; unwipedStall = null; }
        public void WipeByHand() { if (!Unwiped) return; Wipe(); HandsDirty = true; }
        public void SetPaperRequired(int units) { PaperRequired = System.Math.Max(0, units); }
        public void SetUnwipedPipe(Pipe pipe) { UnwipedPipe = pipe; }
        public void ConsumePaper(int units) { units = System.Math.Max(0, units); PaperRequired = System.Math.Max(0, PaperRequired - units); UnwipedPipe?.AddPaper(units); AddComfort(2f / 100f); if (PaperRequired == 0) Wipe(); }
        public void WashHands() { HandsDirty = false; AddComfort(1f / 100f); }
        public void LeaveUnwiped() { if (!Unwiped) return; AddDisgust(NeedsRules.UnwipedDisgust); Wipe(); }
        public void AddDisgust(float value) { if (Ticking) { Disgust += value; Composure = Mathf.Clamp(Composure - value, 0, NeedsRules.ComposureMax); } }
        public void AddComposure(float value) { if (Ticking) Composure = Mathf.Clamp(Composure + value, 0, NeedsRules.ComposureMax); }
        public void AddComfort(float value) { if (Ticking) { Comfort = Mathf.Clamp01(Comfort + value); comfortWindowRemaining = NeedsRules.ComfortWindowSeconds; } }
        public void SetComposure(float value) { Composure = Mathf.Clamp(value, 0, NeedsRules.ComposureMax); }
        public void SetVersusState(float bladder, float bowel) { Bladder = Mathf.Clamp(bladder, 0, 100); Bowel = Mathf.Clamp(bowel, 0, 100); Urgency = NeedsRules.Highest(Bladder, Bowel); }
        /// <summary>Authority hook used by match/facility rules when control is lost.</summary>
        public void ForceAccident(bool bowel) => TriggerAccident(bowel ? MessKind.Accident : MessKind.Puddle);
        public void CompleteUrination() { if (Ticking) { if (MatchContext.Versus) { Bladder = 0; Urgency = NeedsRules.Highest(Bladder, Bowel); } else { Urgency = NeedsRules.UrgencyAfterUrination(Urgency); Bladder = Urgency; } /* versus: the bladder empties even when the bowel is fuller */ ComfortBoostRemaining = 15; Comfort = Mathf.Clamp01(Comfort + .1f); OnRelieved?.Invoke(); } }


        private void Awake() => posture = GetComponent<Posture>();

        /// <summary>Starts this player's needs for a new round.</summary>
        public void BeginRound(float random01)
        {
            Urgency = NeedsRules.InitialUrgency(random01);
            Bladder = Urgency; Bowel = 0; Composure = NeedsRules.ComposureMax; Disgust = 0; Comfort = 0; InEndurance = false; Vomiting = false; IsOutOfControl = false; earlyBowelScale = 1f; comfortWindowRemaining = 0;
            Disgust = 0;
            HandsDirty = false;
            PaperRequired = 0;
            Ticking = true;
            RollNextStool();
        }

        public void StopRound() => Ticking = false;

        private void Update()
        {
            var appearance = GetComponent<PlayerAppearance>();
            var bodyType = appearance != null ? appearance.BodyType : LocalProfile.BodyType;
            if (Ticking)
            {
                float bowelBefore = Bowel;
                bool paused = (posture.IsSquatting || Toileting || Vomiting) && !Aiming;
                float aimingRate = Aiming ? NeedsRules.AimingUrgencyRate : 1f;
                if (!MatchContext.Versus)
                {
                    Urgency = NeedsRules.TickUrgency(Urgency, Time.deltaTime, false, CharacterProfile.For(bodyType).UrgencyRateScale * aimingRate);
                    Bladder = Urgency;
                }
                else
                {
                Bladder = NeedsRules.TickBladder(Bladder, Time.deltaTime, CharacterProfile.For(bodyType).UrgencyRateScale * aimingRate, false);
                Bowel = NeedsRules.TickBowel(Bowel, Time.deltaTime, MatchContext.BowelMultiplier(GetComponent<Movement>()), CharacterProfile.For(bodyType).UrgencyRateScale * aimingRate, Comfort, RoleRegistry.IsSaboteur(GetComponent<Movement>()), false, earlyBowelScale);
                if (bowelBefore <= 0f && Bowel > 0f) RollNextStool();
                if (Bowel >= NeedsRules.UrgencyMax && bowelBefore < NeedsRules.UrgencyMax) RollNextStool();
                if (comfortWindowRemaining > 0) comfortWindowRemaining -= Time.deltaTime; else Comfort = 0;
                if (earlyScaleRemaining > 0) { earlyScaleRemaining -= Time.deltaTime; if (earlyScaleRemaining <= 0) earlyBowelScale = 1f; }
                Urgency = NeedsRules.Highest(Bladder, Bowel);
                if (!InEndurance && !IsOutOfControl && (Bladder >= 100 || Bowel >= 100)) { InEndurance = true; enduranceRemaining = NeedsRules.EnduranceDuration(UnityEngine.Random.value, CharacterProfile.For(bodyType).EnduranceBonus, Comfort); }
                if (InEndurance) { enduranceRemaining -= Time.deltaTime; if (enduranceRemaining <= 0) { InEndurance = false; BeginLossOfControl(); } }
                if (ComfortBoostRemaining > 0) ComfortBoostRemaining -= Time.deltaTime;
                if (!Vomiting)
                {
                    if (ComfortBoostRemaining > 0) AddComposure(3f * Time.deltaTime);
                    else if (Composure > 0 && !VisionSuppressedForTests && DirtField.Local(transform.position) <= 0) AddComposure(Time.deltaTime);
                }
                if (Composure <= 0 && !Vomiting) { Vomiting = true; VomitProgress = 0; }
                if (Vomiting) TickVomit(Time.deltaTime);
                }
            }
            if (!Ticking) return;
            TickVision(Time.deltaTime);
            if (Unwiped && unwipedStall != null && !unwipedStall.Contains(GetComponent<Movement>())) LeaveUnwiped();
            if (Toileting && Bowel > 0)
            {
                if (GetComponent<Movement>().Pitch > 45f) AddComfort(Time.deltaTime / 100f);
                if (GetComponent<Movement>().LastInputMagnitude < .05f) AddComfort(.5f * Time.deltaTime / 100f);
            }
            if (stunRemaining > 0) { stunRemaining -= Time.deltaTime; GetComponent<Movement>().Immobile = true; if (stunRemaining <= 0 && !IsOutOfControl) GetComponent<Movement>().Immobile = false; }
            if (IsOutOfControl) TickLossOfControl(Time.deltaTime);
            Comfort = Mathf.Clamp01(Comfort);
        }

        /// <summary>A finished toileting clears Urgency; outside a stall it also adds Disgust.</summary>
        public void CompleteToileting(bool insideStall)
        {
            if (!Ticking) return;
            if (MatchContext.Versus) { if (Bowel > 0) Bowel = 0; else Bladder = 0; Urgency = NeedsRules.Highest(Bladder, Bowel); }
            else { Urgency = 0; Bladder = 0; Bowel = 0; }
            ComfortBoostRemaining = 15; Comfort = Mathf.Clamp01(Comfort + .1f);
            Disgust = NeedsRules.DisgustAfterToileting(Disgust, insideStall);
            if (insideStall) AddComfort(.1f);
            OnRelieved?.Invoke();
        }

        public void RollNextStool()
        {
            float urgency01 = (MatchContext.Versus ? Bowel : Urgency) / NeedsRules.UrgencyMax;
            NextStoolKind = StoolRules.Roll(RoleRegistry.IsSaboteur(GetComponent<Movement>()), urgency01,
                IsOutOfControl || InEndurance, Comfort, UnityEngine.Random.value);
        }

        /// <summary>Peers show the authority's values.</summary>
        public void ApplyShared(float urgency, float disgust, bool unwiped = false, bool handsDirty = false, int paperRequired = 0)
        {
            Urgency = urgency;
            Disgust = disgust;
            Unwiped = unwiped;
            HandsDirty = handsDirty;
            PaperRequired = System.Math.Max(0, paperRequired);
            Bladder = urgency; Bowel = 0; Composure = Mathf.Clamp(NeedsRules.ComposureMax - disgust, 0, NeedsRules.ComposureMax);
        }
        public void ApplyVersusShared(float bladder, float bowel, float composure, bool endurance, bool vomiting, float vomitProgress, float comfortBoost, float comfort = 0, bool outOfControl = false)
        { Bladder = bladder; Bowel = bowel; Urgency = NeedsRules.Highest(bladder, bowel); Composure = composure; InEndurance = endurance; Vomiting = vomiting; VomitProgress = vomitProgress; ComfortBoostRemaining = comfortBoost; Comfort = comfort; IsOutOfControl = outOfControl; }

        private float enduranceRemaining, earlyBowelScale = 1f, earlyScaleRemaining, comfortWindowRemaining, stunRemaining, lossControlElapsed, twistTimer, twistDuration, twistAngle;
        private Toilet lossTarget;
        private readonly Dictionary<IDirtSource, float> seenDirt = new();
        private float visionElapsed;
        private static int NeedsBand(float value) => value >= 90 ? 3 : value >= 70 ? 2 : value >= 40 ? 1 : 0;
        public float CleaningSpeedMultiplier => InEndurance ? 1.4f : 1f;
        public MovementModifier GetModifier(Movement player)
        {
            float high = NeedsRules.Highest(Bladder, Bowel);
            return new MovementModifier((NeedsRules.BlocksRun(high) ? .8f : 1f) * (InEndurance ? 1.25f : 1f) * (ComfortBoostRemaining > 0 ? 1.2f : 1f), 0, false, NeedsRules.BlocksJump(high));
        }
        public bool AppliesTo(Movement player) => player == GetComponent<Movement>();
        public bool ModifyInput(Movement player, ref Vector2 input)
        {
            bool changed = false;
            if (IsOutOfControl && lossTarget != null)
            {
                Vector3 desired = Vector3.ProjectOnPlane(lossTarget.transform.position - player.transform.position, Vector3.up).normalized;
                Vector3 mixed = Vector3.Lerp(desired, player.transform.TransformDirection(new Vector3(input.x, 0, input.y)), NeedsRules.LossOfControlInputWeight).normalized;
                Vector3 local = player.transform.InverseTransformDirection(mixed); input = new Vector2(local.x, local.z); changed = true;
            }
            if (InEndurance)
            {
                twistTimer += Time.deltaTime;
                if (twistDuration <= 0 && twistTimer >= UnityEngine.Random.Range(2f, 4f)) { twistAngle = UnityEngine.Random.Range(-20f, 20f); twistDuration = .5f; twistTimer = 0; }
                if (twistDuration > 0) { twistDuration -= Time.deltaTime; float radians = twistAngle * Mathf.Deg2Rad; input = new Vector2(input.x * Mathf.Cos(radians) - input.y * Mathf.Sin(radians), input.x * Mathf.Sin(radians) + input.y * Mathf.Cos(radians)); changed = true; }
            }
            return changed;
        }
        private void BeginLossOfControl()
        {
            var movement = GetComponent<Movement>(); var toilets = FindObjectsByType<Toilet>(FindObjectsSortMode.None);
            lossTarget = null; float nearest = float.MaxValue;
            foreach (var toilet in toilets) if (toilet.IsAvailable) { float d = Vector3.Distance(movement.transform.position, toilet.TargetCenter); if (d < nearest) { nearest = d; lossTarget = toilet; } }
            float p = lossTarget == null ? 1f : NeedsRules.LossOfControlProbability(Mathf.Clamp01(nearest / 20f), movement.Profile.Control);
            bool local = UnityEngine.Random.value < p || lossTarget == null;
            IsOutOfControl = true; lossControlElapsed = 0;
            if (local) lossTarget = null;
        }
        private void TickLossOfControl(float seconds)
        {
            lossControlElapsed += seconds;
            if (lossTarget != null)
            {
                if (Vector3.Distance(transform.position, lossTarget.TargetCenter) < 1.2f) { IsOutOfControl = false; AutoToiletRequested?.Invoke(lossTarget); lossTarget = null; return; }
                if (lossControlElapsed < NeedsRules.LossOfControlTimeout) return;
            }
            IsOutOfControl = false; lossTarget = null; GetComponent<Movement>().Immobile = true; stunRemaining = 3f;
            TriggerAccident(Bowel >= Bladder ? MessKind.Accident : MessKind.Puddle);
        }
        private void TickVision(float seconds)
        {
            if (VisionSuppressedForTests) return;
            visionElapsed += seconds; if (visionElapsed < NeedsRules.VisionInterval) return; float interval = visionElapsed; visionElapsed = 0;
            var movement = GetComponent<Movement>(); if (movement == null) return;
            var sync = GetComponent<Network.FacilitySync>(); if (sync != null && sync.IsSpawned && !sync.IsServer) return;
            float visibleWeight = 0; var visible = new HashSet<IDirtSource>();
            foreach (var source in DirtField.Sources)
            {
                if (source == null || source.DirtWeight <= 0) continue;
                Vector3 delta = source.DirtPosition - movement.ViewOrigin; float distance = delta.magnitude;
                if (distance > NeedsRules.VisionRadius) continue;
                Vector3 direction = delta / Mathf.Max(.001f, distance);
                if (Vector3.Dot(movement.ViewRotation * Vector3.forward, direction) < .55f) continue;
                if (Physics.Linecast(movement.ViewOrigin, source.DirtPosition, out var hit) && !hit.transform.IsChildOf(transform)) continue;
                visible.Add(source); visibleWeight += source.DirtWeight;
                seenDirt[source] = seenDirt.TryGetValue(source, out float age) ? age + interval : interval;
            }
            float loss = NeedsRules.VisionComposureLoss(visibleWeight, interval, false);
            foreach (var source in visible) if (seenDirt[source] > 5f) loss -= source.DirtWeight * NeedsRules.VisionRate * interval * .5f;
            float local = 0; foreach (var source in DirtField.Sources) if (source != null && source.DirtWeight > 0 && Vector3.Distance(transform.position, source.DirtPosition) <= NeedsRules.GroundMessRadius) local += source.DirtWeight;
            if (local > 0) loss += NeedsRules.GroundComposureRate * interval;
            if (visible.Count == 0 && local <= 0) AddComposure(interval); else AddDisgust(loss);
        }
        private void TriggerAccident(MessKind kind)
        {
            if (kind == MessKind.Accident) { Bowel = 30; earlyBowelScale = RoleRegistry.IsSaboteur(GetComponent<Movement>()) ? 1.3f : 1f; earlyScaleRemaining = 60f; }
            else Bladder = 30;
            Urgency = NeedsRules.Highest(Bladder, Bowel);
            MessSinkRegistry.Current?.AddMess(kind == MessKind.Accident ? MessKind.Accident : MessKind.Puddle, transform.position, -1, GetComponent<Movement>());
            OnAccident?.Invoke(kind);
        }
        private void TickVomit(float seconds)
        {
            VomitProgress += seconds * (1f + Mathf.Min(2f, .5f * DirtField.Local(transform.position)));
            if (VomitProgress < 8f) return;
            Vomiting = false; VomitProgress = 0; Composure = 50.1f; OnVomit?.Invoke();
            MessSinkRegistry.Current?.AddMess(MessKind.Vomit, transform.position, -1, GetComponent<Movement>());
            foreach (var player in FindObjectsByType<Movement>(FindObjectsSortMode.None)) if (player != GetComponent<Movement>() && Vector3.Distance(player.transform.position, transform.position) <= 1.5f) BodyEvents.Splash(player, BodyEvents.SplashKind.Vomit);
        }
        private void OnEnable() { BodyEvents.Splashed += OnSplash; BodyEvents.Knocked += OnKnock; }
        private void OnDisable() { BodyEvents.Splashed -= OnSplash; BodyEvents.Knocked -= OnKnock; MovementEffectRegistry.Unregister(this); }
        private void OnSplash(Movement target, BodyEvents.SplashKind kind) { if (target != GetComponent<Movement>()) return; AddDisgust(kind == BodyEvents.SplashKind.OwnRebound ? 15 : 35); }
        private void OnKnock(Movement target) { if (target == GetComponent<Movement>()) Comfort = Mathf.Clamp01(Comfort - .02f); }
    }

    /// <summary>Shared tool-speed hook for sabotage and cleaning tools.</summary>
    public static class ToolSpeed
    {
        public static float Multiplier(Movement player) => player != null && player.GetComponent<PlayerNeeds>()?.InEndurance == true ? 1.4f : 1f;
    }
}
