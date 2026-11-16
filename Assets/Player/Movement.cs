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
        public bool Aiming { get; set; }
        public bool Immobile { get; set; }
        public float Pitch { get; private set; }
        public Quaternion ViewRotation => Quaternion.Euler(Pitch, transform.eulerAngles.y, 0);
        private CharacterController controller;
        private Posture posture;
        private float verticalSpeed;
        private Quaternion returnRotation;
        private Vector3 returnPosition;
        private Vector3 bodyBaseLocalPosition;
        private Vector3 bodyGroundedLocalPosition;
        private bool bodyGroundingPending;
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
            if (Body != null)
            {
                bodyBaseLocalPosition = Body.localPosition;
                bodyGroundedLocalPosition = bodyBaseLocalPosition;
                bodyGroundingPending = true;
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
            if (!Controlled || !controller.enabled) return;
            var startPosition = transform.position;
            var movement = (posture.IsSquatting || Immobile) ? Vector3.zero : transform.TransformDirection(new Vector3(direction.x, 0, direction.y));
            movement = Vector3.ClampMagnitude(movement, 1) * Speed;
            verticalSpeed = controller.isGrounded && verticalSpeed <= 0 ? -2 : Mathf.Max(verticalSpeed - 20 * deltaTime, -30);
            controller.Move((movement + Vector3.up * verticalSpeed) * deltaTime);
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
            if (!Controlled || !controller.enabled || posture.IsSquatting || Immobile || !controller.isGrounded) return false;
            verticalSpeed = JumpSpeed;
#if UNITY_EDITOR
            Debug.Log($"PLAYER_JUMP_OK|player={name}|speed={JumpSpeed:F2}|position=({transform.position.x:F3},{transform.position.y:F3},{transform.position.z:F3})");
#endif
            return true;
        }
        public void Enter(Transform anchor)
        {
            if (!Controlled || posture.IsSquatting || Immobile) return;
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
            controller.height = squat ? SquattingHeight : StandingHeight;
            controller.center = new Vector3(0, controller.height / 2, 0);
            if (Body != null)
            {
                Body.localPosition = bodyGroundedLocalPosition;
                Body.localScale = new Vector3(1, squat ? .56f : 1, 1);
            }
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
    }
}
