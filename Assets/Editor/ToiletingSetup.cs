using System;
using System.Linq;
using NatureCalls.Facility;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;
namespace NatureCalls.Editor
{
    public static class ToiletingSetup
    {
        [MenuItem("NatureCalls/Configure Toileting")]
        public static void Configure()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Playground.unity", OpenSceneMode.Single);
            var world = Object.FindFirstObjectByType<Map.Playground>();
            var fixtures = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
                .Where(t => t.name == "Urinal -2" || t.name == "Urinal -1" || t.name == "Urinal 0").ToArray();
            if (fixtures.Length != 3) throw new InvalidOperationException("Expected the three existing urinals, found " + fixtures.Length);
            foreach (var fixture in fixtures)
            {
                var bounds = fixture.GetComponentsInChildren<Renderer>().Select(r => r.bounds).Aggregate((a, b) => { a.Encapsulate(b); return a; });
                var forward = Vector3.ProjectOnPlane(world.Spawns[0].position - bounds.center, Vector3.up).normalized;
                // Fixture's depth axis, choosing the side toward the public room.
                forward = Vector3.Dot(fixture.forward, forward) >= 0 ? fixture.forward : -fixture.forward;
                forward = Vector3.ProjectOnPlane(forward, Vector3.up).normalized;
                var urinal = fixture.GetComponent<Urinal>();
                if (urinal == null) urinal = fixture.gameObject.AddComponent<Urinal>();
                urinal.Center = fixture.InverseTransformPoint(bounds.center + forward * .1f);
                var front = bounds.center + forward * .65f;
                front.y = world.Spawns[0].position.y;
                urinal.Front = fixture.InverseTransformPoint(front);
                EditorUtility.SetDirty(urinal);
                Debug.Log($"URINAL_CONFIGURED|name={fixture.name}|center={urinal.TargetCenter}|front={urinal.FrontPosition}");
            }
            const string path = "Assets/Prefabs/Toolbox.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
            {
                var root = new GameObject("Toolbox");
                var box = root.AddComponent<Toolbox>();
                var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
                visual.name = "Box"; visual.transform.SetParent(root.transform, false);
                visual.transform.localPosition = Vector3.up * .2f;
                visual.transform.localScale = new Vector3(.6f, .4f, .4f);
                var brush = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/WC/Prefabs/props/SM_brush Variant.prefab");
                var mop = brush != null ? (GameObject)PrefabUtility.InstantiatePrefab(brush) : GameObject.CreatePrimitive(PrimitiveType.Capsule);
                mop.name = "Mop"; mop.transform.SetParent(root.transform, false);
                mop.transform.localPosition = Vector3.up * .5f;
                box.Mop = mop.AddComponent<Mop>();
                if (mop.GetComponent<Collider>() == null) mop.AddComponent<BoxCollider>();
                if (mop.GetComponent<Rigidbody>() == null) mop.AddComponent<Rigidbody>();
                PrefabUtility.SaveAsPrefabAsset(root, path);
                Object.DestroyImmediate(root);
            }
            if (Object.FindFirstObjectByType<Toolbox>() == null)
            {
                var box = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path), scene);
                var origin = world.Spawns[0].position;
                bool placed = false;
                for (int x = -2; x <= 2 && !placed; x++)
                for (int z = -2; z <= 2 && !placed; z++)
                {
                    var point = origin + new Vector3(x, 0, z);
                    if (Vector3.Distance(point, origin) < 1 || Physics.CheckBox(point + Vector3.up * .25f, new Vector3(.4f, .2f, .3f))) continue;
                    if (!Physics.Raycast(point + Vector3.up * .5f, Vector3.down, 1)) continue;
                    box.transform.position = point; placed = true;
                }
                if (!placed) throw new InvalidOperationException("No clear public floor for toolbox.");
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            // Save affected network contracts through Unity so obsolete serialized list fields disappear.
            foreach (var prefabPath in new[] { "Assets/Prefabs/Characters/Player.prefab", "Assets/Prefabs/Network/Facilities.prefab" })
            {
                var root = PrefabUtility.LoadPrefabContents(prefabPath);
                try { PrefabUtility.SaveAsPrefabAsset(root, prefabPath); }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            AssetDatabase.SaveAssets();
            Automation.Validate();
        }
    }
}
