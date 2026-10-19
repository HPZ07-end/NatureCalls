using UnityEngine;

namespace NatureCalls.Player
{
    [RequireComponent(typeof(CharacterController), typeof(Posture))]
    public sealed class Movement : MonoBehaviour
    {
        [Min(.1f)] public float Speed = 3.5f;
        [Range(.01f, 1)] public float Sensitivity = .12f;
        [Range(10, 85)] public float PitchLimit = 75;
        public Transform Body;
        public bool Controlled { get; private set; }
        public float Pitch { get; private set; }
        public Quaternion ViewRotation => Quaternion.Euler(Pitch, transform.eulerAngles.y, 0);
        private CharacterController controller;
        private Posture posture;
        private float verticalSpeed;
        private Quaternion returnRotation;
        private bool aligned;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            posture = GetComponent<Posture>();
            posture.Changed += ApplyPosture;
            ApplyPosture(posture.State);
        }
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
            var movement = posture.IsSquatting ? Vector3.zero : transform.TransformDirection(new Vector3(direction.x, 0, direction.y));
            movement = Vector3.ClampMagnitude(movement, 1) * Speed;
            verticalSpeed = controller.isGrounded ? -2 : Mathf.Max(verticalSpeed - 20 * deltaTime, -30);
            controller.Move((movement + Vector3.up * verticalSpeed) * deltaTime);
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
            controller.height = squat ? 1 : 1.8f;
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
