using UnityEngine;
using Unity.Cinemachine;
using NatureCalls.Player;

namespace NatureCalls.Camera
{
    [DefaultExecutionOrder(-20)]
    public sealed class CameraController : MonoBehaviour
    {
        public UnityEngine.Camera Output;
        public CinemachineBrain Brain;
        public CinemachineCamera FirstPerson;
        public CinemachineCamera ThirdPerson;
        public Transform Eye;
        public Transform Shoulder;
        [Range(1, 2.1f)] public float EyeHeight = 1.65f;
        [Range(.5f, 5)] public float ThirdPersonDistance = 1.6f;
        [Range(.3f, 3)] public float ThirdPersonHeight = .8f;
        [Range(10, 70)] public float ThirdPersonPitch = 50;
        [Range(-2, 2)] public float ShoulderOffset = 0;
        [Range(.05f, 2)] public float TransitionDuration = .65f;
        public LayerMask CameraCollisionLayers = 1;
        [Range(.05f, .4f)] public float CameraRadius = .15f;
        public Movement Target { get; private set; }
        private Posture posture;
        private Renderer[] body;
        private CinemachineThirdPersonFollow follow;

        private void Awake()
        {
            follow = ThirdPerson.GetComponent<CinemachineThirdPersonFollow>();
            Unbind();
        }
        public void Bind(Movement target)
        {
            Unbind();
            Target = target;
            posture = target.GetComponent<Posture>();
            body = target.Body.GetComponentsInChildren<Renderer>();
            Eye.SetPositionAndRotation(target.transform.position + Vector3.up * EyeHeight, target.ViewRotation);
            FirstPerson.gameObject.SetActive(true);
            ThirdPerson.gameObject.SetActive(true);
            Brain.enabled = true;
            posture.Changed += SetPosture;
            SetPosture(posture.State);
            UpdateTargets();
        }
        public void Unbind()
        {
            if (posture != null) posture.Changed -= SetPosture;
            if (body != null) foreach (var renderer in body) if (renderer != null) renderer.forceRenderingOff = false;
            Target = null; posture = null; body = null;
            FirstPerson.gameObject.SetActive(false);
            ThirdPerson.gameObject.SetActive(false);
            Brain.enabled = false;
            Output.transform.position = new Vector3(5, 6, -5);
            Output.transform.LookAt(new Vector3(0, .5f, 3));
        }
        private void OnDestroy() { if (posture != null) posture.Changed -= SetPosture; }
        private void SetPosture(PostureState state)
        {
            bool squat = state == PostureState.Squatting;
            FirstPerson.Priority = squat ? 10 : 20;
            ThirdPerson.Priority = squat ? 20 : 10;
        }
        private void LateUpdate()
        {
            if (Target == null) return;
            UpdateTargets();
            // Per-renderer visibility affects only this process, never network state or physics.
            bool hidden = !posture.IsSquatting || Vector3.Distance(Output.transform.position, Eye.position) < .65f;
            foreach (var renderer in body) if (renderer != null) renderer.forceRenderingOff = hidden;
        }
        public void UpdateTargets()
        {
            if (Target == null) return;
            Brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, TransitionDuration);
            if (!posture.IsSquatting)
                Eye.SetPositionAndRotation(Target.transform.position + Vector3.up * EyeHeight, Target.ViewRotation);
            if (posture.IsSquatting)
                Shoulder.SetPositionAndRotation(Target.transform.position + Vector3.up * ThirdPersonHeight,
                    Quaternion.Euler(ThirdPersonPitch, Target.transform.eulerAngles.y, 0));
            follow.CameraDistance = ThirdPersonDistance;
            follow.ShoulderOffset = new Vector3(ShoulderOffset, 0, 0);
            follow.VerticalArmLength = 0;
            follow.CameraSide = 1;
            follow.Damping = Vector3.zero;
            follow.AvoidObstacles = new CinemachineThirdPersonFollow.ObstacleSettings
            {
                Enabled = true, CollisionFilter = CameraCollisionLayers, CameraRadius = CameraRadius,
                IgnoreTag = "", DampingIntoCollision = 0, DampingFromCollision = .25f
            };
        }
    }
}
