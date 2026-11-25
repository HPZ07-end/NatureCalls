using NatureCalls.Core;
using NatureCalls.Player;
using Unity.Netcode;
using UnityEngine;

namespace NatureCalls.Network
{
    /// <summary>Shares a player's identity color (by room slot) and outfit; both are written by the host.</summary>
    [RequireComponent(typeof(NetworkObject), typeof(PlayerAppearance))]
    public sealed class AvatarAppearance : NetworkBehaviour
    {
        public NetworkVariable<int> ColorIndex = new(0);
        // Server-write; -1 (direct play without a Lobby room) shows the identity color instead.
        public NetworkVariable<int> OutfitIndex = new(-1);
        public NetworkVariable<PlayerBodyType> BodyType = new(PlayerBodyType.Balanced);
        private PlayerAppearance appearance;
        private static readonly Color[] Colors = { new(.12f, .5f, .9f), new(.95f, .35f, .15f), new(.65f, .3f, .85f), new(.15f, .75f, .4f) };

        public override void OnNetworkSpawn()
        {
            appearance = GetComponent<PlayerAppearance>();
            if (IsServer) ColorIndex.Value = Session.Instance.Slot(OwnerClientId);
            ColorIndex.OnValueChanged += AppearanceChanged;
            OutfitIndex.OnValueChanged += AppearanceChanged;
            BodyType.OnValueChanged += BodyTypeChanged;
            RefreshAppearance();
            if (IsOwner && OutfitIndex.Value < 0) SetDirectBodyTypeRpc(LocalProfile.BodyType);
            name = "Player " + (ColorIndex.Value + 1) + (IsOwner ? " (you)" : " (remote)");
        }

        private void AppearanceChanged(int previous, int current) => RefreshAppearance();
        private void BodyTypeChanged(PlayerBodyType previous, PlayerBodyType current) => RefreshAppearance();

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void SetDirectBodyTypeRpc(PlayerBodyType bodyType)
        {
            if (OutfitIndex.Value < 0 && OutfitSelection.IsValidBodyType(bodyType)) BodyType.Value = bodyType;
        }

        private void RefreshAppearance()
        {
            appearance.SetBodyType(BodyType.Value);
            if (OutfitIndex.Value >= 0 && appearance.Catalog != null && appearance.Catalog.Contains(OutfitIndex.Value))
                appearance.ApplyOutfit(OutfitIndex.Value);
            else
                appearance.ApplyIdentityColor(Colors[Mathf.Abs(ColorIndex.Value) % Colors.Length]);
        }

        public override void OnNetworkDespawn()
        {
            ColorIndex.OnValueChanged -= AppearanceChanged;
            OutfitIndex.OnValueChanged -= AppearanceChanged;
            BodyType.OnValueChanged -= BodyTypeChanged;
        }
    }
}
