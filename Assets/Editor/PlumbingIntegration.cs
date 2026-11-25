using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using NatureCalls.Facility;
using NatureCalls.Map;

namespace NatureCalls.Editor
{
    public static class PlumbingIntegration
    {
        public static void Apply()
        {
            ApplyScene("Assets/Scenes/Playground.unity", false);
            ApplyScene("Assets/Scenes/Map2.unity", true);
            AssetDatabase.SaveAssets();
        }

        private static void ApplyScene(string path, bool map2)
        {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            var world = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<NatureCalls.Map.Playground>(true)).SingleOrDefault();
            if (world == null) throw new InvalidOperationException($"No Playground in {path}");
            var sinks = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .Where(t => (t.name.IndexOf("sink", StringComparison.OrdinalIgnoreCase) >= 0 || t.name.IndexOf("washstand", StringComparison.OrdinalIgnoreCase) >= 0) && t.GetComponent<Sink>() == null)
                .Where(t => t.GetComponentInChildren<Renderer>(true) != null).Take(map2 ? 1 : 2).ToList();
            if (sinks.Count == 0 && !map2)
            {
                var go = new GameObject("Handwashing sink"); go.transform.SetParent(world.transform, false); go.transform.position = new Vector3(4, .5f, 3);
                var collider = go.AddComponent<BoxCollider>(); collider.size = new Vector3(1.2f, 1f, .6f); sinks.Add(go.transform);
            }
            foreach (var transform in sinks)
            {
                var sink = transform.gameObject.AddComponent<Sink>();
                if (transform.GetComponentInChildren<Collider>(true) == null)
                { var box = transform.gameObject.AddComponent<BoxCollider>(); box.center = Vector3.up * .5f; box.size = Vector3.one; }
                EditorUtility.SetDirty(sink);
            }
            var fixtures = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<MonoBehaviour>(true))
                .Where(component => component is Toilet || component is Urinal || component is Sink).ToArray();
            var pipeObject = world.transform.Find("Floor 1 plumbing pipe");
            if (pipeObject == null) { pipeObject = new GameObject("Floor 1 plumbing pipe").transform; pipeObject.SetParent(world.transform, false); }
            var pipe = pipeObject.GetComponent<Pipe>() ?? pipeObject.gameObject.AddComponent<Pipe>();
            pipe.SetFixtures(fixtures);
            foreach (var fixture in fixtures)
            {
                if (fixture is Toilet toilet) { toilet.SetPipe(pipe); var target = toilet.transform.Find("Flush handle target"); if (target == null) { target = new GameObject("Flush handle target").transform; target.SetParent(toilet.transform, false); target.localPosition = new Vector3(0, .8f, .65f); } toilet.SetFlushTarget(target); PrefabUtility.RecordPrefabInstancePropertyModifications(toilet); }
                else if (fixture is Urinal urinal) urinal.SetPipe(pipe);
                else if (fixture is Sink sink) sink.SetPipe(pipe);
            }
            foreach (var repair in scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Map2Repair>(true))) repair.SetPipes(new[] { pipe });
            ApplyVersus(world, scene, map2);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException($"Could not save {path}");
            Debug.Log($"PLUMBING_INTEGRATION_OK|scene={path}|fixtures={fixtures.Length}|sinks={sinks.Count}");
        }

        private static void ApplyVersus(NatureCalls.Map.Playground world, Scene scene, bool map2)
        {
            foreach (var toilet in scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Toilet>(true)))
            {
                var target = toilet.Target.gameObject;
                var broken = target.GetComponent<Breakable>() ?? target.AddComponent<Breakable>();
                broken.TargetComponent = toilet;
                EditorUtility.SetDirty(broken);
            }
            foreach (var sink in scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Sink>(true)))
            {
                var broken = sink.gameObject.GetComponent<Breakable>() ?? sink.gameObject.AddComponent<Breakable>();
                broken.TargetComponent = sink;
                EditorUtility.SetDirty(broken);
            }
            var box = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Toolbox>(true)).FirstOrDefault();
            if (box != null)
            {
                box.Wrench = EnsureTool<Wrench>(box.transform, "Wrench");
                box.Plunger = EnsureTool<Plunger>(box.transform, "Plunger");
                EditorUtility.SetDirty(box);
            }
            if (map2)
                foreach (var valve in scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true)).Where(t => t.name.IndexOf("valve", StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    var broken = valve.GetComponent<Breakable>() ?? valve.gameObject.AddComponent<Breakable>();
                    EditorUtility.SetDirty(broken);
                }
        }

        private static T EnsureTool<T>(Transform parent, string name) where T : Item
        {
            var child = parent.Find(name);
            if (child == null) { child = new GameObject(name).transform; child.SetParent(parent, false); child.localPosition = Vector3.up * .2f; child.gameObject.AddComponent<BoxCollider>().size = Vector3.one * .2f; }
            return child.GetComponent<T>() ?? child.gameObject.AddComponent<T>();
        }
    }
}
