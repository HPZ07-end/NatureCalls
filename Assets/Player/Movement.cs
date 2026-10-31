using UnityEngine;

namespace NatureCalls.Player
{
    [RequireComponent(typeof(CharacterController), typeof(Posture))]
    public sealed class Movement : MonoBehaviour
    {
        [Min(.1f)] public float Speed = 3.5f;
        [Range(.01f, 1)] public float Sensitivity = .12f;
        [Range(10, 85)] public float PitchLimit = 75;
        [Min(.5f)] public float StandingHeight = 1.8f;
        [Min(.5f)] public float SquattingHeight = 1f;
        public Transform Body;
        [SerializeField] private bool startControlled;
        public bool Controlled { get; private set; }
        public float Pitch { get; private set; }
        public Quaternion ViewRotation => Quaternion.Euler(Pitch, transform.eulerAngles.y, 0);
        private CharacterController controller;
        private Posture posture;
        private float verticalSpeed;
        private Quaternion returnRotation;
        private bool aligned;
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
            posture.Changed += ApplyPosture;
            ApplyPosture(posture.State);
        }
        private void Start() { if (startControlled && !Controlled) SetControl(true); }
        private void OnDestroy() { if (posture != null) posture.Changed -= ApplyPosture; }
        public void SetControl(bool value) { Controlled = value; controller.enabled = value; }
        public void Look(Vector2 delta)
        {
            if (!Controlled || posture.IsSquatting) return;
            transform.Rotate(Vector3.up, delta.x * Sensitivity, Space.World);
            Pitch = Mathf.Clamp(Pitch - delta.y * Sensitivity, -PitchLimit, PitchLimit);
        }
        public void Move(Vector2 direction, float deltaTime)
        {
            if (!Controlled || !controller.enabled) return;
            var startPosition = transform.position;
            var movement = posture.IsSquatting ? Vector3.zero : transform.TransformDirection(new Vector3(direction.x, 0, direction.y));
            movement = Vector3.ClampMagnitude(movement, 1) * Speed;
            verticalSpeed = controller.isGrounded ? -2 : Mathf.Max(verticalSpeed - 20 * deltaTime, -30);
            controller.Move((movement + Vector3.up * verticalSpeed) * deltaTime);
#if UNITY_EDITOR
            if (!loggedFirstMove && direction.sqrMagnitude > .01f && (transform.position - startPosition).sqrMagnitude > .000001f)
            {
                loggedFirstMove = true;
                Debug.Log($"WASD_MOVE_OK|input=({direction.x:F2},{direction.y:F2})|from=({startPosition.x:F3},{startPosition.y:F3},{startPosition.z:F3})|to=({transform.position.x:F3},{transform.position.y:F3},{transform.position.z:F3})");
            }
#endif
        }
        public void Enter(Transform anchor)
        {
            if (!Controlled || posture.IsSquatting) return;
            returnRotation = transform.rotation;
            aligned = true;
            controller.enabled = false;
            transform.SetPositionAndRotation(anchor.position, anchor.rotation);
            controller.enabled = true;
            posture.Set(PostureState.Squatting);
        }
        private void ApplyPosture(PostureState state)
        {
            bool squat = state == PostureState.Squatting;
            controller.height = squat ? SquattingHeight : StandingHeight;
            controller.center = new Vector3(0, controller.height / 2, 0);
            if (Body != null) Body.localScale = new Vector3(1, squat ? .56f : 1, 1);
            if (!squat && aligned)
            {
                transform.rotation = returnRotation;
                aligned = false;
            }
        }
    }
}
