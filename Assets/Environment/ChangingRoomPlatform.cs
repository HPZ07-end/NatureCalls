using NatureCalls.Player;
using NatureCalls.UI;
using UnityEngine;

namespace NatureCalls.Environment
{
    [RequireComponent(typeof(Collider))]
    public sealed class ChangingRoomPlatform : MonoBehaviour
    {
        [SerializeField] private ChangingRoomHud hud;
        private SimplePlayerController nearbyPlayer;
        private PlayerOutfitController nearbyOutfit;
        private bool active;

        private void Update()
        {
            if (!active && nearbyPlayer != null && Input.GetKeyDown(KeyCode.E))
                BeginChanging();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (active)
                return;
            var player = other.GetComponentInParent<SimplePlayerController>();
            var outfit = other.GetComponentInParent<PlayerOutfitController>();
            if (player == null || outfit == null)
                return;
            nearbyPlayer = player;
            nearbyOutfit = outfit;
            hud.ShowPrompt(true);
        }

        private void OnTriggerExit(Collider other)
        {
            if (active || nearbyPlayer == null || other.GetComponentInParent<SimplePlayerController>() != nearbyPlayer)
                return;
            nearbyPlayer = null;
            nearbyOutfit = null;
            hud.ShowPrompt(false);
        }

        private void BeginChanging()
        {
            active = true;
            nearbyPlayer.InputLocked = true;
            nearbyOutfit.BeginSelection();
            hud.Open(nearbyOutfit, Confirm, Cancel);
        }

        private void Confirm()
        {
            nearbyOutfit.ConfirmSelection();
            EndChanging();
        }

        private void Cancel()
        {
            nearbyOutfit.CancelSelection();
            EndChanging();
        }

        private void EndChanging()
        {
            active = false;
            nearbyPlayer.InputLocked = false;
            hud.Close();
            hud.ShowPrompt(true);
        }
    }
}
