using NatureCalls.Core;
using NatureCalls.Facility;
using UnityEngine;
using System;

namespace NatureCalls.Player
{
    /// <summary>
    /// The player's Urgency and Disgust. Only the authority (the offline player, or the host for every networked
    /// player) changes them; other peers receive the values. They change only while a round is running.
    /// </summary>
    [RequireComponent(typeof(Posture))]
    public sealed class PlayerNeeds : MonoBehaviour, IMovementEffectSource
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
        public event Action<MessKind> OnAccident;
        public event Action OnRelieved;
        public event Action OnVomit;
        /// <summary>Set by whoever owns the round (offline Playground or the host); peers leave it off.</summary>
        public bool Ticking { get; private set; }
        public bool Failed => false;

        private Posture posture;
        private StallDoor unwipedStall;
        public bool Unwiped { get; private set; }
        public bool HandsDirty { get; private set; }
        public int PaperRequired { get; private set; }
        public Pipe UnwipedPipe { get; private set; }
        public bool Toileting { get; set; }
        public void MarkUnwiped(StallDoor stall) { unwipedStall = stall; Unwiped = true; }
        public void Wipe() { Unwiped = false; unwipedStall = null; }
        public void WipeByHand() { if (!Unwiped) return; Wipe(); HandsDirty = true; }
        public void SetPaperRequired(int units) { PaperRequired = System.Math.Max(0, units); }
        public void SetUnwipedPipe(Pipe pipe) { UnwipedPipe = pipe; }
        public void ConsumePaper(int units) { units = System.Math.Max(0, units); PaperRequired = System.Math.Max(0, PaperRequired - units); UnwipedPipe?.AddPaper(units); if (PaperRequired == 0) Wipe(); }
        public void WashHands() { HandsDirty = false; }
        public void LeaveUnwiped() { if (!Unwiped) return; AddDisgust(NeedsRules.UnwipedDisgust); Wipe(); }
        public void AddDisgust(float value) { if (Ticking) { Disgust += value; Composure = Mathf.Clamp(Composure - value, 0, NeedsRules.ComposureMax); } }
        public void AddComposure(float value) { if (Ticking) Composure = Mathf.Clamp(Composure + value, 0, NeedsRules.ComposureMax); }
        public void AddComfort(float value) { if (Ticking) Comfort = Mathf.Clamp01(Comfort + value); }
        /// <summary>Authority hook used by match/facility rules when control is lost.</summary>
        public void ForceAccident(bool bowel) => TriggerAccident(bowel ? MessKind.Accident : MessKind.Puddle);
        public void CompleteUrination() { if (Ticking) { Bladder = 0; Urgency = NeedsRules.Highest(Bladder, Bowel); ComfortBoostRemaining = 15; Comfort = Mathf.Clamp01(Comfort + .1f); OnRelieved?.Invoke(); } }


        private void Awake() => posture = GetComponent<Posture>();

        /// <summary>Starts this player's needs for a new round.</summary>
        public void BeginRound(float random01)
        {
            Urgency = NeedsRules.InitialUrgency(random01);
            Bladder = Urgency; Bowel = 0; Composure = NeedsRules.ComposureMax; Disgust = 0; Comfort = 0; InEndurance = false; Vomiting = false;
            Disgust = 0;
            HandsDirty = false;
            PaperRequired = 0;
            Ticking = true;
        }

        public void StopRound() => Ticking = false;

