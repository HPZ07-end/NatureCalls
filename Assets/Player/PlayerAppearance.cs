using NatureCalls.Core;
using UnityEngine;

namespace NatureCalls.Player
{
    [DisallowMultipleComponent]
    public sealed class PlayerAppearance : MonoBehaviour
    {
        [SerializeField] private Transform body;
        [SerializeField] private OutfitCatalog catalog;
        [SerializeField] private Animator male;
        [SerializeField] private Animator female;
        [SerializeField] private Renderer maleShirt;
        [SerializeField] private Renderer femaleShirt;

        public Transform Body => body;
        public OutfitCatalog Catalog => catalog;
        public int OutfitIndex { get; private set; } = -1;
        public PlayerGender Gender { get; private set; }
        public bool HasRiggedBody => male != null && female != null;
        public Animator ActiveAnimator => Gender == PlayerGender.Female ? female : male;
        public Renderer ShirtRenderer => Gender == PlayerGender.Female ? femaleShirt : maleShirt;

        private static readonly int SpeedParameter = Animator.StringToHash("Speed");
        private static readonly int GroundedParameter = Animator.StringToHash("Grounded");
        private static readonly int SquattingParameter = Animator.StringToHash("Squatting");
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
            SetGender(Gender);
        }

        public void SetGender(PlayerGender gender)
        {
            if (!OutfitSelection.IsValidGender(gender)) return;
            Gender = gender;
            if (HasRiggedBody)
            {
                male.gameObject.SetActive(gender == PlayerGender.Male);
                female.gameObject.SetActive(gender == PlayerGender.Female);
            }
            if (hasTint) ApplyColor(tint);
        }

        public void SetRemoteAirborne(bool airborne) => remoteAirborne = airborne;

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
            animator.SetFloat(SpeedParameter, speed);
            animator.SetBool(GroundedParameter, !airborne);
            animator.SetBool(SquattingParameter, squatting);

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
            if (ShirtRenderer != null)
            {
                ShirtRenderer.SetPropertyBlock(tintBlock);
                return;
            }
            // Legacy primitive player used by the standalone map builder.
            foreach (var renderer in body.GetComponentsInChildren<Renderer>(true))
                renderer.SetPropertyBlock(tintBlock);
        }
    }
}
