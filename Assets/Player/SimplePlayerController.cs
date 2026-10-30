using UnityEngine;

namespace NatureCalls.Player
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class SimplePlayerController : MonoBehaviour
    {
        [SerializeField] private float moveSpeed = 5.5f;
        [SerializeField] private float jumpHeight = 1.8f;
        [SerializeField] private float gravity = -20f;
        private CharacterController characterController;
        private float verticalVelocity;

        public bool InputLocked { get; set; }

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
        }

        private void Update()
        {
            if (InputLocked)
                return;

            var movement = new Vector3(Input.GetAxisRaw("Horizontal"), 0f, Input.GetAxisRaw("Vertical"));
            movement = Vector3.ClampMagnitude(movement, 1f);
            if (movement.sqrMagnitude > 0.01f)
                transform.forward = movement;

            if (characterController.isGrounded && verticalVelocity < 0f)
                verticalVelocity = -2f;
            if (characterController.isGrounded && Input.GetButtonDown("Jump"))
                verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);

            verticalVelocity += gravity * Time.deltaTime;
            var velocity = movement * moveSpeed;
            velocity.y = verticalVelocity;
            characterController.Move(velocity * Time.deltaTime);
        }
    }
}
