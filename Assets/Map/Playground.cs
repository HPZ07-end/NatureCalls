using UnityEngine;
using NatureCalls.Camera;
using NatureCalls.Player;
using PlayerInput = NatureCalls.Input.PlayerInput;

namespace NatureCalls.Map
{
    public sealed class Playground : MonoBehaviour
    {
        public GameObject PlayerPrefab;
        public Transform[] Spawns;
        public CameraController Camera;
        public bool StartOffline = true;
        public Movement OfflinePlayer { get; private set; }
        private void Start() { if (StartOffline) Practice(); }
        public void Practice()
        {
            StopPractice();
            var instance = Instantiate(PlayerPrefab, Spawns[0].position, Spawns[0].rotation);
            instance.name = "Local practice player";
            OfflinePlayer = instance.GetComponent<Movement>();
            OfflinePlayer.SetControl(true);
            instance.GetComponent<PlayerInput>().enabled = true;
            Camera.Bind(OfflinePlayer);
        }
        public void StopPractice()
        {
            Camera.Unbind();
            if (OfflinePlayer != null)
            {
                // NGO scans active objects when starting a host; Destroy is deferred.
                OfflinePlayer.gameObject.SetActive(false);
                Destroy(OfflinePlayer.gameObject);
            }
            OfflinePlayer = null;
            PlayerInput.Unlock();
        }
    }
}
