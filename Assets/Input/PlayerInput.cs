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
        private InputAction move, look, jump, toggle, interact, action, throwRoll, release;
        private Movement movement;
        private Posture posture;
        private FacilitySync facilities;
        private NeedsSync needsSync;
        private PlayerNeeds needs;
        public enum Stage { None, Aligning, Holding, Urinating, Cleaning }
        public Stage Phase { get; private set; }
        public bool IsToileting => Phase == Stage.Holding || Phase == Stage.Urinating;
        public bool Unwiped => needs != null && needs.Unwiped;
        public Item HeldItem => Item.FindCarriedBy(movement);
        public int PaperRemainingUses => HeldItem is PaperRoll roll ? roll.RemainingUses : 0;
        public IInteractable Focus { get; private set; }
        public string FocusPrompt { get; private set; } = "";
        public string FPrompt => Unwiped ? (HeldItem is PaperRoll ? "F  擦拭" : "F  用手擦拭") : HeldItem is Mop ? "F  长按清理" : "";
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

            move = new InputAction("Move", InputActionType.Value);
            move.AddCompositeBinding("2DVector").With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            look = new InputAction("Look", InputActionType.Value, "<Mouse>/delta");
            jump = new InputAction("Jump", InputActionType.Button, "<Keyboard>/space");
            toggle = new InputAction("Posture", InputActionType.Button, "<Keyboard>/c");
            interact = new InputAction("Interact", InputActionType.Button, "<Keyboard>/e");
            action = new InputAction("Action", InputActionType.Button, "<Keyboard>/f");
            throwRoll = new InputAction("Throw Paper Roll", InputActionType.Button, "<Keyboard>/r");
            release = new InputAction("Release", InputActionType.Button, "<Keyboard>/escape");
        }
        private void OnEnable() { move?.Enable(); look?.Enable(); jump?.Enable(); toggle?.Enable(); interact?.Enable(); action?.Enable(); throwRoll?.Enable(); release?.Enable(); }
        private void OnDisable()
        {
            move?.Disable(); look?.Disable(); jump?.Disable(); toggle?.Disable(); interact?.Disable(); action?.Disable(); throwRoll?.Disable(); release?.Disable();
            CancelCleaning();
            Unlock();
        }
        private void OnDestroy() { move?.Dispose(); look?.Dispose(); jump?.Dispose(); toggle?.Dispose(); interact?.Dispose(); action?.Dispose(); throwRoll?.Dispose(); release?.Dispose(); }
        public static void Unlock() { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
        private void OnApplicationFocus(bool focused) { if (!focused) Unlock(); }
        private void Update()
        {
            if (requested >= 0 && Time.time > requested + 2) requested = -1;
            if (Phase == Stage.Aligning)
            {
                if (toilet == null || !toilet.GetComponentInParent<StallDoor>().Contains(movement))
                { Phase = Stage.None; movement.Aiming = false; toilet = null; }
                else CurrentLanding = ButtLanding();
            }
            if (!movement.Controlled) return;
            if (release.WasPressedThisFrame()) Unlock();
            if (CaptureMouse && Application.isFocused && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame &&
                !IsPointerOverUi())
            { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
            bool accept = Application.isFocused && (Captured || !CaptureMouse);
            if (accept)
            {
                ResolveFocus();
                if (!IsToileting) movement.Look(look.ReadValue<Vector2>());
                if (jump.WasPressedThisFrame()) movement.Jump();
                if (toggle.WasPressedThisFrame()) TryToilet();
                if (interact.WasPressedThisFrame()) TryInteract();
                if (action.WasPressedThisFrame()) TryAction();
                if (throwRoll.WasPressedThisFrame()) TryThrowCarriedItem();
            }
            if (IsToileting)
            {
                var keys = move.ReadValue<Vector2>();
                var worldMove = movement.transform.TransformDirection(new Vector3(keys.x, 0, keys.y));
                var adjustment = Phase == Stage.Urinating ? look.ReadValue<Vector2>() * NeedsRules.MouseAimScale :
                    new Vector2(worldMove.x, worldMove.z) * NeedsRules.CorrectionSpeed * Time.deltaTime;
                TickAim(Time.deltaTime, accept ? adjustment : Vector2.zero);
            }
            if (Phase == Stage.Cleaning) ContinueCleaning(accept && action.IsPressed(), Time.deltaTime);
            movement.Move(accept ? move.ReadValue<Vector2>() : Vector2.zero, Time.deltaTime);
        }

        private static bool IsPointerOverUi() => EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

        private void ResolveFocus()
        {
            if (movement == null || movement.IsTraversing || Phase != Stage.None) { Focus = null; FocusPrompt = ""; return; }
            var context = new InteractionContext(movement, Online ? facilities.OwnerClientId : 0, Online, facilities);
            // First person: the crosshair (view ray). Seated, squatting, crawling or aiming: nearest by proximity.
            Ray? view = posture.State == PostureState.Standing && movement.Seat == null && !movement.IsCrawling && !movement.Aiming
                ? movement.ViewRay : null;
            Focus = InteractionRegistry.Resolve(context, view);
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
            movement.RefreshVentPosture();
            if (IsToileting || movement.IsTraversing || posture.State != PostureState.Standing || requested >= 0 || Phase == Stage.Cleaning) return false;
            var stall = StallDoor.Containing(movement);
            if (Phase != Stage.Aligning && stall != null && stall.Toilet != null)
            {
                toilet = stall.Toilet; Phase = Stage.Aligning; movement.Aiming = true;
                CurrentLanding = ButtLanding(); accuracy = null; return true;
            }
            if (Phase == Stage.Aligning && (stall == null || stall.Toilet != toilet)) return false;
            urinal = null;
            if (Phase != Stage.Aligning)
            {
                toilet = null;
                foreach (var candidate in FindObjectsByType<Urinal>(FindObjectsSortMode.None))
                    if (candidate.Contains(movement) && (urinal == null || Vector3.Distance(candidate.FrontPosition, transform.position) < Vector3.Distance(urinal.FrontPosition, transform.position))) urinal = candidate;
            }
            requested = Time.time;
            bool started = Online ? (toilet != null ? facilities.RequestSeat(toilet) : urinal != null ? facilities.RequestUrinal(urinal) : facilities.RequestOutside()) : BeginGranted(toilet, urinal);
            if (!started) requested = -1;
            return started;
        }
        public bool BeginGranted(Toilet targetToilet, Urinal targetUrinal)
        {
            if (posture.IsCrawling) return false;
            toilet = targetToilet; urinal = targetUrinal; requested = -1;
            if (toilet != null && !toilet.Seat(movement)) return false;
            if (urinal != null && !urinal.Occupy(movement)) return false;
            Phase = urinal != null ? Stage.Urinating : Stage.Holding;
            movement.Aiming = toilet != null;
            movement.Immobile = urinal != null;
            if (urinal == null) posture.Set(PostureState.Squatting);
            if (!Online && needs != null) needs.Toileting = true;
            accuracy = new Accuracy(urinal != null ? NeedsRules.UrinatingSeconds : NeedsRules.ToiletingSeconds);
            initialLanding = urinal != null ? urinal.TargetCenter : ButtLanding();
            CurrentLanding = initialLanding; correction = Vector2.zero;
            noiseSeed = new Vector2(Random.Range(0f, 100f), Random.Range(0f, 100f));
            return true;
        }
        public void TickAim(float seconds, Vector2 adjustment)
        {
            if (!IsToileting) return;
            correction += adjustment;
            float t = accuracy.Elapsed * 2;
            var drift = new Vector2(Mathf.PerlinNoise(noiseSeed.x, t) * 2 - 1, Mathf.PerlinNoise(noiseSeed.y, t) * 2 - 1) * NeedsRules.DriftAmplitude(needs != null ? needs.Urgency : 0);
            var offset = correction + drift;
            if (urinal != null)
            {
                var facing = Vector3.ProjectOnPlane(urinal.TargetCenter - urinal.FrontPosition, Vector3.up).normalized;
                var right = Vector3.Cross(Vector3.up, facing);
                CurrentLanding = initialLanding + right * offset.x + Vector3.up * offset.y;
            }
            else CurrentLanding = initialLanding + new Vector3(offset.x, 0, offset.y);
            accuracy.Sample(seconds, Vector3.Distance(CurrentLanding, TargetCenter) <= TargetRadius);
            if (accuracy.Complete) FinishToileting();
        }
        private void FinishToileting()
        {
            var position = movement.transform.position;
            if (Online) facilities.ReportResult(accuracy.Value, position);
            else ApplyResult(movement, toilet, urinal, accuracy.Value, position, ToiletIndex(toilet));
            if (!Online) { toilet?.Release(movement); urinal?.Release(movement); if (needs != null) needs.Toileting = false; }
            // The completion RPC precedes posture replication, so the server can still validate its reservation.
            posture.Set(PostureState.Standing);
            movement.Immobile = false; movement.Aiming = false; Phase = Stage.None;
            toilet = null; urinal = null;
        }
        public static int ToiletIndex(Toilet target)
        {
            if (target == null) return -1;
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
            playerNeeds.CompleteToileting(inStall);
            if (!inStall) { world?.Drop(position); return; }
            if (world != null && world.IsDirty(toiletIndex)) playerNeeds.AddDisgust(NeedsRules.DirtyToiletDisgust);
            var kind = NeedsRules.Defecation(value);
            if (kind == MessKind.Dropping) playerNeeds.AddDisgust(NeedsRules.OutsideStallDisgust);
            world?.AddMess(kind, kind == MessKind.DirtyToilet ? targetToilet.TargetCenter : position, toiletIndex);
            playerNeeds.MarkUnwiped(targetToilet.GetComponentInParent<StallDoor>());
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
            cleanElapsed = NeedsRules.TickCleaning(cleanElapsed, seconds, true);
            if (Online)
            {
                if (Time.time >= nextCleanReport) { facilities.RequestClean(cleaning.Index, true); nextCleanReport = Time.time + .1f; }
                return; // Server removes the object; clients never award cleaning themselves.
            }
            if (cleanElapsed < NeedsRules.CleaningSeconds) return;
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

        /// <summary>E performs the facility selected by the active camera centre ray.</summary>
        public bool TryInteract()
        {
            if (movement.IsTraversing) return false;
            ResolveFocus();
            if (Focus == null) return false;
            var context = new InteractionContext(movement, Online ? facilities.OwnerClientId : 0, Online, facilities);
            if (!Focus.Perform(context)) return false;
            GetComponent<PlayerAppearance>()?.PlayReach();
            var avatar = GetComponent<Network.Avatar>();
            if (avatar != null && avatar.IsSpawned && avatar.IsOwner) avatar.PlayReachRpc();
            return true;
        }

        public bool TryAction()
        {
            if (Unwiped)
            {
                if (HeldItem is PaperRoll paper)
                {
                    if (Online) return facilities.RequestWipe();
                    if (!paper.Use()) return false;
                    needs.Wipe(); return true;
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

        private bool TryThrowCarriedItem()
        {
            var carried = Item.FindCarriedBy(movement);
            return carried != null && (facilities != null && facilities.IsSpawned
                ? facilities.RequestThrow(carried, movement.transform.forward) : carried.Throw(movement, movement.transform.forward));
        }
    }
}
