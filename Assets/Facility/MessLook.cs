using NatureCalls.Core;
using NatureCalls.Player;
using UnityEngine;

namespace NatureCalls.Facility
{
    /// <summary>
    /// The formal look of a shared Mess (GLOSSARY Mess/StoolKind): model, URP material and odour particles by kind, plus
    /// the slippery floor of loose stool and accidents (same inertia as a WetZone). Mess.Configure calls <see cref="Apply"/>.
    /// Presentation only: kind, position and index come from the shared mess list, so every peer shows the same thing.
    /// </summary>
    public sealed class MessLook : MonoBehaviour, IMovementEffectSource
    {
        public MessKind Kind { get; private set; }
        public float SlipRadius { get; private set; }
        public float OdorWeight { get; private set; }
        public Wisps Odor { get; private set; }
        /// <summary>The visible model; a separate object so the Mess transform (owned by the mess list) stays as configured.</summary>
        public GameObject Look { get; private set; }
        private MeshFilter filter;
        private MeshRenderer meshRenderer;
        private GameObject extra;
        private bool registered;

        /// <summary>Hook for Mess.Configure: dresses the pooled object for its kind at <paramref name="position"/>.</summary>
        public static void Apply(Component mess, MessKind kind, Vector3 position)
        {
            if (mess == null) return;
            var look = mess.GetComponent<MessLook>();
            if (look == null) look = mess.gameObject.AddComponent<MessLook>();
            look.Show(kind, position);
        }

        private void Show(MessKind kind, Vector3 position)
        {
            Kind = kind;
            if (Look == null)
            {
                Look = new GameObject(name + " look", typeof(MeshFilter), typeof(MeshRenderer));
                Look.transform.SetParent(transform.parent, false);
                filter = Look.GetComponent<MeshFilter>(); meshRenderer = Look.GetComponent<MeshRenderer>();
            }
            Look.name = kind + " look";
            Look.SetActive(isActiveAndEnabled);
            float seed = Hash(position);
            var yaw = Quaternion.Euler(0, seed * 360f, 0);
            SlipRadius = kind is MessKind.LooseStool or MessKind.Accident ? .35f * (kind == MessKind.Accident ? 1.3f : 1f) : 0f;
            OdorWeight = StoolRules.Odor(kind);
            var art = MessArt.Current;
            if (extra != null) extra.SetActive(false);
            if (art != null)
            {
                var floor = FloorBelow(position);
                switch (kind)
                {
                    case MessKind.Dropping:
                        Dress(seed < .5f ? art.SoftSwirl : art.SoftSquashed, new[] { art.Stool }, floor, yaw, 1f);
                        break;
                    case MessKind.LooseStool:
                        Dress(art.LooseSplat, new[] { art.Loose, art.Bubble }, floor, yaw, .8f);
                        break;
                    case MessKind.Accident:
                        Dress(art.LooseSplat, new[] { art.Loose, art.Bubble }, floor, yaw, 1.05f);
                        Extra(art.SoftSquashed, art.Stool, new Vector3(.03f, .02f, -.02f), 1.25f);
                        break;
                    case MessKind.Vomit:
                        Dress(art.Vomit, new[] { art.VomitBase, art.VomitChunk }, floor, yaw, 1f);
                        break;
                    case MessKind.Puddle:
                        Dress(art.UrinePuddle, new[] { art.Urine }, floor + Vector3.up * .003f, yaw, 1f);
                        break;
                    case MessKind.Backflow:
                        Dress(art.LooseSplat, new[] { art.Backflow, art.BackflowBubble }, floor, yaw, 1.2f);
                        break;
                    case MessKind.DirtyToilet:
                        var toilet = NearestToilet(position);
                        var facing = toilet != null ? Quaternion.Euler(0, toilet.transform.eulerAngles.y, 0) : Quaternion.identity;
                        Dress(art.ToiletSmear, new[] { art.Smear }, position + Vector3.down * SmearDepth, facing, 1f);
                        break;
                }
            }
            if (!RuntimeMode.Dedicated && art != null && art.Stink != null && OdorWeight > 0)
            {
                if (Odor == null) Odor = MessFx.CreateOdor(Look.transform, art.Stink);
                MessFx.SetOdor(Odor, OdorWeight, kind == MessKind.Puddle ? .25f : .2f);
            }
            Register(SlipRadius > 0 && isActiveAndEnabled);
        }

        /// <summary>How far below Toilet.TargetCenter the smear's bowl bottom sits.</summary>
        public static float SmearDepth = .15f;

