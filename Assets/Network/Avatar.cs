using NatureCalls.Player;
using Unity.Netcode;
using UnityEngine;
using PlayerInput = NatureCalls.Input.PlayerInput;

namespace NatureCalls.Network
{
    [RequireComponent(typeof(NetworkObject), typeof(Movement), typeof(Posture))]
    public sealed class Avatar : NetworkBehaviour
    {
        public NetworkVariable<PostureState> SharedPosture = new(PostureState.Standing,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        public NetworkVariable<int> ColorIndex = new(0);
        private Movement movement;
        private Posture posture;
        private Map.Playground world;
        private static readonly Color[] Colors = { new(.12f, .5f, .9f), new(.95f, .35f, .15f), new(.65f, .3f, .85f), new(.15f, .75f, .4f) };

        public override void OnNetworkSpawn()
        {
            movement = GetComponent<Movement>();
            posture = GetComponent<Posture>();
            world = FindFirstObjectByType<Map.Playground>();
            if (IsServer) ColorIndex.Value = FindFirstObjectByType<Session>().Slot(OwnerClientId);
            ColorIndex.OnValueChanged += ColorChanged;
            ColorChanged(0, ColorIndex.Value);
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
        private void ColorChanged(int previous, int current)
        {
            var block = new MaterialPropertyBlock();
            block.SetColor("_BaseColor", Colors[Mathf.Abs(current) % Colors.Length]);
            foreach (var renderer in movement.Body.GetComponentsInChildren<Renderer>()) renderer.SetPropertyBlock(block);
        }
        public override void OnNetworkDespawn()
        {
            SharedPosture.OnValueChanged -= PostureChanged;
            ColorIndex.OnValueChanged -= ColorChanged;
            if (posture != null) posture.Changed -= PublishPosture;
            if (movement != null) movement.SetControl(false);
            GetComponent<PlayerInput>().enabled = false;
            if (world != null && world.Camera.Target == movement) world.Camera.Unbind();
        }
    }
}
