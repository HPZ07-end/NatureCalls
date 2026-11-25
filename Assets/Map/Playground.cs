using UnityEngine;
using NatureCalls.Camera;
using NatureCalls.Player;
using PlayerInput = NatureCalls.Input.PlayerInput;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Pool;
using NatureCalls.Core;
using NatureCalls.Facility;

namespace NatureCalls.Map
{
    public sealed class Playground : MonoBehaviour, IMessSink
    {
        public GameObject PlayerPrefab;
        public Transform[] Spawns;
        public CameraController Camera;
        public bool StartOffline = true;
        [Tooltip("Start the standard offline round when this scene is opened directly.")]
        [SerializeField] private bool startOfflineRoundOnDirectPlay;
        public Movement OfflinePlayer { get; private set; }
        // A networked match spawns its players through the Lobby room instead.

        private void Awake()
        {
            MessSinkRegistry.Register(this);
            stableToilets = FindObjectsByType<Toilet>(FindObjectsSortMode.None).OrderBy(t => StablePath(t.transform), System.StringComparer.Ordinal).ToArray();
            droppingPool = CreateDroppingPool();
            if (RuntimeMode.Dedicated)
            {
                foreach (var camera in FindObjectsByType<UnityEngine.Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None)) camera.enabled = false;
                if (Camera != null) Camera.gameObject.SetActive(false);
            }
        }
        private void OnDestroy() => MessSinkRegistry.Unregister(this);
        public int ToiletIndex(Toilet toilet) => System.Array.IndexOf(stableToilets, toilet);
        private static string StablePath(Transform node)
        {
            var path = string.Empty;
            for (; node != null; node = node.parent) path = "/" + node.name + "[" + node.GetSiblingIndex() + "]" + path;
            return path;
        }

        private void Start()
        {
            if (RuntimeMode.Dedicated) return;
            if (StartOffline && Match.Mode != MatchMode.Networked)
            {
                if (startOfflineRoundOnDirectPlay) Match.BeginOfflineInCurrentScene();
                Practice();
            }
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

        public int DroppingCount => messes.Count(mess => mess != null && mess.Kind == MessKind.Dropping);
        private readonly List<Mess> messes = new();
        private Toilet[] stableToilets = System.Array.Empty<Toilet>();
        public IReadOnlyList<Mess> Messes => messes;
        /// <summary>Online: the server-owned mess list (set by Network/Facilities while spawned). Offline: null, messes are local.</summary>
        public static System.Func<MessKind, Vector3, int, int> MessAuthority;
        public void Drop(Vector3 position) => AddMess(MessKind.Dropping, position);
        public int AddMess(MessKind kind, Vector3 position, int toiletIndex = -1)
        {
            if (kind == MessKind.None) return -1;
            if (kind == MessKind.DirtyToilet && messes.Any(m => m != null && m.Kind == kind && m.ToiletIndex == toiletIndex)) return -1;
            if (MessAuthority != null) return MessAuthority(kind, position, toiletIndex);
            int index = messes.Count;
            var mess = droppingPool.Get().GetComponent<Mess>();
            mess.Configure(index, kind, position, toiletIndex);
            if (kind == MessKind.Backflow && toiletIndex >= 0 && toiletIndex < stableToilets.Length) stableToilets[toiletIndex].SetBackflowShared(true);
            messes.Add(mess);
            return index;
        }
        public int AddMess(MessKind kind, Vector3 position, int toiletIndex, Movement source)
        {
            var index = AddMess(kind, position, toiletIndex);
            if (index >= 0 && index < messes.Count && messes[index] != null) messes[index].SetSource(source);
            return index;
        }
        public bool IsDirty(int toiletIndex) => messes.Any(m => m != null && m.Kind == MessKind.DirtyToilet && m.ToiletIndex == toiletIndex);
        public bool IsBackflow(int toiletIndex) => messes.Any(m => m != null && m.Kind == MessKind.Backflow && m.ToiletIndex == toiletIndex);
        /// <summary>Shows the shared mess at <paramref name="index"/> (MessKind.None removes it). Called by Network/Facilities for every list entry.</summary>
        public void ShowMess(int index, MessKind kind, Vector3 position, int toiletIndex)
        {
            while (messes.Count <= index) messes.Add(null);
            if (kind == MessKind.None) { RemoveMess(index); return; }
            if (messes[index] == null) messes[index] = droppingPool.Get().GetComponent<Mess>();
            messes[index].Configure(index, kind, position, toiletIndex);
            if (kind == MessKind.Backflow && toiletIndex >= 0 && toiletIndex < stableToilets.Length) stableToilets[toiletIndex].SetBackflowShared(true);
        }
        public void RemoveDropping(int index) => RemoveMess(index);
        public void RemoveMess(int index)
        {
            if (index < 0 || index >= messes.Count || messes[index] == null) return;
            if (messes[index].Kind == MessKind.Backflow && messes[index].ToiletIndex >= 0)
            {
                if (messes[index].ToiletIndex < stableToilets.Length) stableToilets[messes[index].ToiletIndex].SetBackflowShared(false);
            }
            droppingPool.Release(messes[index].gameObject);
            messes[index] = null;
        }
        private ObjectPool<GameObject> droppingPool;
        private ObjectPool<GameObject> CreateDroppingPool() => new(
            createFunc: () =>
            {
                var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Destroy(ball.GetComponent<Collider>());
                ball.AddComponent<Mess>();
                ball.transform.SetParent(transform, true);
                return ball;
            },
            actionOnGet: ball => ball.SetActive(true),
            actionOnRelease: ball => ball.SetActive(false),
            actionOnDestroy: Destroy,
            defaultCapacity: 8);
        public void Practice()
        {
            if (RuntimeMode.Dedicated) return;
            StopPractice();
            var instance = Instantiate(PlayerPrefab, Spawns[0].position, Spawns[0].rotation);
            instance.name = "Local practice player";
            OfflinePlayer = instance.GetComponent<Movement>();
            OfflinePlayer.SetControl(true);
            instance.GetComponent<PlayerInput>().enabled = true;
            var appearance = instance.GetComponent<PlayerAppearance>();
            appearance.SetBodyType(LocalProfile.BodyType);
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
