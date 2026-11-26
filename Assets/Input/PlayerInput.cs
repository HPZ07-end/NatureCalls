using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using NatureCalls.Core;
using NatureCalls.Player;
using NatureCalls.Facility;
using NatureCalls.Map;
using NatureCalls.Network;

namespace NatureCalls.Input
{
    [RequireComponent(typeof(Movement), typeof(Posture))]
    public sealed class PlayerInput : MonoBehaviour
    {
        private InputAction move, look, jump, toggle, urinate, interact, action, throwRoll, release;
        private Movement movement;
        private Posture posture;
        private FacilitySync facilities;
        private NeedsSync needsSync;
        private PlayerNeeds needs;
        public enum Stage { None, Squatting, Aiming, Urinating, Cleaning }
        public Stage Phase { get; private set; }
        /// <summary>Squat ↔ aim follows the head: bowed past <see cref="ToiletGaze.AimPitch"/> aims, looking back up leaves it.</summary>
        public void RefreshAimPhase()
        {
            if (Phase == Stage.Squatting && movement.Pitch >= ToiletGaze.AimPitch) { Phase = Stage.Aiming; if (needs != null) needs.Aiming = true; }
            else if (Phase == Stage.Aiming && movement.Pitch < ToiletGaze.AimPitch - 15f) { Phase = Stage.Squatting; if (needs != null) needs.Aiming = false; }
        }
        /// <summary>WASD moves the player except while aiming (squat look-down or urination); squatting still shuffles.</summary>
        public bool AcceptsWalking => Phase != Stage.Aiming && Phase != Stage.Urinating;
        /// <summary>V aims first; the stream starts only after C confirms.</summary>
        public bool UrineFlowing { get; private set; }
        public bool IsToileting => Phase == Stage.Squatting || Phase == Stage.Aiming || Phase == Stage.Urinating;
        public bool Unwiped => needs != null && needs.Unwiped;
        public Item HeldItem => Item.FindCarriedBy(movement);
        public int PaperRemainingUses => HeldItem is PaperRoll roll ? roll.RemainingUses : 0;
        public IInteractable Focus { get; private set; }
        public string FocusPrompt { get; private set; } = "";
        public string FPrompt => DressingRoomStool.CarriedBy(movement) != null ? "F  放下凳子" :
            Unwiped ? (HeldItem is PaperRoll ? "F  擦拭" : "F  用手擦拭") : HeldItem is Mop ? "F  长按清理" : "";
        public Vector3 TargetCenter => toilet != null ? toilet.TargetCenter : urinal != null ? urinal.TargetCenter : CurrentLanding;
        public float TargetRadius => NeedsRules.TargetRadius;
        /// <summary>The two axes the target circle lies in: the floor for a toilet, the wall for a urinal.</summary>
        public (Vector3 x, Vector3 y) TargetAxes
        {
            get
            {
                if (urinal == null) return (Vector3.right, Vector3.forward);
                var facing = Vector3.ProjectOnPlane(urinal.TargetCenter - urinal.FrontPosition, Vector3.up).normalized;
                return (Vector3.Cross(Vector3.up, facing), Vector3.up);
            }
        }
        public Vector3 CurrentLanding { get; private set; }
        public float Elapsed => Phase == Stage.Cleaning ? cleanElapsed : accuracy?.Elapsed ?? 0;
        public float Duration => Phase == Stage.Cleaning ? NeedsRules.CleaningSeconds : accuracy?.Duration ?? 0;
        public float CurrentAccuracy => accuracy?.Value ?? 0;
        public float CleaningProgress => Phase == Stage.Cleaning ? Mathf.Clamp01(cleanElapsed / NeedsRules.CleaningSeconds) : 0;
        private Toilet toilet;
        private Urinal urinal;
        private Accuracy accuracy;
        private Vector2 correction;
        private Vector2 noiseSeed;
        private Vector3 initialLanding;
        private float requested = -1, cleanElapsed, nextCleanReport;
        private IHoldInteractable heldInteract;
        private float heldInteractSeconds;
        private bool heldInteractDone;
        private bool urineMode, seatedUrine;
        private float heldToolSeconds;
        private Mess cleaning;
        private bool Online => facilities != null && facilities.IsSpawned;
        public bool CaptureMouse = true;
        public bool Captured => Cursor.lockState == CursorLockMode.Locked;
        private void Awake()
        {
            movement = GetComponent<Movement>();
            posture = GetComponent<Posture>();
            facilities = GetComponent<FacilitySync>();
            needsSync = GetComponent<NeedsSync>();
            needs = GetComponent<PlayerNeeds>();
            if (needs != null) needs.AutoToiletRequested += AutoToilet;
            ToiletingView.Attach(gameObject); // look-down, falling stool, urine arc, comfort sparkles

            move = new InputAction("Move", InputActionType.Value);
            move.AddCompositeBinding("2DVector").With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            look = new InputAction("Look", InputActionType.Value, "<Mouse>/delta");
            jump = new InputAction("Jump", InputActionType.Button, "<Keyboard>/space");
            toggle = new InputAction("Posture", InputActionType.Button, "<Keyboard>/c");
            urinate = new InputAction("Urinate", InputActionType.Button, "<Keyboard>/v");
            interact = new InputAction("Interact", InputActionType.Button, "<Keyboard>/e");
            action = new InputAction("Action", InputActionType.Button, "<Keyboard>/f");
            throwRoll = new InputAction("Throw Paper Roll", InputActionType.Button, "<Keyboard>/r");
            release = new InputAction("Release", InputActionType.Button, "<Keyboard>/escape");
        }
        private void OnEnable() { move?.Enable(); look?.Enable(); jump?.Enable(); toggle?.Enable(); urinate?.Enable(); interact?.Enable(); action?.Enable(); throwRoll?.Enable(); release?.Enable(); }
        private void OnDisable()
        {
            move?.Disable(); look?.Disable(); jump?.Disable(); toggle?.Disable(); urinate?.Disable(); interact?.Disable(); action?.Disable(); throwRoll?.Disable(); release?.Disable();
            CancelCleaning();
            Unlock();
        }
        private void OnDestroy() { if (needs != null) needs.AutoToiletRequested -= AutoToilet; move?.Dispose(); look?.Dispose(); jump?.Dispose(); toggle?.Dispose(); urinate?.Dispose(); interact?.Dispose(); action?.Dispose(); throwRoll?.Dispose(); release?.Dispose(); }
        private void AutoToilet(Toilet target)
        {
            if (target == null || Phase != Stage.None || !BeginGranted(target, null)) return;
            // Loss-of-control uses the same authority path but confirms immediately.
            Phase = Stage.Aiming; if (needs != null) needs.Aiming = true; FinishToileting();
        }
        public static void Unlock() { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
        private void OnApplicationFocus(bool focused) { if (!focused) Unlock(); }
        private void Update()
        {
            if (requested >= 0 && Time.time > requested + 2) requested = -1;
            RefreshAimPhase();
            if (!movement.Controlled)
            {
                // Connection-toilet water view: the player is parked and the cursor is free, so E presses the
                // flush button under the mouse pointer (the crosshair path below only runs while controlled).
                if (Application.isFocused && interact.WasPressedThisFrame()) TryFlushLobbyFromPointer();
                return;
            }
            // Esc leaves a toileting aim that has not started; once started it does nothing. Otherwise it frees the mouse.
            if (release.WasPressedThisFrame()) { if (!IsToileting) Unlock(); else CancelAim(); }
            if (CaptureMouse && Application.isFocused && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame &&
                !IsPointerOverUi())
            { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
            bool accept = Application.isFocused && (Captured || !CaptureMouse);
            if (accept)
            {
                RefreshFocus();
                if (!IsToileting) movement.Look(look.ReadValue<Vector2>());
                else
                {
                    // Toileting: the mouse always moves the first-person view (squatting or seated bodies ignore
                    // Movement.Look). Looking down far enough is the squat aim; the urine stream goes where you look.
                    var delta = look.ReadValue<Vector2>(); movement.transform.Rotate(Vector3.up, delta.x * movement.Sensitivity, Space.World);
                    movement.SetPitch(movement.Pitch - delta.y * movement.Sensitivity);
                }
                if (jump.WasPressedThisFrame() && !IsToileting) movement.Jump();
                if (toggle.WasPressedThisFrame()) TryToilet();
                if (urinate.WasPressedThisFrame()) TryUrinate();
                if (interact.WasPressedThisFrame()) { RefreshFocus(); if (Focus is not IHoldInteractable) TryInteract(); }
                var hold = Focus as IHoldInteractable;
                if (interact.IsPressed() && hold != null)
                {
                    if (heldInteract != hold) { heldInteract = hold; heldInteractSeconds = 0; heldInteractDone = false; }
                    if (!heldInteractDone) { heldInteractSeconds += Time.deltaTime; if (heldInteractSeconds >= hold.HoldSeconds) { heldInteractDone = Focus.Perform(Context()); } }
                }
                else { heldInteract = null; heldInteractSeconds = 0; heldInteractDone = false; }
                if (action.WasPressedThisFrame()) { RefreshFocus(); if (!(HeldItem is Wrench || HeldItem is Plunger)) TryAction(); }
                ContinueToolAction(action.IsPressed() && (HeldItem is Wrench || HeldItem is Plunger));
                if (throwRoll.WasPressedThisFrame()) TryThrowCarriedItem();
            }
            if (IsToileting)
            {
                // WASD shifts the body a little (posture): the squat landing follows the buttocks, and the
                // stream is aimed from the new spot along the view.
                if (accept) Shuffle(move.ReadValue<Vector2>(), Time.deltaTime);
                TickAim(Time.deltaTime, Vector2.zero);
            }
            if (Phase == Stage.Cleaning) ContinueCleaning(accept && action.IsPressed(), Time.deltaTime);
            movement.Move(accept && AcceptsWalking ? move.ReadValue<Vector2>() : Vector2.zero, Time.deltaTime);
        }

        private NatureCalls.UI.ToiletLobby toiletLobby;
        private bool toiletLobbySearched;
        /// <summary>E in the connection-toilet water view: flush the button under the mouse pointer.</summary>
        public bool TryFlushLobbyFromPointer()
        {
            if (!toiletLobbySearched) { toiletLobby = FindFirstObjectByType<NatureCalls.UI.ToiletLobby>(); toiletLobbySearched = true; }
            return toiletLobby != null && toiletLobby.Focused && toiletLobby.World.OfflinePlayer == movement && toiletLobby.TryFlushFromPointer();
        }

        private static bool IsPointerOverUi() => EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

        /// <summary>Re-resolves the crosshair (or proximity) focus now; Update does this every frame.</summary>
        public void RefreshFocus()
        {
            if (movement == null || movement.IsTraversing) { Focus = null; FocusPrompt = ""; return; }
            var context = Context();
            // First person: the crosshair (view ray). Seated, squatting, crawling or aiming: nearest by proximity.
            Ray? view = (posture.State == PostureState.Standing || IsToileting) && !movement.IsCrawling
                ? movement.ViewRay : null;
            Focus = InteractionRegistry.Resolve(context, view);
            if (Focus == null && posture.IsSquatting && movement.Seat != null)
                Focus = InteractionRegistry.All.Where(item => item is not ToiletFlushButton && item.CanInteract(context))
                    .OrderBy(item => Vector3.Distance(item.AimPoint, movement.transform.position)).FirstOrDefault();
            if (Focus == null && view.HasValue)
                Focus = FindObjectsByType<Toolbox>(FindObjectsSortMode.None)
                    .Where(toolbox => toolbox.CanInteract(context) && toolbox.IsTargetedBy(movement))
                    .OrderBy(toolbox => Vector3.Distance(toolbox.AimPoint, view.Value.origin))
                    .FirstOrDefault();
            FocusPrompt = Focus == null ? "" : Focus.Prompt(context);
        }

        private Vector3 ButtLanding()
        {
            var point = movement.transform.position - movement.transform.forward * NeedsRules.ButtOffset;
            point.y = toilet != null ? toilet.TargetCenter.y : movement.transform.position.y;
            return point;
        }
        public bool TryToilet()
        {
            if (needs != null && needs.Vomiting) return false;
            // Second C: the stool comes out now (looking down first is optional, it only shows the bowl).
            if (Phase == Stage.Squatting || Phase == Stage.Aiming) { FinishToileting(); return true; }
            if (Phase == Stage.Urinating) return false; // no pooping while peeing
            movement.RefreshVentPosture();
            if (IsToileting || movement.IsTraversing || posture.State != PostureState.Standing || requested >= 0 || Phase == Stage.Cleaning) return false;
            var stall = StallDoor.Containing(movement);
            if (stall != null && stall.Toilet != null)
            {
                toilet = stall.Toilet; urinal = null; requested = Time.time;
                bool started = Online ? facilities.RequestSeat(toilet) : BeginGranted(toilet, null);
                if (!started) requested = -1;
                return started;
            }
            toilet = null; urinal = null;
            foreach (var candidate in FindObjectsByType<Urinal>(FindObjectsSortMode.None))
                if (candidate.Contains(movement) && (urinal == null || Vector3.Distance(candidate.FrontPosition, transform.position) < Vector3.Distance(urinal.FrontPosition, transform.position))) urinal = candidate;
            if (urinal != null) return false; // urinals only accept V
            urineMode = false; requested = Time.time;
            bool startedOutside = Online ? facilities.RequestOutside() : BeginGranted(null, null);
            if (!startedOutside) requested = -1;
            return startedOutside;
        }
        public bool TryUrinate()
        {
            if (needs != null && needs.Vomiting) return false;
            // Second V: the stream starts and must run to the end.
            if (Phase == Stage.Urinating) { if (UrineFlowing) return false; UrineFlowing = true; return true; }
            // Peeing while squatting (on a toilet or on the floor) starts at once; afterwards the squat continues.
            if (Phase == Stage.Squatting || Phase == Stage.Aiming)
            {
                urineMode = true; seatedUrine = true; Phase = Stage.Urinating; UrineFlowing = true;
                accuracy = new Accuracy(NeedsRules.UrinatingSeconds); correction = Vector2.zero;
                if (needs != null) needs.Aiming = true;
                return true;
            }
            if (IsToileting || movement.IsTraversing || posture.State != PostureState.Standing || requested >= 0 || Phase == Stage.Cleaning) return false;
            toilet = null; urinal = null;
            var stall = StallDoor.Containing(movement);
            if (stall != null && stall.Toilet != null) toilet = stall.Toilet;
            else foreach (var candidate in FindObjectsByType<Urinal>(FindObjectsSortMode.None)) if (candidate.Contains(movement)) { urinal = candidate; break; }
            requested = Time.time; urineMode = true;
            bool started = Online ? (toilet != null ? facilities.RequestUrination(toilet) : urinal != null ? facilities.RequestUrinal(urinal) : facilities.RequestOutside()) : BeginGranted(toilet, urinal);
            if (!started) requested = -1;
            return started;
        }
        public bool BeginGranted(Toilet targetToilet, Urinal targetUrinal)
        {
            if (posture.IsCrawling) return false;
            toilet = targetToilet; urinal = targetUrinal; requested = -1; UrineFlowing = false;
            if (toilet != null && !toilet.Seat(movement)) return false;
            if (urinal != null && !urinal.Occupy(movement)) return false;
            Phase = (urineMode || targetUrinal != null) ? Stage.Urinating : Stage.Squatting;
            movement.Aiming = Phase == Stage.Urinating;
            movement.Immobile = false;
            if (Phase == Stage.Squatting) posture.Set(PostureState.Squatting);
            if (!Online && needs != null) needs.Toileting = true;
            accuracy = new Accuracy(Phase == Stage.Urinating ? NeedsRules.UrinatingSeconds : 9999f);
            initialLanding = urinal != null ? urinal.TargetCenter : ButtLanding();
            shuffleOrigin = movement.transform.position;
            CurrentLanding = initialLanding; correction = Vector2.zero;
            noiseSeed = new Vector2(Random.Range(0f, 100f), Random.Range(0f, 100f));
            if (needs != null) needs.Aiming = Phase == Stage.Urinating || Phase == Stage.Aiming;
            return true;
        }
        public void TickAim(float seconds, Vector2 adjustment)
        {
            if (!IsToileting) return;
            correction += adjustment;
            float t = accuracy.Elapsed * 2;
            var appearance = GetComponent<PlayerAppearance>();
            var profile = CharacterProfile.For(appearance != null ? appearance.BodyType : LocalProfile.BodyType);
            var drift = new Vector2(Mathf.PerlinNoise(noiseSeed.x, t) * 2 - 1, Mathf.PerlinNoise(noiseSeed.y, t) * 2 - 1) * NeedsRules.DriftAmplitude(needs != null ? needs.Urgency : 0) * (1f + (needs != null ? needs.AimJitterIntensity : 0)) * (GetComponent<ToiletGaze>()?.DriftScale ?? 1f) / Mathf.Max(.01f, profile.AimSteadiness);
            var offset = correction + drift;
            if (Phase == Stage.Urinating)
            {
                // Where you look is where it goes: the first solid surface on the view ray (not your own body).
                var ray = movement.ViewRay;
                var mask = Physics.DefaultRaycastLayers & ~(1 << gameObject.layer) & ~(1 << NatureCalls.Camera.CameraController.LocalBodyLayer);
                bool struck = Physics.Raycast(ray, out var hit, 3f, mask, QueryTriggerInteraction.Ignore);
                var aimed = struck ? hit.point : ray.GetPoint(1.5f);
                CurrentLanding = aimed + new Vector3(drift.x, 0, drift.y) * .5f;
                // In the target when the stream strikes the toilet / urinal itself near its opening (the bowl surface,
                // seat or porcelain), not a strict distance from the water centre.
                var owner = toilet != null ? toilet.transform : urinal != null ? urinal.transform : null;
                urineInside = struck && owner != null && (hit.transform.IsChildOf(owner) || (owner.parent != null && hit.transform.IsChildOf(owner.parent) && hit.transform.GetComponentInParent<StallDoor>() == null)) &&
                              Vector3.Distance(Vector3.ProjectOnPlane(CurrentLanding, Vector3.up), Vector3.ProjectOnPlane(TargetCenter, Vector3.up)) <= .45f;
            }
            else CurrentLanding = ButtLanding() + new Vector3(offset.x, 0, offset.y); // follows the shuffled body
            if (Phase == Stage.Urinating && !UrineFlowing) return; // still aiming: nothing comes out yet
            accuracy.Sample(seconds, Phase == Stage.Urinating ? urineInside : Vector3.Distance(CurrentLanding, TargetCenter) <= TargetRadius);
            if (accuracy.Complete) FinishToileting();
        }
        private bool urineInside;
        /// <summary>The latest squat release: where the stool left and whether it went into the bowl (for the falling-stool view).</summary>
        public Vector3 LastDropPoint { get; private set; }
        public bool LastDropInBowl { get; private set; }
        public int DropSerial { get; private set; }
        public const float ShuffleSpeed = .6f, SquatShuffleRadius = .25f, UrineShuffleRadius = .35f;
        private Vector3 shuffleOrigin;
        /// <summary>WASD while toileting: a small posture shift around where the player started.</summary>
        public void Shuffle(Vector2 keys, float seconds)
        {
            if (keys.sqrMagnitude < .0001f) return;
            float radius = Phase == Stage.Urinating && !seatedUrine ? UrineShuffleRadius : SquatShuffleRadius;
            var next = movement.transform.position + movement.transform.TransformDirection(new Vector3(keys.x, 0, keys.y)).normalized * ShuffleSpeed * seconds;
            var offset = Vector3.ProjectOnPlane(next - shuffleOrigin, Vector3.up);
            if (offset.magnitude > radius) next = shuffleOrigin + offset.normalized * radius + Vector3.up * (next.y - shuffleOrigin.y);
            var controller = GetComponent<CharacterController>();
            bool was = controller != null && controller.enabled; if (was) controller.enabled = false;
            movement.transform.position = next;
            if (was) controller.enabled = true;
            Physics.SyncTransforms();
        }
        /// <summary>The floor under an aim point (where a puddle or a dropped stool ends up).</summary>
        private Vector3 FloorUnder(Vector3 point)
        {
            var mask = Physics.DefaultRaycastLayers & ~(1 << gameObject.layer) & ~(1 << NatureCalls.Camera.CameraController.LocalBodyLayer);
            return Physics.Raycast(point + Vector3.up * .5f, Vector3.down, out var hit, 3f, mask, QueryTriggerInteraction.Ignore)
                ? hit.point : new Vector3(point.x, movement.transform.position.y, point.z);
        }
        /// <summary>Squat result at the moment of release: in the opening, on the rim, or past the bowl.</summary>
        private float ReleaseAccuracy()
        {
            if (toilet == null) return 0f;
            float distance = Vector3.Distance(Vector3.ProjectOnPlane(CurrentLanding, Vector3.up), Vector3.ProjectOnPlane(TargetCenter, Vector3.up));
            return distance <= TargetRadius ? 1f : distance <= TargetRadius * 1.6f ? (NeedsRules.PerfectAccuracy + NeedsRules.FloorAccuracy) / 2f : 0f;
        }
        private void FinishToileting()
        {
            // Messes land where the aim went (the floor under it), not at the player's feet.
            var position = FloorUnder(CurrentLanding);
            if (!urineMode && accuracy != null && accuracy.Duration > 1000f) accuracy = Accuracy.Fixed(ReleaseAccuracy());
            if (!urineMode) { LastDropPoint = CurrentLanding; LastDropInBowl = toilet != null && accuracy.Value >= NeedsRules.PerfectAccuracy; DropSerial++; }
            if (seatedUrine)
            {
                if (Online) facilities.ReportSeatedUrination(accuracy.Value, position);
                else ApplyUrination(movement, toilet, null, accuracy.Value, position);
                seatedUrine = false; urineMode = false; UrineFlowing = false;
                accuracy = new Accuracy(9999f); Phase = Stage.Squatting; if (needs != null) needs.Aiming = false;
                return; // still squatting: C poops, Esc stands up
            }
            if (Online) facilities.ReportResult(accuracy.Value, position);
            else if (urineMode) ApplyUrination(movement, toilet, urinal, accuracy.Value, position);
            else ApplyResult(movement, toilet, urinal, accuracy.Value, position, ToiletIndex(toilet));
            if (!Online) { toilet?.Release(movement); urinal?.Release(movement); if (needs != null) needs.Toileting = false; }
            // The completion RPC precedes posture replication, so the server can still validate its reservation.
            posture.Set(PostureState.Standing);
            movement.Immobile = false; movement.Aiming = false; if (needs != null) needs.Aiming = false; Phase = Stage.None; urineMode = false;
            toilet = null; urinal = null;
        }
        /// <summary>Esc: leave a squat, a look-down or a urine aim that has not started yet.</summary>
        public bool CancelAim()
        {
            if (Phase == Stage.Squatting || Phase == Stage.Aiming || (Phase == Stage.Urinating && !UrineFlowing)) { CancelToileting(); return true; }
            return false;
        }
        private void CancelToileting()
        {
            toilet?.Release(movement); urinal?.Release(movement);
            if (needs != null) { needs.Toileting = false; needs.Aiming = false; }
            posture.Set(PostureState.Standing); movement.Immobile = false; movement.Aiming = false; Phase = Stage.None; toilet = null; urinal = null; urineMode = false;
        }
        public static void ApplyUrination(Movement player, Toilet targetToilet, Urinal targetUrinal, float value, Vector3 position)
        {
            var playerNeeds = player.GetComponent<PlayerNeeds>(); if (playerNeeds == null || !playerNeeds.Ticking) return;
            playerNeeds.CompleteUrination();
            if (value < NeedsRules.UrinalAccuracy) MessSinkRegistry.Current?.AddMess(MessKind.Puddle, position);
        }
        public static int ToiletIndex(Toilet target)
        {
            if (target == null) return -1;
            if (MessSinkRegistry.Current != null) return MessSinkRegistry.Current.ToiletIndex(target);
            var toilets = FindObjectsByType<Toilet>(FindObjectsSortMode.InstanceID);
            return System.Array.IndexOf(toilets, target);
        }
        public static void ApplyResult(Movement player, Toilet targetToilet, Urinal targetUrinal, float value, Vector3 position, int toiletIndex)
        {
            var authority = player.GetComponent<FacilitySync>();
            if (authority != null && authority.IsSpawned && !authority.IsServer) return;
            var playerNeeds = player.GetComponent<PlayerNeeds>();
            var world = FindFirstObjectByType<Playground>();
            if (playerNeeds == null || !playerNeeds.Ticking) return;
            if (targetUrinal != null)
            {
                playerNeeds.CompleteUrination();
                world?.AddMess(NeedsRules.Urination(value), new Vector3(targetUrinal.FrontPosition.x, player.transform.position.y, targetUrinal.FrontPosition.z));
                return;
            }
            bool inStall = targetToilet != null;
            var stool = StoolOutput.Roll(player); // StoolKind, drawn before urgency clears
            playerNeeds.CompleteToileting(inStall);
            if (!inStall) { StoolOutput.Floor(player, position, stool); return; }
            if (targetToilet.IsBackflow || (world != null && world.IsBackflow(toiletIndex)))
            {
                StoolOutput.Floor(player, position, stool);
                playerNeeds.AddDisgust(NeedsRules.OutsideStallDisgust);
                return; // Backflow makes this equivalent to outside defecation: no paper or pipe contribution.
            }
            if (world != null && world.IsDirty(toiletIndex)) { playerNeeds.AddDisgust(NeedsRules.DirtyToiletDisgust); playerNeeds.AddComfort(-.02f); }
            var kind = NeedsRules.Defecation(value);
            if (kind == MessKind.Dropping) playerNeeds.AddDisgust(NeedsRules.OutsideStallDisgust);
            if (kind == MessKind.Dropping) StoolOutput.Floor(player, position, stool);
            else world?.AddMess(kind, kind == MessKind.DirtyToilet ? targetToilet.TargetCenter : position, toiletIndex);
            playerNeeds.MarkUnwiped(targetToilet.GetComponentInParent<StallDoor>());
            playerNeeds.SetPaperRequired(NeedsRules.PaperRequiredForSaboteur(kind, RoleRegistry.IsSaboteur(player)));
            playerNeeds.SetUnwipedPipe(targetToilet.ConnectedPipe);
            if (kind != MessKind.Dropping) StoolOutput.Bowl(targetToilet, stool);
        }
        public bool BeginCleaning(Mess mess)
        {
            if (IsToileting || mess == null || !mess.CanClean(movement)) return false;
            cleaning = mess; cleanElapsed = 0; Phase = Stage.Cleaning;
            if (Online) facilities.RequestClean(mess.Index, true);
            nextCleanReport = Time.time + .1f;
            return true;
        }
        public void ContinueCleaning(bool held, float seconds)
        {
            if (Phase != Stage.Cleaning) return;
            if (!held || cleaning == null || !cleaning.gameObject.activeSelf || !cleaning.CanClean(movement)) { CancelCleaning(); return; }
            cleanElapsed = NeedsRules.TickCleaning(cleanElapsed, seconds * (needs != null ? needs.CleaningSpeedMultiplier : 1f), true);
            if (Online)
            {
                if (Time.time >= nextCleanReport) { facilities.RequestClean(cleaning.Index, true); nextCleanReport = Time.time + .1f; }
                return; // Server removes the object; clients never award cleaning themselves.
            }
            if (cleanElapsed + .001f < NeedsRules.CleaningSeconds) return;
            FindFirstObjectByType<Playground>()?.RemoveMess(cleaning.Index);
            needs?.AddDisgust(NeedsRules.CleaningDisgust);
            CancelCleaning();
        }
        public void CancelCleaning()
        {
            if (Phase != Stage.Cleaning) return;
            if (Online) facilities.RequestClean(-1, false);
            cleaning = null; cleanElapsed = 0; Phase = Stage.None;
        }

        private InteractionContext Context() => new InteractionContext(movement, Online ? facilities.OwnerClientId : 0, Online, facilities, Phase != Stage.None);

        /// <summary>E performs the facility selected by the crosshair (first person) or by proximity (seated, squatting, crawling, aiming).</summary>
        public bool TryInteract()
        {
            if (movement.IsTraversing) return false;
            RefreshFocus();
            if (Focus == null)
            {
                var context = Context();
                Focus = InteractionRegistry.All.Where(item => item is Item && item.CanInteract(context))
                    .OrderBy(item => Vector3.Distance(item.AimPoint, movement.transform.position)).FirstOrDefault();
            }
            if (Focus == null) return false;
            if (needs != null && needs.Vomiting && Focus is not StallDoor && Focus is not Toilet) return false;
            if (!Focus.Perform(Context())) return false;
            GetComponent<PlayerAppearance>()?.PlayReach();
            var avatar = GetComponent<Network.Avatar>();
            if (avatar != null && avatar.IsSpawned && avatar.IsOwner) avatar.PlayReachRpc();
            return true;
        }

        public bool TryAction()
        {
            if (needs != null && needs.Vomiting) return false;
            var stool = DressingRoomStool.CarriedBy(movement);
            if (stool != null) return stool.Place(movement);
            if (Unwiped)
            {
                if (HeldItem is PaperRoll paper)
                {
                    if (Online) return facilities.RequestWipe();
                    int amount = Mathf.Min(needs.PaperRequired, paper.RemainingUses);
                    if (amount <= 0 || !paper.Use(amount)) return false;
                    needs.ConsumePaper(amount); return true;
                }
                if (Online) return facilities.RequestWipe();
                needs?.WipeByHand();
                return true;
            }
            if (HeldItem is Mop && Phase != Stage.Cleaning)
            {
                var ray = movement.ViewRay;
                var nearest = FindObjectsByType<Mess>(FindObjectsSortMode.None).Where(m => m.CanClean(movement))
                    .OrderBy(m => Vector3.Cross(ray.direction, m.transform.position - ray.origin).sqrMagnitude).FirstOrDefault();
                if (nearest != null) return BeginCleaning(nearest);
            }
            return false;
        }

        private void ContinueToolAction(bool held)
        {
            if (!held || Focus == null) { heldToolSeconds = 0; return; }
            float duration = HeldItem is Wrench ? 3f : 4f; // 暂定
            heldToolSeconds += Time.deltaTime * ToolSpeed.Multiplier(movement);
            if (heldToolSeconds < duration) return;
            bool done;
            if (Online && Focus is Breakable || Online && Focus is Toilet) done = facilities.RequestInteract(Focus);
            else done = Focus is Breakable broken ? RepairTools.Repair(movement, broken) : Focus is Toilet toilet ? RepairTools.Clear(movement, toilet) : false;
            if (done) heldToolSeconds = 0;
        }

        private bool TryThrowCarriedItem()
        {
            if (needs != null && needs.Vomiting) return false;
            var carried = Item.FindCarriedBy(movement);
            return carried != null && (facilities != null && facilities.IsSpawned
                ? facilities.RequestThrow(carried, movement.transform.forward) : carried.Throw(movement, movement.transform.forward));
        }
    }
}
