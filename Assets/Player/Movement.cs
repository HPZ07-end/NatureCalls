using NatureCalls.Core;
using System.Collections;
using UnityEngine;

namespace NatureCalls.Player
{
    [RequireComponent(typeof(CharacterController), typeof(Posture))]
    public sealed class Movement : MonoBehaviour
    {
        [Min(.1f)] public float Speed = 3.5f;
        [Range(.01f, 1)] public float Sensitivity = .12f;
        [Range(10, 85)] public float PitchLimit = 75;
        [Min(.1f)] public float JumpSpeed = 6f;
        [Min(.5f)] public float StandingHeight = 1.8f;
        [Min(.5f)] public float SquattingHeight = 1f;
        public Transform Body;
        [SerializeField] private bool startControlled;
        public bool Controlled { get; private set; }
        public bool Airborne { get; private set; }
        public bool Aiming { get; set; }
        public bool Immobile { get; set; }
        public bool IsTraversing { get; private set; }
        public bool IsCrawling => posture.IsCrawling;
        private const float CrawlingHeight = .85f;
        private const float CrawlingSpeed = 1.8f;
        private const float CrawlingStep = .08f;
        public float Pitch { get; private set; }
        public Quaternion ViewRotation => Quaternion.Euler(Pitch, transform.eulerAngles.y, 0);
        private CharacterController controller;
        private Posture posture;
        private float verticalSpeed;
        private Quaternion returnRotation;
        private Vector3 returnPosition;
        private Vector3 bodyBaseLocalPosition;
        private Vector3 bodyBaseLocalScale = Vector3.one;
        private Vector3 bodyGroundedLocalPosition;
        private bool bodyGroundingPending;
        private bool usesRiggedBody;
        private float standingStep;
        private Vector3 planarVelocity;
        /// <summary>The toilet anchor the player is seated at, or null.</summary>
        public Transform Seat { get; private set; }
#if UNITY_EDITOR
        private bool loggedFirstMove;
#endif

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            // NGO instantiates before applying its approved spawn transform.
            // Enable physics only when ownership and the initial transform are ready.
            controller.enabled = false;
            posture = GetComponent<Posture>();
            standingStep = controller.stepOffset;
            usesRiggedBody = GetComponent<PlayerAppearance>()?.HasRiggedBody == true;
            if (Body != null)
            {
                bodyBaseLocalPosition = Body.localPosition;
                bodyBaseLocalScale = Body.localScale;
                bodyGroundedLocalPosition = bodyBaseLocalPosition;
                bodyGroundingPending = !usesRiggedBody;
            }
            posture.Changed += ApplyPosture;
            ApplyPosture(posture.State);
        }
        private void Start() { if (startControlled && !Controlled) SetControl(true); }
        private void LateUpdate()
        {
            if (!bodyGroundingPending || Body == null || posture.IsSquatting) return;
            bodyGroundingPending = false;
            Body.localPosition = bodyBaseLocalPosition;
            var renderers = Body.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;

            var visualBottom = renderers[0].bounds.min.y;
            for (int index = 1; index < renderers.Length; index++)
                visualBottom = Mathf.Min(visualBottom, renderers[index].bounds.min.y);
            var desiredBottom = transform.position.y - controller.skinWidth;
            var worldCorrection = Vector3.up * (desiredBottom - visualBottom);
            bodyGroundedLocalPosition = bodyBaseLocalPosition + Body.parent.InverseTransformVector(worldCorrection);
            Body.localPosition = bodyGroundedLocalPosition;
        }
        private void OnDestroy() { if (posture != null) posture.Changed -= ApplyPosture; }
        public void SetControl(bool value) { Controlled = value; controller.enabled = value; }
        public void Look(Vector2 delta)
        {
            if (!Controlled || posture.IsSquatting || Immobile) return;
            transform.Rotate(Vector3.up, delta.x * Sensitivity, Space.World);
            Pitch = Mathf.Clamp(Pitch - delta.y * Sensitivity, -PitchLimit, PitchLimit);
        }
        public void Move(Vector2 direction, float deltaTime)
        {
            if (!Controlled || !controller.enabled || IsTraversing) return;
            RefreshVentPosture();
            var startPosition = transform.position;
            var effect = MovementEffectRegistry.Combine(this);
            var movement = (posture.IsSquatting || Immobile) ? Vector3.zero : transform.TransformDirection(new Vector3(direction.x, 0, direction.y));
            var speed = (posture.IsCrawling ? CrawlingSpeed : Speed) * effect.SpeedMultiplier;
            movement = Vector3.ClampMagnitude(movement, 1) * speed;
            planarVelocity = effect.Acceleration > 0 && !posture.IsSquatting && !Immobile
                ? Vector3.MoveTowards(planarVelocity, movement, effect.Acceleration * deltaTime) : movement;
            verticalSpeed = controller.isGrounded && verticalSpeed <= 0 ? -2 : Mathf.Max(verticalSpeed - 20 * deltaTime, -30);
            controller.Move((planarVelocity + Vector3.up * verticalSpeed) * deltaTime);
            Airborne = !controller.isGrounded && !posture.IsSquatting;
            RefreshVentPosture();
#if UNITY_EDITOR
            if (!loggedFirstMove && direction.sqrMagnitude > .01f && (transform.position - startPosition).sqrMagnitude > .000001f)
            {
                loggedFirstMove = true;
                Debug.Log($"WASD_MOVE_OK|input=({direction.x:F2},{direction.y:F2})|from=({startPosition.x:F3},{startPosition.y:F3},{startPosition.z:F3})|to=({transform.position.x:F3},{transform.position.y:F3},{transform.position.z:F3})");
            }
#endif
        }
        public bool Jump()
        {
            RefreshVentPosture();
            if (!Controlled || !controller.enabled || posture.State != PostureState.Standing || Immobile || !controller.isGrounded) return false;
            verticalSpeed = JumpSpeed;
            Airborne = true;
#if UNITY_EDITOR
            Debug.Log($"PLAYER_JUMP_OK|player={name}|speed={JumpSpeed:F2}|position=({transform.position.x:F3},{transform.position.y:F3},{transform.position.z:F3})");
#endif
            return true;
        }

