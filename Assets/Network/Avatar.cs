using NatureCalls.Player;
using Unity.Netcode;
using UnityEngine;
using PlayerInput = NatureCalls.Input.PlayerInput;

namespace NatureCalls.Network
{
    [RequireComponent(typeof(NetworkObject), typeof(Movement), typeof(Posture))]
    [RequireComponent(typeof(PlayerAppearance))]
    public sealed class Avatar : NetworkBehaviour
    {
        public NetworkVariable<PostureState> SharedPosture = new(PostureState.Standing,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        public NetworkVariable<int> ColorIndex = new(0);
        // Server-write; -1 (direct play without a Lobby room) shows the identity color instead.
        public NetworkVariable<int> OutfitIndex = new(-1);
        private Movement movement;
        private Posture posture;
        private PlayerAppearance appearance;
        private Map.Playground world;
        private static readonly Color[] Colors = { new(.12f, .5f, .9f), new(.95f, .35f, .15f), new(.65f, .3f, .85f), new(.15f, .75f, .4f) };

        public override void OnNetworkSpawn()
        {
            movement = GetComponent<Movement>();
            posture = GetComponent<Posture>();
            appearance = GetComponent<PlayerAppearance>();
            world = FindFirstObjectByType<Map.Playground>();
            if (IsServer) ColorIndex.Value = Session.Instance.Slot(OwnerClientId);
            ColorIndex.OnValueChanged += AppearanceChanged;
            OutfitIndex.OnValueChanged += AppearanceChanged;
            RefreshAppearance();
            SharedPosture.OnValueChanged += PostureChanged;
            posture.Set(SharedPosture.Value);
            posture.Changed += PublishPosture;
            movement.SetControl(IsOwner);
            GetComponent<PlayerInput>().enabled = IsOwner;
            if (IsOwner) world.Camera.Bind(movement);
            name = "Player " + (ColorIndex.Value + 1) + (IsOwner ? " (you)" : " (remote)");
        }
        private void PublishPosture(PostureState value) { if (IsSpawned && IsOwner) SharedPosture.Value = value; }
        private void PostureChanged(PostureState previous, PostureState current) { posture.Set(current); }
        private void AppearanceChanged(int previous, int current) => RefreshAppearance();
        private void RefreshAppearance()
        {
            if (OutfitIndex.Value >= 0 && appearance.Catalog != null && appearance.Catalog.Contains(OutfitIndex.Value))
                appearance.ApplyOutfit(OutfitIndex.Value);
            else
                appearance.ApplyIdentityColor(Colors[Mathf.Abs(ColorIndex.Value) % Colors.Length]);
        }
        public override void OnNetworkDespawn()
        {
            SharedPosture.OnValueChanged -= PostureChanged;
            ColorIndex.OnValueChanged -= AppearanceChanged;
            OutfitIndex.OnValueChanged -= AppearanceChanged;
            if (posture != null) posture.Changed -= PublishPosture;
            if (movement != null) movement.SetControl(false);
            GetComponent<PlayerInput>().enabled = false;
            if (world != null && world.Camera.Target == movement) world.Camera.Unbind();
        }
    }
}
