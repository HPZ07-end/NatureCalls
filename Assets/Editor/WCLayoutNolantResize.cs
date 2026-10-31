using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NatureCalls.Editor
{
    public static class WCLayoutNolantResize
    {
        private const string ScenePath = "Assets/Scenes/Playground.unity";
        private const string LayoutName = "WC Layout v2";
        private const string NolantPath = "Assets/Stylized NPC - Peasant Nolant/Prefabs/Peasant Nolant Blue(Free Version).prefab";
        private const string WallPath = "Assets/UnityTechnologies/Basic Asset Pack Interior/Prefabs/Walls/Wall2m.prefab";

        [MenuItem("NatureCalls/Resize WC Layout For Nolant")]
        public static void Resize()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath) throw new InvalidOperationException($"Open {ScenePath} first.");

            var root = scene.GetRootGameObjects().SingleOrDefault(item => item.name == LayoutName);
            if (root == null) throw new InvalidOperationException($"Missing active layout root: {LayoutName}");

            var npcHeight = MeasurePrefabHeight(NolantPath);
            var baseWallHeight = MeasurePrefabHeight(WallPath);
            var targetWallHeight = Mathf.Ceil((npcHeight + .75f) * 4f) / 4f;
            var wallScaleY = targetWallHeight / baseWallHeight;

            Undo.SetCurrentGroupName("Resize WC room for Nolant");
            var undoGroup = Undo.GetCurrentGroup();

            var shell = RequireChild(root.transform, child => child.name.Contains("room shell"), "room shell");
            Undo.RecordObject(shell.gameObject, "Rename resized room shell");
            shell.name = "8m x 10m room shell";

            var floorGroup = RequireChild(shell, child => child.name == "Floor - 2m square grid", "floor group");
            var wallGroup = RequireChild(shell, child => child.name == "Closed outer walls", "wall group");

            var floorPositions = new List<Vector3>();
            for (var z = -4; z <= 4; z += 2)
                for (var x = -3; x <= 3; x += 2)
                    floorPositions.Add(new Vector3(x, 0, z));
            ReuseChildren(floorGroup, floorPositions, "Floor", 0, Vector3.one);

            ReuseWallSide(wallGroup, "Back wall", new[] { -3f, -1f, 1f, 3f }.Select(x => new Vector3(x, 0, 4)), 0, wallScaleY);
            ReuseWallSide(wallGroup, "Front wall", new[] { -1f, 1f, 3f, 5f }.Select(x => new Vector3(x, 0, -6)), 180, wallScaleY);
            ReuseWallSide(wallGroup, "Left wall", new[] { -4f, -2f, 0f, 2f, 4f }.Select(z => new Vector3(-3, 0, z)), 90, wallScaleY);
            ReuseWallSide(wallGroup, "Right wall", new[] { -6f, -4f, -2f, 0f, 2f }.Select(z => new Vector3(5, 0, z)), -90, wallScaleY);

            SetGroupX(root.transform, "Three reusable WC stalls", new Dictionary<string, float>
            {
                ["WC Stall 1"] = -1.00f,
                ["WC Stall 2"] = .35f,
                ["WC Stall 3"] = 1.70f,
            });
            SetFixtureGroupX(root.transform, "Right wall urinals", child => child.name.StartsWith("Urinal divider", StringComparison.Ordinal) ? 4.990f : 4.979f);
            SetFixtureGroupX(root.transform, "Left wall sinks and mirrors", child => -2.990f);

            var storage = RequireChild(root.transform, child => child.name == "Storage and paper", "storage group");
            foreach (Transform child in storage)
            {
                Undo.RecordObject(child, "Move storage with resized room");
                var position = child.localPosition;
                position = child.name switch
                {
                    "Back-left shelf" => new Vector3(-2.859f, position.y, 3.385f),
                    "Paper roll 1" => new Vector3(-3.059f, position.y, 3.285f),
                    "Paper roll 2" => new Vector3(-2.759f, position.y, 3.285f),
                    "Paper roll 3" => new Vector3(-2.909f, position.y, 3.555f),
                    "Front shelf near urinals" => new Vector3(2.330f, position.y, -5.738f),
                    _ => position,
                };
                child.localPosition = position;
                if (child.name == "Front shelf near urinals") child.localRotation = Quaternion.Euler(0, 90, 0);
            }

            var activeFloors = floorGroup.Cast<Transform>().Count(child => child.gameObject.activeSelf);
            var activeWalls = wallGroup.Cast<Transform>().Count(child => child.gameObject.activeSelf);
            var wallHeightsMatch = wallGroup.Cast<Transform>()
                .Where(child => child.gameObject.activeSelf)
                .All(child => Mathf.Abs(child.localScale.y - wallScaleY) < .0001f);
            if (activeFloors != 20 || activeWalls != 18 || !wallHeightsMatch)
                throw new InvalidOperationException($"Resize verification failed: floors={activeFloors}, walls={activeWalls}, wallScaleMatch={wallHeightsMatch}.");

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save Playground.unity.");
            AssetDatabase.SaveAssets();
            Undo.CollapseUndoOperations(undoGroup);
            Selection.activeGameObject = root;
            SceneView.lastActiveSceneView?.FrameSelected();
            Debug.Log($"NOLANT_RESIZE_OK|npcHeight={npcHeight:F3}|wallHeight={targetWallHeight:F2}|room=8x10|floorTiles={activeFloors}|walls={activeWalls}|wallScaleY={wallScaleY:F4}");
        }

        [MenuItem("NatureCalls/Align WC Fixtures To Walls")]
        public static void AlignFixturesToWalls()
        {
            const float clearance = .01f;
            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath) throw new InvalidOperationException($"Open {ScenePath} first.");
            var root = scene.GetRootGameObjects().SingleOrDefault(item => item.name == LayoutName);
            if (root == null) throw new InvalidOperationException($"Missing active layout root: {LayoutName}");

            var shell = RequireChild(root.transform, child => child.name.Contains("room shell"), "room shell");
            var wallGroup = RequireChild(shell, child => child.name == "Closed outer walls", "wall group");
            var leftInner = SideBounds(wallGroup, "Left wall").max.x;
            var rightInner = SideBounds(wallGroup, "Right wall").min.x;
            var backInner = SideBounds(wallGroup, "Back wall").min.z;
            var frontInner = SideBounds(wallGroup, "Front wall").max.z;

            Undo.SetCurrentGroupName("Align WC fixtures to room walls");
            var undoGroup = Undo.GetCurrentGroup();
            var sinks = RequireChild(root.transform, child => child.name == "Left wall sinks and mirrors", "sink group");
            foreach (Transform child in sinks)
                ShiftWorld(child, new Vector3(leftInner + clearance - RendererBounds(child).min.x, 0, 0), "Align sink or mirror to left wall");

            var urinals = RequireChild(root.transform, child => child.name == "Right wall urinals", "urinal group");
            foreach (Transform child in urinals)
                ShiftWorld(child, new Vector3(rightInner - clearance - RendererBounds(child).max.x, 0, 0), "Align urinal or divider to right wall");

            var storage = RequireChild(root.transform, child => child.name == "Storage and paper", "storage group");
            var cornerShelf = RequireChild(storage, child => child.name == "Back-left shelf", "back-left shelf");
            var shelfStart = cornerShelf.position;
            ShiftWorld(cornerShelf, new Vector3(leftInner + clearance - RendererBounds(cornerShelf).min.x, 0, 0), "Align corner shelf to left wall");
            ShiftWorld(cornerShelf, new Vector3(0, 0, backInner - clearance - RendererBounds(cornerShelf).max.z), "Align corner shelf to back wall");
            var shelfDelta = cornerShelf.position - shelfStart;
            foreach (Transform child in storage)
                if (child.name.StartsWith("Paper roll", StringComparison.Ordinal))
                    ShiftWorld(child, shelfDelta, "Move paper with corner shelf");

            var frontShelf = RequireChild(storage, child => child.name == "Front shelf near urinals", "front shelf");
            Undo.RecordObject(frontShelf, "Orient front shelf to front wall");
            frontShelf.SetPositionAndRotation(new Vector3(2.33f, frontShelf.position.y, frontShelf.position.z), Quaternion.Euler(0, 90, 0));
            ShiftWorld(frontShelf, new Vector3(0, 0, frontInner + clearance - RendererBounds(frontShelf).min.z), "Align front shelf to front wall");

            var leftAligned = sinks.Cast<Transform>().All(child => Mathf.Abs(RendererBounds(child).min.x - (leftInner + clearance)) < .002f);
            var rightAligned = urinals.Cast<Transform>().All(child => Mathf.Abs(RendererBounds(child).max.x - (rightInner - clearance)) < .002f);
            var cornerAligned = Mathf.Abs(RendererBounds(cornerShelf).min.x - (leftInner + clearance)) < .002f
                && Mathf.Abs(RendererBounds(cornerShelf).max.z - (backInner - clearance)) < .002f;
            var frontAligned = Mathf.Abs(RendererBounds(frontShelf).min.z - (frontInner + clearance)) < .002f;
            if (!leftAligned || !rightAligned || !cornerAligned || !frontAligned)
                throw new InvalidOperationException($"Fixture alignment verification failed: left={leftAligned}, right={rightAligned}, corner={cornerAligned}, front={frontAligned}.");

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save Playground.unity.");
            AssetDatabase.SaveAssets();
            Undo.CollapseUndoOperations(undoGroup);
            Selection.activeGameObject = root;
            SceneView.lastActiveSceneView?.FrameSelected();
            var firstUrinal = urinals.Cast<Transform>().First(child => child.name.StartsWith("Urinal ", StringComparison.Ordinal) && !child.name.StartsWith("Urinal divider", StringComparison.Ordinal));
            var firstDivider = urinals.Cast<Transform>().First(child => child.name.StartsWith("Urinal divider", StringComparison.Ordinal));
            Debug.Log($"WC_FIXTURE_ALIGN_OK|leftPlane={leftInner:F3}|rightPlane={rightInner:F3}|backPlane={backInner:F3}|frontPlane={frontInner:F3}|" +
                $"sinkX={sinks.GetChild(0).position.x:F3}|mirrorX={sinks.GetChild(1).position.x:F3}|urinalX={firstUrinal.position.x:F3}|" +
                $"dividerX={firstDivider.position.x:F3}|cornerShelf={cornerShelf.position.x:F3},{cornerShelf.position.z:F3}|frontShelf={frontShelf.position.x:F3},{frontShelf.position.z:F3}");
        }

        private static float MeasurePrefabHeight(string path)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) throw new InvalidOperationException($"Missing prefab: {path}");
            var preview = EditorSceneManager.NewPreviewScene();
            try
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, preview);
                var renderers = instance.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length == 0) throw new InvalidOperationException($"Prefab has no renderer: {path}");
                var bounds = renderers[0].bounds;
                foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
                return bounds.size.y;
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(preview);
            }
        }

        private static Bounds SideBounds(Transform wallGroup, string prefix)
        {
            var renderers = wallGroup.Cast<Transform>()
                .Where(child => child.gameObject.activeSelf && child.name.StartsWith(prefix, StringComparison.Ordinal))
                .SelectMany(child => child.GetComponentsInChildren<Renderer>(true))
                .ToArray();
            if (renderers.Length == 0) throw new InvalidOperationException($"No active renderers found for {prefix}.");
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            return bounds;
        }

        private static Bounds RendererBounds(Transform target)
        {
            var renderers = target.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) throw new InvalidOperationException($"{target.name} has no renderer.");
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            return bounds;
        }

        private static void ShiftWorld(Transform target, Vector3 delta, string undoName)
        {
            Undo.RecordObject(target, undoName);
            target.position += delta;
        }

        private static void ReuseChildren(Transform parent, IReadOnlyList<Vector3> positions, string prefix, float rotationY, Vector3 scale)
        {
            var children = parent.Cast<Transform>().OrderBy(child => child.GetSiblingIndex()).ToList();
            if (children.Count < positions.Count) throw new InvalidOperationException($"{parent.name} has only {children.Count} modules.");
            for (var index = 0; index < children.Count; index++)
            {
                var child = children[index];
                Undo.RecordObject(child.gameObject, "Reuse room module");
                Undo.RecordObject(child, "Resize room module");
                var active = index < positions.Count;
                child.gameObject.SetActive(active);
                if (!active) continue;
                child.name = $"{prefix} {index + 1}";
                child.SetLocalPositionAndRotation(positions[index], Quaternion.Euler(0, rotationY, 0));
                child.localScale = scale;
            }
        }

        private static void ReuseWallSide(Transform parent, string prefix, IEnumerable<Vector3> positions, float rotationY, float scaleY)
        {
            var modules = parent.Cast<Transform>().Where(child => child.name.StartsWith(prefix, StringComparison.Ordinal)).OrderBy(child => child.GetSiblingIndex()).ToList();
            var targets = positions.ToList();
            if (modules.Count < targets.Count) throw new InvalidOperationException($"{prefix} has only {modules.Count} modules.");
            for (var index = 0; index < modules.Count; index++)
            {
                var module = modules[index];
                Undo.RecordObject(module.gameObject, "Reuse outer wall module");
                Undo.RecordObject(module, "Resize outer wall module");
                var active = index < targets.Count;
                module.gameObject.SetActive(active);
                if (!active) continue;
                module.name = $"{prefix} {index + 1}";
                module.SetLocalPositionAndRotation(targets[index], Quaternion.Euler(0, rotationY, 0));
                module.localScale = new Vector3(1, scaleY, 1);
            }
        }

        private static void SetFixtureGroupX(Transform root, string groupName, Func<Transform, float> xForChild)
        {
            var group = RequireChild(root, child => child.name == groupName, groupName);
            foreach (Transform child in group)
            {
                Undo.RecordObject(child, "Move fixture with resized wall");
                var position = child.localPosition;
                position.x = xForChild(child);
                child.localPosition = position;
            }
        }

        private static void SetGroupX(Transform root, string groupName, IReadOnlyDictionary<string, float> xPositions)
        {
            var group = RequireChild(root, child => child.name == groupName, groupName);
            foreach (Transform child in group)
            {
                if (!xPositions.TryGetValue(child.name, out var x)) continue;
                Undo.RecordObject(child, "Center stalls in resized room");
                var position = child.localPosition;
                position.x = x;
                child.localPosition = position;
            }
        }

        private static Transform RequireChild(Transform parent, Func<Transform, bool> predicate, string description)
        {
            var result = parent.Cast<Transform>().SingleOrDefault(predicate);
            return result != null ? result : throw new InvalidOperationException($"Missing {description} under {parent.name}.");
        }
    }
}