        internal bool Traverse(Vector3[] route, float seconds)
        {
            if (!Controlled || IsTraversing || Immobile || posture.State != PostureState.Standing) return false;
            StartCoroutine(FollowTraversal(route, seconds));
            return true;
        }

        private IEnumerator FollowTraversal(Vector3[] route, float seconds)
        {
            IsTraversing = true;
            Airborne = true;
            controller.enabled = false;
            float elapsed = 0;
            while (elapsed < seconds && Controlled)
            {
                float phase = Mathf.Clamp01(elapsed / seconds);
                if (phase < .2f)
                    transform.position = Vector3.Lerp(route[0], route[1], phase / .2f);
                else if (phase < .8f)
                    transform.position = Vector3.Lerp(route[1], route[2], (phase - .2f) / .6f);
                else
                    transform.position = Vector3.Lerp(route[2], route[3], (phase - .8f) / .2f);
                yield return null;
                elapsed += Time.deltaTime;
            }
            if (Controlled) transform.position = route[3];
            verticalSpeed = 0;
            planarVelocity = Vector3.zero;
            Airborne = false;
            IsTraversing = false;
            controller.enabled = Controlled;
        }
        public void Enter(Transform anchor)
        {
            if (!Controlled || posture.State != PostureState.Standing || Immobile) return;
            returnRotation = transform.rotation;
            returnPosition = transform.position;
            Seat = anchor;
            bool wasEnabled = controller.enabled;
            controller.enabled = false;
            // The aligned pose belongs to the player; Anchor identifies the occupied toilet.
            controller.enabled = wasEnabled;
            posture.Set(PostureState.Squatting);
#if UNITY_EDITOR
            Debug.Log($"TOILET_SEATED|player={name}|seat=({anchor.position.x:F2},{anchor.position.z:F2})|from=({returnPosition.x:F2},{returnPosition.z:F2})");
#endif
        }
        private void ApplyPosture(PostureState state)
        {
            bool squat = state == PostureState.Squatting;
            bool crawl = state == PostureState.Crawling;
            controller.height = crawl ? CrawlingHeight : squat ? SquattingHeight : StandingHeight;
            controller.center = new Vector3(0, controller.height / 2, 0);
            controller.stepOffset = crawl ? CrawlingStep : standingStep;
            if (squat) Airborne = false;
            if (Body != null && !usesRiggedBody)
            {
                Body.localPosition = bodyGroundedLocalPosition;
            }
            if (Body != null)
                Body.localScale = Vector3.Scale(bodyBaseLocalScale, new Vector3(1, crawl ? .43f : squat && !usesRiggedBody ? .56f : 1, 1));
            if (!squat && Seat != null)
            {
                // Stand up where the player was before sitting: the seat itself sits against the bowl.
                bool enabled = controller.enabled;
                controller.enabled = false;
                transform.SetPositionAndRotation(returnPosition, returnRotation);
                controller.enabled = enabled;
                Seat = null;
#if UNITY_EDITOR
                Debug.Log($"TOILET_STOOD_UP|player={name}|at=({returnPosition.x:F2},{returnPosition.z:F2})");
#endif
            }
        }

        internal void RefreshVentPosture()
        {
            if (!Controlled || !controller.enabled || posture.IsSquatting || Aiming || Immobile) return;
            if (MovementEffectRegistry.ForceCrawl(this))
            { posture.Set(PostureState.Crawling); return; }
            if (posture.IsCrawling && HasStandingClearance())
                posture.Set(PostureState.Standing);
        }

        private bool HasStandingClearance()
        {
            float radius = controller.radius - .01f;
            var feet = transform.position;
            var lower = feet + Vector3.up * (CrawlingHeight - radius);
            var upper = feet + Vector3.up * (StandingHeight - radius);
            foreach (var hit in Physics.OverlapCapsule(lower, upper, radius, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                if (!hit.transform.IsChildOf(transform))
                    return false;
            return true;
        }
    }
}
