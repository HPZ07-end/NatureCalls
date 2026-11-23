using NatureCalls.Core;
using UnityEngine;

namespace NatureCalls.Player
{
    [DisallowMultipleComponent]
    public sealed class PlayerAppearance : MonoBehaviour
    {
        [SerializeField] private Transform body;
        [SerializeField] private OutfitCatalog catalog;
        [SerializeField] private Animator[] rigs;

        public Transform Body => body;
        public OutfitCatalog Catalog => catalog;
        public int OutfitIndex { get; private set; } = -1;
        public PlayerBodyType BodyType { get; private set; }
        public bool HasRiggedBody => rigs != null && rigs.Length == 3;
        public Animator ActiveAnimator => HasRiggedBody ? rigs[(int)BodyType] : null;
        public Renderer OutfitRenderer { get; private set; }

        private static readonly int SpeedParameter = Animator.StringToHash("Speed");
        private static readonly int GroundedParameter = Animator.StringToHash("Grounded");
        private static readonly int SquattingParameter = Animator.StringToHash("Squatting");
        private static readonly int CrawlingParameter = Animator.StringToHash("Crawling");
        private static readonly int ReachParameter = Animator.StringToHash("Reach");
        private MaterialPropertyBlock tintBlock;
        private Movement movement;
        private Posture posture;
        private CharacterController capsule;
        private Vector3 previousPosition;
        private Vector3 bodyOrigin;
        private float floorOffset;
        private float speed;
        private Color tint;
        private bool hasTint;
        private bool remoteAirborne;

        private void Awake()
        {
            if (body == null)
                body = transform.Find("Body");
            if (!HasRiggedBody) return;
            movement = GetComponent<Movement>();
            posture = GetComponent<Posture>();
            capsule = GetComponent<CharacterController>();
            previousPosition = transform.position;
            bodyOrigin = body.localPosition;
            SetBodyType(BodyType);
        }

        public void SetBodyType(PlayerBodyType bodyType)
        {
            if (!OutfitSelection.IsValidBodyType(bodyType)) return;
            BodyType = bodyType;
            if (HasRiggedBody)
            {
                foreach (var rig in rigs)
                    rig.transform.parent.gameObject.SetActive(rig == ActiveAnimator);
                OutfitRenderer = ActiveAnimator.GetComponentInChildren<SkinnedMeshRenderer>();
            }
            if (hasTint) ApplyColor(tint);
        }

        public void SetRemoteAirborne(bool airborne) => remoteAirborne = airborne;

        public void PlayReach()
        {
            if (ActiveAnimator != null && posture.State == PostureState.Standing)
                ActiveAnimator.SetTrigger(ReachParameter);
        }

        private void LateUpdate()
        {
            if (!HasRiggedBody) return;
            var position = transform.position;
            var delta = position - previousPosition;
            previousPosition = position;
            delta.y = 0;
            var airborne = movement.Controlled ? movement.Airborne : remoteAirborne;
            var squatting = posture.IsSquatting;
            // Remote planar movement already arrives through NetworkTransform.
            var dt = Time.deltaTime;
            var targetSpeed = squatting || delta.sqrMagnitude > .25f || dt <= 0 ? 0 : delta.magnitude / dt;
            speed = Mathf.Lerp(speed, Mathf.Min(movement.Speed, targetSpeed), 1 - Mathf.Exp(-18 * dt));
            var animator = ActiveAnimator;
            animator.SetFloat(SpeedParameter, speed / movement.Speed);
            animator.SetBool(GroundedParameter, !airborne);
            animator.SetBool(SquattingParameter, squatting);
            animator.SetBool(CrawlingParameter, posture.IsCrawling);

            // Offset only the visible model for the capsule's skin gap. Retain this offset in the air.
            if (!airborne && Physics.Raycast(position + Vector3.up * .035f, Vector3.down, out var floor,
                capsule.skinWidth + .09f, Physics.DefaultRaycastLayers & ~(1 << gameObject.layer),
                QueryTriggerInteraction.Ignore))
                floorOffset = floor.point.y - position.y;
            body.localPosition = bodyOrigin + body.parent.InverseTransformVector(Vector3.up * floorOffset);
        }

        public void ApplyIdentityColor(Color color)
        {
            ApplyColor(color);
        }

        public void ApplyOutfit(int index)
        {
            if (catalog == null || !catalog.Contains(index))
            {
                Debug.LogWarning($"OUTFIT_INVALID|player={name}|index={index}|catalog={catalog != null}");
                return;
            }
            OutfitIndex = index;
            ApplyColor(catalog[index].Color);
        }

        private void ApplyColor(Color color)
        {
            tint = color;
            hasTint = true;
            if (body == null) return;
            tintBlock ??= new MaterialPropertyBlock();
            tintBlock.SetColor("_BaseColor", color);
            tintBlock.SetColor("_Color", color);
            if (OutfitRenderer != null)
            {
                // Slot 1 uses a grayscale brush atlas for the recolorable cloth.
                OutfitRenderer.SetPropertyBlock(tintBlock, 1);
                return;
            }
            // Legacy primitive player used by the standalone map builder.
            foreach (var renderer in body.GetComponentsInChildren<Renderer>(true))
                renderer.SetPropertyBlock(tintBlock);
        }
    }
}