        private void Dress(Mesh mesh, Material[] materials, Vector3 position, Quaternion rotation, float scale)
        {
            if (mesh == null) return;
            filter.sharedMesh = mesh;
            meshRenderer.sharedMaterials = materials;
            Look.transform.SetPositionAndRotation(position, rotation);
            Look.transform.localScale = Vector3.one * scale;
            var own = GetComponent<Renderer>(); if (own != null) own.enabled = false;
        }

        private void Extra(Mesh mesh, Material material, Vector3 offset, float scale)
        {
            if (mesh == null) return;
            if (extra == null)
            {
                extra = new GameObject("Heap", typeof(MeshFilter), typeof(MeshRenderer));
                extra.transform.SetParent(Look.transform, false);
            }
            extra.SetActive(true);
            extra.GetComponent<MeshFilter>().sharedMesh = mesh;
            extra.GetComponent<MeshRenderer>().sharedMaterial = material;
            extra.transform.localPosition = offset; extra.transform.localRotation = Quaternion.identity;
            extra.transform.localScale = Vector3.one * scale / Mathf.Max(.01f, Look.transform.localScale.x);
        }

        /// <summary>Dresses a pooled hard stool: a cracked log or a string of pellets.</summary>
        public static void DressHardStool(HardStool stool, int index)
        {
            var art = MessArt.Current;
            var filter = stool.GetComponent<MeshFilter>(); if (filter == null) filter = stool.gameObject.AddComponent<MeshFilter>();
            var renderer = stool.GetComponent<MeshRenderer>(); if (renderer == null) renderer = stool.gameObject.AddComponent<MeshRenderer>();
            var mesh = art != null ? (index % 2 == 0 ? art.HardLog : art.HardPellets) : null;
            if (mesh == null)
            {
                var temp = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                mesh = temp.GetComponent<MeshFilter>().sharedMesh; Destroy(temp);
                stool.transform.localScale = new Vector3(.06f, .1f, .06f);
            }
            filter.sharedMesh = mesh;
            if (art != null && art.StoolDry != null) renderer.sharedMaterial = art.StoolDry;
            var box = stool.GetComponent<BoxCollider>();
            if (box != null) { box.center = mesh.bounds.center; box.size = Vector3.Max(mesh.bounds.size, new Vector3(.04f, .04f, .04f)); }
            if (!RuntimeMode.Dedicated && art != null && art.Stink != null)
                MessFx.SetOdor(MessFx.CreateOdor(stool.transform, art.Stink), StoolRules.Odor(StoolKind.Hard), .08f);
        }

        private void OnEnable() { Register(SlipRadius > 0); if (Look != null) Look.SetActive(true); }
        private void OnDisable() { Register(false); if (Look != null) Look.SetActive(false); }
        private void OnDestroy() { if (Look != null) Destroy(Look); }
        private void Register(bool value)
        {
            if (value == registered) return;
            registered = value;
            if (value) MovementEffectRegistry.Register(this); else MovementEffectRegistry.Unregister(this);
        }

        public bool AppliesTo(Movement player)
        {
            if (player == null || SlipRadius <= 0 || !isActiveAndEnabled) return false;
            var delta = player.transform.position - (Look != null ? Look.transform.position : transform.position);
            return Mathf.Abs(delta.y) < .4f && Vector3.ProjectOnPlane(delta, Vector3.up).magnitude <= SlipRadius;
        }
        public MovementModifier GetModifier(Movement player) => new MovementModifier(1, StoolRules.LooseInertia);

        private static float Hash(Vector3 p) => Mathf.Abs(Mathf.Sin(p.x * 12.9898f + p.z * 78.233f + p.y * 37.719f) * 43758.5453f) % 1f;

        /// <summary>The floor under a point, ignoring players and items; the point itself if nothing is found.</summary>
        public static Vector3 FloorBelow(Vector3 position)
        {
            var hits = Physics.RaycastAll(position + Vector3.up * .3f, Vector3.down, 1.5f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue; var floor = position;
            foreach (var hit in hits)
            {
                if (hit.collider.GetComponentInParent<Movement>() != null || hit.collider.GetComponentInParent<Item>() != null) continue;
                if (hit.collider.GetComponentInParent<Toilet>() != null) continue;
                if (hit.distance < best) { best = hit.distance; floor = hit.point; }
            }
            return floor;
        }

        private static Toilet NearestToilet(Vector3 position)
        {
            Toilet best = null; float distance = float.MaxValue;
            foreach (var toilet in FindObjectsByType<Toilet>(FindObjectsSortMode.None))
            {
                float d = (toilet.TargetCenter - position).sqrMagnitude;
                if (d < distance) { distance = d; best = toilet; }
            }
            return best;
        }
    }
}
