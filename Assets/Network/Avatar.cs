using NatureCalls.Player;
using Unity.Netcode;
using UnityEngine;
using PlayerInput = NatureCalls.Input.PlayerInput;

namespace NatureCalls.Network
{
    /// <summary>
    /// A networked player: gives control, input and the camera to the owner only, and shares the owner's posture.
    /// Appearance, needs and facility requests live in <see cref="AvatarAppearance"/>, <see cref="NeedsSync"/>
    /// and <see cref="FacilitySync"/> on the same object.
    /// </summary>
    [RequireComponent(typeof(NetworkObject), typeof(Movement), typeof(Posture))]
    public sealed class Avatar : NetworkBehaviour
    {
        public NetworkVariable<PostureState> SharedPosture = new(PostureState.Standing,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        private Movement movement;
        private Posture posture;
        private Map.Playground world;

        public override void OnNetworkSpawn()
        {
            movement = GetComponent<Movement>();
            posture = GetComponent<Posture>();
            world = FindFirstObjectByType<Map.Playground>();
            SharedPosture.OnValueChanged += PostureChanged;
            posture.Set(SharedPosture.Value);
            posture.Changed += PublishPosture;
            movement.SetControl(IsOwner);
            GetComponent<PlayerInput>().enabled = IsOwner;
            if (IsOwner) world.Camera.Bind(movement);
        }

        private void PublishPosture(PostureState value)
        {
            if (IsSpawned && IsOwner) SharedPosture.Value = value;
        }

        private void PostureChanged(PostureState previous, PostureState current) => posture.Set(current);

        public override void OnNetworkDespawn()
        {
            SharedPosture.OnValueChanged -= PostureChanged;
            if (posture != null) posture.Changed -= PublishPosture;
            if (movement != null) movement.SetControl(false);
            GetComponent<PlayerInput>().enabled = false;
            if (world != null && world.Camera.Target == movement) world.Camera.Unbind();
        }
    }
}
