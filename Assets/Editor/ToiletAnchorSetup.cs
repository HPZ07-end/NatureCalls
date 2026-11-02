using System;
using System.Linq;
using NatureCalls.Facility;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NatureCalls.Editor
{
    /// <summary>
    /// Restores the Toilet interaction (posture Anchor and visible Interior) on the WCStall bowl,
    /// which the WC map rebuild left without one. The Anchor stands in front of the bowl, facing it,
    /// on the door side. Poses are measured on the Playground stall instances (which override the
    /// prefab layout) and stored relative to the bowl, so every instance shares them.
    /// </summary>
    public static class ToiletAnchorSetup
    {
        private const string StallPrefabPath = "Assets/Prefabs/Levels/WCStall.prefab";
        private const string ScenePath = "Assets/Scenes/Playground.unity";
        private const string BowlName = "Toilet";
        private const string AnchorName = "Posture anchor";
        private const string InteriorName = "Toilet interior";
        // Clearance between the bowl front and the anchor: at least the player's capsule radius.
        private const float AnchorGap = .4f;
        private const float PlayerRadius = .32f;
        private const float PlayerHeight = 2.05f;

        [MenuItem("NatureCalls/Configure WC Stall Toilet")]
        public static void Configure()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var doors = Object.FindObjectsByType<StallDoor>(FindObjectsInactive.Include, FindObjectsSortMode.InstanceID);
            if (doors.Length == 0) throw new InvalidOperationException("Playground has no WC stalls.");
            Measure(doors[0], out var anchorPosition, out var anchorRotation, out var interiorPosition);

            var root = PrefabUtility.LoadPrefabContents(StallPrefabPath);
            try
            {
                var bowl = FindBowl(root.transform);
                var anchor = Child(bowl, AnchorName);
                anchor.localPosition = anchorPosition;
                anchor.localRotation = anchorRotation;
                var interior = Child(bowl, InteriorName);
                interior.localPosition = interiorPosition;
                interior.localRotation = Quaternion.identity;
                var marker = GetOrAdd<BoxCollider>(interior.gameObject);
                marker.isTrigger = true;
                marker.size = new Vector3(.2f, .02f, .2f);
                var toilet = GetOrAdd<Toilet>(bowl.gameObject);
                toilet.Anchor = anchor;
                toilet.Interior = interior;
                PrefabUtility.SaveAsPrefabAsset(root, StallPrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }

            Verify();
        }

        // Anchor and interior in the bowl's local space, measured on one placed stall.
        private static void Measure(StallDoor door, out Vector3 anchorPosition, out Quaternion anchorRotation, out Vector3 interiorPosition)
        {
            var bowl = FindBowl(door.transform);
            var bounds = BowlBounds(bowl);
            // Straight out of the stall along its depth axis (the hinge is off to one side).
            var axis = Flat(door.transform.forward).normalized;
            var entrance = Vector3.Dot(door.DoorColliderPivot.position - bounds.center, axis) >= 0 ? axis : -axis;
            var reach = Mathf.Abs(entrance.x) * bounds.extents.x + Mathf.Abs(entrance.z) * bounds.extents.z;
            var floor = door.transform.position.y;
            var anchorWorld = new Vector3(bounds.center.x, floor, bounds.center.z) + entrance * (reach + AnchorGap);
            var interiorWorld = new Vector3(bounds.center.x, bounds.min.y + .4f, bounds.center.z) + entrance * (reach * .35f);
            anchorPosition = bowl.InverseTransformPoint(anchorWorld);
            anchorRotation = Quaternion.Inverse(bowl.rotation) * Quaternion.LookRotation(-entrance, Vector3.up);
            interiorPosition = bowl.InverseTransformPoint(interiorWorld);
        }

        // Re-opens the scene and checks every stall: anchor inside the stall and clear of colliders when standing.
        private static void Verify()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Physics.SyncTransforms();
            foreach (var door in Object.FindObjectsByType<StallDoor>(FindObjectsInactive.Include, FindObjectsSortMode.InstanceID))
            {
                var toilet = door.GetComponentInChildren<Toilet>(true);
                if (toilet == null || toilet.Anchor == null) throw new InvalidOperationException(door.name + " has no configured toilet.");
                var anchor = toilet.Anchor.position;
                var inside = door.transform.InverseTransformPoint(anchor).z - door.transform.InverseTransformPoint(door.DoorLeaf.position).z;
                var bottom = anchor + Vector3.up * (PlayerRadius + .02f);
                var top = anchor + Vector3.up * (PlayerHeight - PlayerRadius);
                var hits = Physics.OverlapCapsule(bottom, top, PlayerRadius, 1, QueryTriggerInteraction.Ignore);
                Debug.Log($"WC_TOILET_ANCHOR|stall={door.name}|anchor={anchor}|insideDepth={inside:F2}|doorPivot={Vector3.Distance(anchor, door.DoorColliderPivot.position):F2}|" +
                    $"overlaps={string.Join(",", hits.Select(hit => hit.name))}");
                if (inside <= .05f) throw new InvalidOperationException(door.name + ": toilet anchor is outside the stall.");
                if (hits.Length > 0) throw new InvalidOperationException(door.name + ": a standing player at the toilet anchor overlaps " + hits[0].name);
            }
        }

        private static Transform FindBowl(Transform stall) =>
            stall.GetComponentsInChildren<Transform>(true).FirstOrDefault(item => item.name == BowlName)
            ?? throw new InvalidOperationException(stall.name + " has no Toilet bowl child.");

        private static Bounds BowlBounds(Transform bowl)
        {
            var renderers = bowl.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) throw new InvalidOperationException("Toilet bowl has no renderer.");
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            return bounds;
        }

        private static T GetOrAdd<T>(GameObject owner) where T : Component
        {
            // GetComponent can return Unity's fake null in the Editor, which ?? does not treat as null.
            var found = owner.GetComponent<T>();
            return found != null ? found : owner.AddComponent<T>();
        }

        private static Vector3 Flat(Vector3 value) => new(value.x, 0, value.z);

        private static Transform Child(Transform parent, string name)
        {
            var child = parent.Find(name);
            if (child != null) return child;
            child = new GameObject(name).transform;
            child.SetParent(parent, false);
            return child;
        }
    }
}