        private void Update()
        {
            var appearance = GetComponent<PlayerAppearance>();
            var bodyType = appearance != null ? appearance.BodyType : LocalProfile.BodyType;
            if (Ticking)
            {
                bool paused = posture.IsSquatting || Toileting || Vomiting;
                Bladder = NeedsRules.TickBladder(Bladder, Time.deltaTime, CharacterProfile.For(bodyType).UrgencyRateScale, paused);
                Bowel = NeedsRules.TickBowel(Bowel, Time.deltaTime, MatchContext.BowelMultiplier(GetComponent<Movement>()), CharacterProfile.For(bodyType).UrgencyRateScale, Comfort, RoleRegistry.IsSaboteur(GetComponent<Movement>()), paused, earlyBowelScale);
                Urgency = NeedsRules.Highest(Bladder, Bowel);
                if (!InEndurance && (Bladder >= 100 || Bowel >= 100)) { InEndurance = true; enduranceRemaining = NeedsRules.EnduranceDuration(UnityEngine.Random.value, 0, Comfort); }
                if (InEndurance) { enduranceRemaining -= Time.deltaTime; if (enduranceRemaining <= 0) { InEndurance = false; if (Bowel >= 100) TriggerAccident(MessKind.Accident); else Bladder = 30; } }
                if (ComfortBoostRemaining > 0) ComfortBoostRemaining -= Time.deltaTime;
                if (ComfortBoostRemaining > 0) AddComposure(3f * Time.deltaTime); else AddComposure(Time.deltaTime);
                if (Composure <= 0 && !Vomiting) { Vomiting = true; VomitProgress = 0; }
                if (Vomiting) TickVomit(Time.deltaTime);
            }
            if (!Ticking) return;
            if (Unwiped && unwipedStall != null && !unwipedStall.Contains(GetComponent<Movement>())) LeaveUnwiped();
            var world = FindFirstObjectByType<Map.Playground>();
            if (world != null) foreach (var mess in world.Messes)
                if (mess != null && mess.Kind == MessKind.Puddle)
                {
                    float amount = NeedsRules.PuddleDisgust(Vector3.ProjectOnPlane(transform.position - mess.transform.position, Vector3.up).magnitude, Time.deltaTime);
                    if (amount > 0) { AddDisgust(amount); break; }
                }
            Comfort = Mathf.Clamp01(Comfort);
        }

        /// <summary>A finished toileting clears Urgency; outside a stall it also adds Disgust.</summary>
        public void CompleteToileting(bool insideStall)
        {
            if (!Ticking) return;
            if (Bowel > 0) Bowel = 0; else Bladder = 0; Urgency = NeedsRules.Highest(Bladder, Bowel); ComfortBoostRemaining = 15; Comfort = Mathf.Clamp01(Comfort + .1f);
            Disgust = NeedsRules.DisgustAfterToileting(Disgust, insideStall);
            OnRelieved?.Invoke();
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
        public void ApplyVersusShared(float bladder, float bowel, float composure, bool endurance, bool vomiting, float vomitProgress, float comfortBoost)
        { Bladder = bladder; Bowel = bowel; Urgency = NeedsRules.Highest(bladder, bowel); Composure = composure; InEndurance = endurance; Vomiting = vomiting; VomitProgress = vomitProgress; ComfortBoostRemaining = comfortBoost; }

        private float enduranceRemaining, earlyBowelScale = 1f;
        public MovementModifier GetModifier(Movement player)
        {
            float high = NeedsRules.Highest(Bladder, Bowel);
            return new MovementModifier((NeedsRules.BlocksRun(high) ? .8f : 1f) * (InEndurance ? 1.25f : 1f) * (ComfortBoostRemaining > 0 ? 1.2f : 1f), 0, false, NeedsRules.BlocksJump(high));
        }
        public bool AppliesTo(Movement player) => player == GetComponent<Movement>();
        private void TriggerAccident(MessKind kind)
        {
            if (kind == MessKind.Accident) { Bowel = 30; earlyBowelScale = RoleRegistry.IsSaboteur(GetComponent<Movement>()) ? 1.3f : 1f; }
            else Bladder = 30;
            Urgency = NeedsRules.Highest(Bladder, Bowel);
            MessSinkRegistry.Current?.AddMess(kind == MessKind.Accident ? MessKind.Accident : MessKind.Puddle, transform.position, -1, GetComponent<Movement>());
            OnAccident?.Invoke(kind);
        }
        private void TickVomit(float seconds)
        {
            VomitProgress += seconds * (1f + Mathf.Min(2f, .5f * DirtField.Local(transform.position)));
            if (VomitProgress < 8f) return;
            Vomiting = false; VomitProgress = 0; Composure = 50; OnVomit?.Invoke();
            MessSinkRegistry.Current?.AddMess(MessKind.Vomit, transform.position, -1, GetComponent<Movement>());
        }
        private void OnEnable() { BodyEvents.Splashed += OnSplash; BodyEvents.Knocked += OnKnock; MovementEffectRegistry.Register(this); }
        private void OnDisable() { BodyEvents.Splashed -= OnSplash; BodyEvents.Knocked -= OnKnock; MovementEffectRegistry.Unregister(this); }
        private void OnSplash(Movement target, BodyEvents.SplashKind kind) { if (target != GetComponent<Movement>()) return; AddDisgust(kind == BodyEvents.SplashKind.OwnRebound ? 15 : 35); }
        private void OnKnock(Movement target) { if (target == GetComponent<Movement>()) Comfort = Mathf.Clamp01(Comfort - .02f); }
    }
}
