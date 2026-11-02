using System;
using System.Linq;
using NatureCalls.Facility;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NatureCalls.Editor
{
    /// <summary>
    /// Restores the Toilet interaction (posture Anchor and visible Interior) on the WCStall bowl,
    /// which the WC map rebuild left without one. The Anchor stands in front of the bowl facing it.
    /// </summary>
    public static class ToiletAnchorSetup
    {
        private const string StallPrefabPath = "Assets/Prefabs/Levels/WCStall.prefab";
        private const string BowlName = "Toilet";
        private const string AnchorName = "Posture anchor";
        private const string InteriorName = "Toilet interior";
        private const float AnchorGap = .3f;

        [MenuItem("NatureCalls/Configure WC Stall Toilet")]
        public static void Configure()
        {
            var root = PrefabUtility.LoadPrefabContents(StallPrefabPath);
            try
            {
                var bowl = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(item => item.name == BowlName)
                    ?? throw new InvalidOperationException("WCStall has no Toilet bowl child.");
                var door = root.GetComponent<StallDoor>();
                if (door == null) throw new InvalidOperationException("WCStall has no StallDoor.");
                var renderers = bowl.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length == 0) throw new InvalidOperationException("Toilet bowl has no renderer.");
                var bounds = renderers[0].bounds;
                foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);

                var entrance = Flat(LeafCenter(door) - bounds.center).normalized;
                var reach = Mathf.Abs(Vector3.Dot(bounds.extents, new Vector3(Mathf.Abs(entrance.x), 0, Mathf.Abs(entrance.z))));
                var anchor = Child(bowl, AnchorName);
                anchor.SetPositionAndRotation(
                    new Vector3(bounds.center.x, bounds.min.y, bounds.center.z) + entrance * (reach + AnchorGap),
                    Quaternion.LookRotation(-entrance, Vector3.up));

                var interior = Child(bowl, InteriorName);
                // The bowl opening: toward the entrance from the tank, at seat height.
                interior.SetPositionAndRotation(new Vector3(bounds.center.x, bounds.min.y + .4f, bounds.center.z) + entrance * (reach * .35f), Quaternion.identity);
                var marker = GetOrAdd<BoxCollider>(interior.gameObject);
                marker.isTrigger = true;
                marker.size = new Vector3(.2f, .02f, .2f);

                var toilet = GetOrAdd<Toilet>(bowl.gameObject);
                toilet.Anchor = anchor;
                toilet.Interior = interior;
                PrefabUtility.SaveAsPrefabAsset(root, StallPrefabPath);

                Debug.Log($"WC_TOILET_CONFIGURED|bowlSize={bounds.size}|anchor={anchor.position}|interior={interior.position}|" +
                    $"anchorToDoorPivot={Vector3.Distance(anchor.position, door.DoorColliderPivot.position):F2}|" +
                    $"doorReach={door.InteractionDistance:F2}|toiletReach={toilet.InteractionDistance:F2}");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static Vector3 LeafCenter(StallDoor door)
        {
            var renderers = door.DoorLeaf.GetComponentsInChildren<Renderer>(true);
            return renderers.Length == 0 ? door.DoorLeaf.position : renderers.Select(item => item.bounds.center).Aggregate(Vector3.zero, (sum, item) => sum + item) / renderers.Length;
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
