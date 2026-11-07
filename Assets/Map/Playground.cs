using UnityEngine;
using NatureCalls.Camera;
using NatureCalls.Player;
using PlayerInput = NatureCalls.Input.PlayerInput;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Pool;
using NatureCalls.Core;

namespace NatureCalls.Map
{
    public sealed class Playground : MonoBehaviour
    {
        public GameObject PlayerPrefab;
        public Transform[] Spawns;
        public CameraController Camera;
        public bool StartOffline = true;
        public Movement OfflinePlayer { get; private set; }
        // A networked match spawns its players through the Lobby room instead.
        private readonly List<GameObject> droppings = new();

        private void Awake() => droppingPool = CreateDroppingPool();

        private void Start()
        {
            if (StartOffline && Match.Mode != MatchMode.Networked) Practice();
            Match.StartOfflineRound();
            if (Match.Mode == MatchMode.Offline && OfflinePlayer != null)
                OfflinePlayer.GetComponent<PlayerNeeds>()?.BeginRound(Random.value);
        }
        private void Update()
        {
            var needs = Match.Mode == MatchMode.Offline && OfflinePlayer != null ? OfflinePlayer.GetComponent<PlayerNeeds>() : null;
            if (needs != null && needs.Failed) { Match.ReturnToDressingRoom(Match.FailureMessage("", NeedsRules.UrgencyFailed(needs.Urgency))); return; }
            Match.TickOfflineRound();
        }

        /// <summary>Leaves a dropping (a small black ball) where a player toileted outside a stall.</summary>
        public void Drop(Vector3 position)
        {
            var ball = droppingPool.Get();
            ball.transform.position = position + Vector3.up * .11f;
            droppings.Add(ball);
        }

        public int DroppingCount => droppings.Count;

        /// <summary>
        /// Shows the host's shared droppings: places any this peer has not placed yet, and returns any the list
        /// no longer holds (for example once cleaning removes them) to the pool.
        /// </summary>
        public void ShowDroppings(IReadOnlyList<Vector3> positions)
        {
            while (droppings.Count > positions.Count) RemoveDropping(droppings.Count - 1);
            for (int index = 0; index < droppings.Count; index++) droppings[index].transform.position = positions[index] + Vector3.up * .11f;
            for (int index = droppings.Count; index < positions.Count; index++) Drop(positions[index]);
        }

        public void RemoveDropping(int index)
        {
            droppingPool.Release(droppings[index]);
            droppings.RemoveAt(index);
        }

        // Object pool: droppings are created once and reused, since cleaning will remove and players re-add them.
        private ObjectPool<GameObject> droppingPool;
        private ObjectPool<GameObject> CreateDroppingPool() => new(
            createFunc: () =>
            {
                var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                ball.name = "Dropping";
                Destroy(ball.GetComponent<Collider>());
                ball.transform.SetParent(transform, true);
                ball.transform.localScale = Vector3.one * .22f;
                ball.GetComponent<Renderer>().material.color = Color.black;
                return ball;
            },
            actionOnGet: ball => ball.SetActive(true),
            actionOnRelease: ball => ball.SetActive(false),
            actionOnDestroy: Destroy,
            defaultCapacity: 8);
        public void Practice()
        {
            StopPractice();
            var instance = Instantiate(PlayerPrefab, Spawns[0].position, Spawns[0].rotation);
            instance.name = "Local practice player";
            OfflinePlayer = instance.GetComponent<Movement>();
            OfflinePlayer.SetControl(true);
            instance.GetComponent<PlayerInput>().enabled = true;
            var appearance = instance.GetComponent<PlayerAppearance>();
            if (appearance.Catalog != null) appearance.ApplyOutfit(appearance.Catalog.Clamp(LocalProfile.OutfitIndex));
            Camera.Bind(OfflinePlayer);
#if UNITY_EDITOR
            var listeners = FindObjectsByType<AudioListener>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Debug.Log($"OFFLINE_PLAYER_READY|position=({OfflinePlayer.transform.position.x:F3},{OfflinePlayer.transform.position.y:F3},{OfflinePlayer.transform.position.z:F3})|" +
                $"controlled={OfflinePlayer.Controlled}|inputEnabled={instance.GetComponent<PlayerInput>().enabled}|bodyRenderers={OfflinePlayer.Body.GetComponentsInChildren<Renderer>().Length}|" +
                $"listeners={string.Join(",", listeners.Select(listener => listener.gameObject.name + ":" + listener.enabled + ":" + listener.gameObject.scene.name))}");
#endif
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
