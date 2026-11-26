using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Unity.Cinemachine;
using NatureCalls.Player;

namespace NatureCalls.Camera
{
    [DefaultExecutionOrder(-20)]
    public sealed class CameraController : MonoBehaviour
    {
        public const int LocalBodyLayer = 30;
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
        // Seated on a toilet the stall is too narrow for the camera behind: look down from above instead.
        [Range(1, 4)] public float SeatedDistance = 1.9f;
        [Range(60, 89)] public float SeatedPitch = 80;
        [Range(.05f, 2)] public float TransitionDuration = .65f;
        public LayerMask CameraCollisionLayers = 1;
        [Range(.05f, .4f)] public float CameraRadius = .15f;
        public Movement Target { get; private set; }
        /// <summary>Toileting look-down (0–1, set by Input/ToiletGaze): the seated view bows over <see cref="GazePoint"/>.</summary>
        public float Gaze { get; set; }
        public Vector3 GazePoint { get; set; }
        private Posture posture;
        private Renderer[] body;
        private Dictionary<GameObject, int> originalBodyLayers;
        private int outputCullingMask;
        private CinemachineThirdPersonFollow follow;

        private void Awake()
        {
            outputCullingMask = Output.cullingMask;
            follow = ThirdPerson.GetComponent<CinemachineThirdPersonFollow>();
            Unbind();
        }
        public void Bind(Movement target)
        {
            Unbind();
            Target = target;
            posture = target.GetComponent<Posture>();
            // Register both variants so switching the mirror preview preserves first-person hiding.
            body = target.Body.GetComponentsInChildren<Renderer>(true);
            originalBodyLayers = body.Select(renderer => renderer.gameObject).Distinct()
                .ToDictionary(item => item, item => item.layer);
            foreach (var item in originalBodyLayers.Keys) item.layer = LocalBodyLayer;
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
            if (originalBodyLayers != null)
                foreach (var pair in originalBodyLayers) if (pair.Key != null) pair.Key.layer = pair.Value;
            Output.cullingMask = outputCullingMask;
            Target = null; posture = null; body = null;
            originalBodyLayers = null;
            FirstPerson.gameObject.SetActive(false);
            ThirdPerson.gameObject.SetActive(false);
            Brain.enabled = false;
            Output.transform.position = new Vector3(5, 6, -5);
            Output.transform.LookAt(new Vector3(0, .5f, 3));
        }
        private void OnDestroy() { if (posture != null) posture.Changed -= SetPosture; }
        private void SetPosture(PostureState state)
        {
            // First person throughout, including squatting and toileting aim (the player aims by feel, looking
            // down past their own legs). The third-person rig stays as a lower-priority fallback.
            FirstPerson.Priority = 20;
            ThirdPerson.Priority = 10;
        }
        private void LateUpdate()
        {
            if (Target == null) return;
            SetPosture(posture.State);
            UpdateTargets();
            // Hide only from the local first-person output. Mirror cameras still render this layer.
            // Show the local body only while squatting with the head up (knees in view). Looking down to aim, or
            // standing to urinate, hides it so the bowl / urinal below is visible instead of the torso.
            bool hidden = !posture.IsSquatting || Gaze > .3f;
            Output.cullingMask = hidden ? outputCullingMask & ~(1 << LocalBodyLayer) : outputCullingMask;
            // Aiming the stream: hide the whole local model (hair, hands, accessories) so nothing covers the crosshair.
            bool urineAim = Target.Aiming && !posture.IsSquatting;
            foreach (var renderer in body) if (renderer != null) renderer.forceRenderingOff = urineAim;
            if (urineAim)
            {
                var held = NatureCalls.Facility.Item.FindCarriedBy(Target);
                if (held != null)
                    foreach (var part in held.GetComponentsInChildren<Renderer>())
                        if (!part.forceRenderingOff) { part.forceRenderingOff = true; extraHidden.Add(part); }
                foreach (var part in Target.GetComponentsInChildren<Renderer>())
                    if (part is not LineRenderer && part.GetType().Name != "ParticleSystemRenderer" && !part.forceRenderingOff) { part.forceRenderingOff = true; extraHidden.Add(part); }
            }
            else if (extraHidden.Count > 0) { foreach (var part in extraHidden) if (part != null) part.forceRenderingOff = false; extraHidden.Clear(); }
        }
        private readonly System.Collections.Generic.List<Renderer> extraHidden = new();
        public void UpdateTargets()
        {
            if (Target == null) return;
            Brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, TransitionDuration);
            if (posture.IsSquatting && Target.Seat != null && Gaze > 0)
            {
                // Seated look-down: from just in front of the head, turn toward the bowl under the body.
                var head = Target.transform.position + Vector3.up * 1.05f + Target.transform.forward * .12f;
                var down = Quaternion.LookRotation(GazePoint - head, Target.transform.forward);
                Eye.SetPositionAndRotation(head, Quaternion.Slerp(Target.ViewRotation, down, Mathf.SmoothStep(0, 1, Gaze)));
            }
            else if (posture.IsSquatting)
                Eye.SetPositionAndRotation(Target.transform.position + Vector3.up * 1.05f, Target.ViewRotation);
            else if (Target.Aiming)
            {
                // Standing pee aim: eye a little ahead of the face, past the body and anything held in the hands.
                var flat = Vector3.ProjectOnPlane(Target.transform.forward, Vector3.up).normalized;
                Eye.SetPositionAndRotation(Target.transform.position + Vector3.up * EyeHeight + flat * .2f, Target.ViewRotation);
            }
            else
                Eye.SetPositionAndRotation(Target.transform.position + Vector3.up * (Target.IsCrawling ? .63f : EyeHeight), Target.ViewRotation);
            bool seated = Target.Aiming || (posture.IsSquatting && Target.Seat != null);
            if (posture.IsSquatting || Target.Aiming)
                Shoulder.SetPositionAndRotation(Target.transform.position + Vector3.up * ThirdPersonHeight,
                    Quaternion.Euler(seated ? SeatedPitch : ThirdPersonPitch, Target.transform.eulerAngles.y, 0));
            follow.CameraDistance = seated ? SeatedDistance : ThirdPersonDistance;
            if (seated && Gaze > 0)
            {
                // Head bowed between the knees: from just above the body, straight down at the landing point.
                var head = Target.transform.position + Vector3.up * 1.05f + Target.transform.forward * .12f;
                var down = Quaternion.LookRotation(GazePoint - head, Target.transform.forward);
                Shoulder.SetPositionAndRotation(Vector3.Lerp(Shoulder.position, head, Gaze), Quaternion.Slerp(Shoulder.rotation, down, Gaze));
                follow.CameraDistance = Mathf.Lerp(SeatedDistance, .02f, Gaze);
            }
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
