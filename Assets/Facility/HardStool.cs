using System;
using System.Collections.Generic;
using NatureCalls.Core;
using NatureCalls.Player;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NatureCalls.Facility
{
    /// <summary>
    /// GLOSSARY Mess/StoolKind Hard: a stool with momentum. Walking into it kicks it; E picks it up at a Composure cost and
    /// R throws it like any item; landing in a toilet bowl adds its pipe load. Each map scene gets a small stored pool
    /// (<see cref="StoolPool"/>) so hard stools ride the existing SharedItems sync instead of the mess list.
    /// </summary>
    [RequireComponent(typeof(Rigidbody), typeof(BoxCollider))]
    public sealed class HardStool : Item, IDirtSource
    {
        // Online glue set by Network/StoolSync; null offline.
        /// <summary>Server: publishes this item's state after a spawn or a flush.</summary>
        public static Action<HardStool> Publish;
        /// <summary>True when this peer decides spawns and flushes (offline, or the server).</summary>
        public static Func<bool> IsAuthority;
        /// <summary>Online kick request; returns false when offline so the kick is simulated locally.</summary>
        public static Func<HardStool, Movement, Vector3, bool> RequestKick;

        public const float KickRadius = .6f, KickSpeed = 3.2f, KickLift = 1.4f, BowlRadius = .22f;
        public bool InPlay => !Stored;
        public bool SaboteurSource { get; private set; }
        public Vector3 DirtPosition => transform.position;
        public float DirtWeight => InPlay ? StoolRules.DirtWeight(StoolKind.Hard) * (SaboteurSource ? .3f : 1f) : 0f;
        public event Action<Toilet> Flushed;
        public event Action<Movement> Kicked;

        private Vector3 home;
        private Movement lastCarrier;
        private float nextKick;

        protected override void Awake()
        {
            base.Awake();
            PromptLabel = "捡起硬便（扣镇定值）";
            InteractionDistance = 1.3f; ThrowSpeed = 5f; ThrowUpwardSpeed = 1.8f;
            home = transform.position;
            var body = GetComponent<Rigidbody>(); body.mass = .3f; body.linearDamping = .3f; body.angularDamping = .5f;
            MessLook.DressHardStool(this, transform.GetSiblingIndex());
            Store(home);
        }
        protected override void OnEnable() { base.OnEnable(); DirtField.Register(this); }
        protected override void OnDisable() { DirtField.Unregister(this); base.OnDisable(); }

        private static bool Authority => IsAuthority == null || IsAuthority();

        /// <summary>Authority: takes a stored stool from the pool and leaves it at <paramref name="position"/>.</summary>
        public static bool Spawn(Vector3 position, Movement source)
        {
            if (!Authority) return false;
            foreach (var stool in StoolPool.Stools)
            {
                if (stool == null || !stool.Stored) continue;
                stool.SaboteurSource = RoleRegistry.IsSaboteur(source);
                stool.ApplySharedRest(position + Vector3.up * .01f, Quaternion.Euler(0, UnityEngine.Random.Range(0f, 360f), 0));
                Publish?.Invoke(stool);
                return true;
            }
            return false;
        }

        /// <summary>Authority: the stool fell into a toilet; it joins that pipe and returns to the pool.</summary>
        public void IntoToilet(Toilet toilet)
        {
            StoolOutput.Bowl(toilet, StoolKind.Hard);
            Store(home);
            Publish?.Invoke(this);
            Flushed?.Invoke(toilet);
        }

        public void Kick(Movement player, Vector3 velocity)
        {
            nextKick = Time.time + .4f;
            Kicked?.Invoke(player);
            if (RequestKick != null && RequestKick(this, player, velocity)) return;
            ApplySharedThrow(transform.position + Vector3.up * .03f, transform.rotation, velocity, true);
        }

        public static Vector3 KickVelocity(Vector3 from, Vector3 stool, Vector3 forward)
        {
            var away = Vector3.ProjectOnPlane(stool - from, Vector3.up);
            var dir = (away.normalized + Vector3.ProjectOnPlane(forward, Vector3.up).normalized).normalized;
            if (dir.sqrMagnitude < .01f) dir = Vector3.forward;
            return dir * KickSpeed + Vector3.up * KickLift;
        }

        // LateUpdate: Item.Update (flight following) must keep running.
        private void LateUpdate()
        {
            // Picking it up costs Composure (applied where needs tick: offline or the server).
            if (PickedUpBy != lastCarrier)
            {
                if (PickedUpBy != null) PickedUpBy.GetComponent<PlayerNeeds>()?.AddDisgust(StoolRules.PickupComposureCost);
                lastCarrier = PickedUpBy;
            }
            if (!InPlay || IsPickedUp) return;
            if (Time.time >= nextKick) TryKick();
            if (Authority) TryBowl();
        }

        // Only the walker's own machine kicks, so every kick has exactly one simulator. The radius covers the capsule
        // (0.32 m) plus the stool, so the kick lands before the walker is blocked by it.
        private void TryKick()
        {
            foreach (var player in StoolPool.Players)
            {
                if (player == null || !player.Controlled || player.Seat != null) continue;
                if (!StoolPool.Motion(player, out var from, out var to, out var velocity)) continue;
                // Swept: the walker's path this frame, so a long frame cannot step across the stool.
                var segment = Vector3.ProjectOnPlane(to - from, Vector3.up);
                var rel = Vector3.ProjectOnPlane(transform.position - from, Vector3.up);
                float along = segment.sqrMagnitude > 1e-6f ? Mathf.Clamp01(Vector3.Dot(rel, segment) / segment.sqrMagnitude) : 1f;
                var closest = from + segment * along;
                var delta = transform.position - closest;
                if (Mathf.Abs(transform.position.y - to.y) > .6f || Vector3.ProjectOnPlane(delta, Vector3.up).magnitude > KickRadius) continue;
                if (velocity.magnitude < .4f) continue;
                Kick(player, KickVelocity(closest, transform.position, velocity));
                return;
            }
        }

        private void TryBowl()
        {
            foreach (var toilet in StoolPool.Toilets)
            {
                if (toilet == null || toilet.Interior == null && toilet.Anchor == null) continue;
                var delta = transform.position - toilet.TargetCenter;
                if (delta.y < -.3f || delta.y > .25f || Vector3.ProjectOnPlane(delta, Vector3.up).magnitude > BowlRadius) continue;
                IntoToilet(toilet);
                return;
            }
        }
    }

    /// <summary>Creates the stored hard-stool pool in every loaded map scene (one that registers an IMessSink).</summary>
    public static class StoolPool
    {
        public const int Size = 6;
        public static readonly List<HardStool> Stools = new();
        public static Toilet[] Toilets { get; private set; } = Array.Empty<Toilet>();
        private static readonly List<Movement> players = new();
        private static float playersRefreshed = -1;
        public static IReadOnlyList<Movement> Players
        {
            get
            {
                if (Time.time - playersRefreshed > .5f || playersRefreshed < 0)
                {
                    players.Clear(); players.AddRange(UnityEngine.Object.FindObjectsByType<Movement>(FindObjectsSortMode.None));
                    playersRefreshed = Time.time;
                }
                return players;
            }
        }

        private static readonly Dictionary<Movement, (Vector3 from, Vector3 to, int frame, float dt)> motion = new();

        /// <summary>The player's horizontal path over the last frame and its velocity (tracked here, not from the controller,
        /// which reports zero once it is blocked).</summary>
        public static bool Motion(Movement player, out Vector3 from, out Vector3 to, out Vector3 velocity)
        {
            var now = player.transform.position;
            if (!motion.TryGetValue(player, out var entry)) entry = (now, now, -1, 0);
            // A gap in tracking (or a teleport) starts a fresh path rather than one long sweep.
            if (entry.frame != Time.frameCount) { entry = (entry.frame == Time.frameCount - 1 ? entry.to : now, now, Time.frameCount, Time.deltaTime); motion[player] = entry; }
            from = entry.from; to = entry.to;
            velocity = entry.dt > 0 ? Vector3.ProjectOnPlane(to - from, Vector3.up) / entry.dt : Vector3.zero;
            return entry.dt > 0;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Init()
        {
            Stools.Clear(); Toilets = Array.Empty<Toilet>(); playersRefreshed = -1; motion.Clear();
            SceneManager.sceneLoaded -= Loaded; SceneManager.sceneLoaded += Loaded;
        }

        private static void Loaded(Scene scene, LoadSceneMode mode) => EnsureIn(scene);

        /// <summary>Builds the pool once per map scene; safe to call again.</summary>
        public static void EnsureIn(Scene scene)
        {
            Stools.RemoveAll(stool => stool == null);
            if (!(MessSinkRegistry.Current is MonoBehaviour sink) || sink == null || sink.gameObject.scene != scene) return;
            Toilets = UnityEngine.Object.FindObjectsByType<Toilet>(FindObjectsSortMode.None);
            motion.Clear(); playersRefreshed = -1;
            foreach (var root in scene.GetRootGameObjects()) if (root.name == "HardStoolPool") return;
            var pool = new GameObject("HardStoolPool");
            SceneManager.MoveGameObjectToScene(pool, scene);
            pool.transform.position = new Vector3(0, -40, 0);
            for (int i = 0; i < Size; i++)
            {
                var item = new GameObject("HardStool " + i);
                item.transform.SetParent(pool.transform, false);
                item.transform.localPosition = new Vector3(i * .5f, 0, 0);
                Stools.Add(item.AddComponent<HardStool>());
            }
        }
    }
}
