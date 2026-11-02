using System;
using System.IO;
using System.Linq;
using NatureCalls.Facility;
using NatureCalls.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;

namespace NatureCalls.Editor
{
    public static class StallDoorSetup
    {
        private const string StallPrefabPath = "Assets/Prefabs/Levels/WCStall.prefab";
        private const string ScenePath = "Assets/Scenes/Playground.unity";
        private const string ColliderPivotName = "Interactive door collider pivot";
        private const string ColliderGroupName = "Walkable stall colliders";
        private static StallDoor probeDoor;
        private static StallDoor[] probeDoors;
        private static Movement probePlayer;
        private static CharacterController probeController;
        private static int probeDoorIndex;
        private static int probeCycle;
        private static int probePhase;
        private static int probeStage;
        private static int probeFrames;

        [MenuItem("NatureCalls/Log WC Stall Door Candidates")]
        public static void LogDoorCandidates()
        {
            var contents = PrefabUtility.LoadPrefabContents(StallPrefabPath);
            try
            {
                var entries = contents.GetComponentsInChildren<Transform>(true)
                    .Select(item =>
                    {
                        var renderer = item.GetComponent<Renderer>();
                        var bounds = renderer != null ? $"|bounds={Format(renderer.bounds.size)}|center={Format(renderer.bounds.center)}" : string.Empty;
                        return $"{Path(contents.transform, item)}|local={Format(item.localPosition)}{bounds}";
                    });
                Debug.Log("STALL_DOOR_CANDIDATES\n" + string.Join("\n", entries));
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        [MenuItem("NatureCalls/Configure Interactive WC Stall Doors")]
        public static void Configure()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
            var contents = PrefabUtility.LoadPrefabContents(StallPrefabPath);
            try
            {
                var cubicle = contents.transform.Find("Cubicle");
                var doorLeaf = contents.transform.Find("Cubicle/door");
                var toilet = contents.transform.Find("Toilet");
                if (cubicle == null || doorLeaf == null || toilet == null)
                    throw new InvalidOperationException("WCStall.prefab is missing Cubicle, Cubicle/door, or Toilet.");
                var renderers = doorLeaf.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length == 0) throw new InvalidOperationException("WC stall door has no Renderer.");
                var wallRenderers = cubicle.GetComponentsInChildren<Renderer>(true)
                    .Where(renderer => renderer.transform != doorLeaf && !renderer.transform.IsChildOf(doorLeaf))
                    .ToArray();
                if (wallRenderers.Length == 0) throw new InvalidOperationException("WC stall cubicle has no wall Renderer.");

                var wallBounds = LocalRendererBounds(cubicle, wallRenderers);
                var doorBounds = LocalRendererBounds(cubicle, renderers);
                doorLeaf.localRotation = Quaternion.Euler(0, 180, 0);
                doorBounds = LocalRendererBounds(cubicle, renderers);
                var alignmentOffset = new Vector3(
                    wallBounds.center.x - doorBounds.center.x,
                    wallBounds.min.y - doorBounds.min.y,
                    wallBounds.max.z - doorBounds.max.z);
                doorLeaf.localPosition += alignmentOffset;
                toilet.localRotation = Quaternion.Euler(0, 180, 0);

                var oldPivot = contents.transform.Find(ColliderPivotName);
                if (oldPivot != null) UnityEngine.Object.DestroyImmediate(oldPivot.gameObject);
                var pivot = new GameObject(ColliderPivotName).transform;
                pivot.SetParent(contents.transform, false);
                pivot.SetPositionAndRotation(doorLeaf.position, doorLeaf.rotation);
                var bounds = LocalRendererBounds(pivot, renderers);
                var collider = pivot.gameObject.AddComponent<BoxCollider>();
                collider.center = bounds.center;
                collider.size = bounds.size;

                var stallDoor = contents.GetComponent<StallDoor>();
                if (stallDoor == null) stallDoor = contents.AddComponent<StallDoor>();
                stallDoor.DoorLeaf = doorLeaf;
                stallDoor.DoorColliderPivot = pivot;
                stallDoor.InteractionDistance = 1.5f;
                stallDoor.OpenAngle = 95f;
                EditorUtility.SetDirty(stallDoor);
                RebuildWalkableColliders(contents.transform, cubicle, doorLeaf, wallRenderers);

                if (PrefabUtility.SaveAsPrefabAsset(contents, StallPrefabPath) == null)
                    throw new IOException($"Could not save {StallPrefabPath}.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(StallPrefabPath, ImportAssetOptions.ForceUpdate);
            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath) throw new InvalidOperationException($"Open {ScenePath} first.");
            var stalls = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<StallDoor>(false))
                .OrderBy(stall => stall.name, StringComparer.Ordinal).ToArray();
            if (stalls.Length != 3) throw new InvalidOperationException($"Expected 3 active StallDoor prefab instances, found {stalls.Length}.");
            var prefabDoor = AssetDatabase.LoadAssetAtPath<GameObject>(StallPrefabPath)?.GetComponent<StallDoor>();
            if (prefabDoor == null) throw new InvalidOperationException("WCStall.prefab has no StallDoor component after import.");
            foreach (var stall in stalls)
            {
                CopyLocalPose(prefabDoor.DoorLeaf, stall.DoorLeaf, $"Reset {stall.name} door pose");
                AlignColliderPivotToDoor(stall);
            }
            var centerOffset = CenterSceneStalls(scene, stalls);
            var sample = stalls[0];
            var sampleCubicle = sample.transform.Find("Cubicle");
            var sampleToilet = sample.transform.Find("Toilet");
            var sampleColliderGroup = sampleCubicle != null ? sampleCubicle.Find(ColliderGroupName) : null;
            var wallCollidersFollowCubicle = sampleColliderGroup != null && sampleColliderGroup.GetComponentsInChildren<BoxCollider>(true).Length == 3;
            if (!wallCollidersFollowCubicle)
                throw new InvalidOperationException("WC stall wall colliders are not parented to the Cubicle local frame.");
            var sampleDoorRenderers = sample.DoorLeaf.GetComponentsInChildren<Renderer>(true);
            var sampleWallRenderers = sampleCubicle.GetComponentsInChildren<Renderer>(true)
                .Where(renderer => renderer.transform != sample.DoorLeaf && !renderer.transform.IsChildOf(sample.DoorLeaf))
                .ToArray();
            var sampleDoorBounds = LocalRendererBounds(sampleCubicle, sampleDoorRenderers);
            var sampleWallBounds = LocalRendererBounds(sampleCubicle, sampleWallRenderers);
            var doorAtFrame = Mathf.Abs(sampleDoorBounds.max.z - sampleWallBounds.max.z) < .02f;
            var doorInsetFromFramePlane = sampleDoorBounds.center.z < sampleWallBounds.max.z - .02f;
            var doorCentered = Mathf.Abs(sampleDoorBounds.center.x - sampleWallBounds.center.x) < .02f;
            var doorOnGround = Mathf.Abs(sampleDoorBounds.min.y - sampleWallBounds.min.y) < .02f;
            var toiletFacesEntrance = Vector3.Dot(sampleToilet.forward, -sample.transform.forward) > .99f;
            if (!doorAtFrame || !doorInsetFromFramePlane || !doorCentered || !doorOnGround || !toiletFacesEntrance)
                throw new InvalidOperationException(
                    $"WC stall alignment failed: doorAtFrame={doorAtFrame}, doorInsetFromFramePlane={doorInsetFromFramePlane}, " +
                    $"doorCentered={doorCentered}, " +
                    $"doorOnGround={doorOnGround}, toiletFacesEntrance={toiletFacesEntrance}.");
            var roomCenterX = RoomFloorBounds(scene).center.x;
            var stallCenterX = WorldRendererBounds(stalls.SelectMany(stall => stall.GetComponentsInChildren<Renderer>(true)).ToArray()).center.x;
            if (Mathf.Abs(stallCenterX - roomCenterX) > .01f)
                throw new InvalidOperationException($"WC stalls are not centered: stalls={stallCenterX:F3}, room={roomCenterX:F3}.");
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save Playground.unity after configuring stall doors.");
            Debug.Log($"STALL_DOOR_SETUP_OK|prefab={StallPrefabPath}|instances={stalls.Length}|input=E|initial=Closed|openAngle=95|" +
                $"doorAtFrame={doorAtFrame}|doorInsetFromFramePlane={doorInsetFromFramePlane}|" +
                $"doorCentered={doorCentered}|doorOnGround={doorOnGround}|" +
                $"toiletFacesEntrance={toiletFacesEntrance}|doorX={sampleDoorBounds.center.x:F3}|stallX={sampleWallBounds.center.x:F3}|" +
                $"doorBottom={sampleDoorBounds.min.y:F3}|ground={sampleWallBounds.min.y:F3}|" +
                $"doorFrameFaceZ={sampleDoorBounds.max.z:F3}|doorCenterZ={sampleDoorBounds.center.z:F3}|frameZ={sampleWallBounds.max.z:F3}|" +
                $"wallCollidersFollowCubicle={wallCollidersFollowCubicle}|" +
                $"centerOffsetX={centerOffset:F3}|stallCenterX={stallCenterX:F3}|roomCenterX={roomCenterX:F3}|" +
                $"positions={string.Join(",", stalls.Select(stall => stall.transform.position.x.ToString("F3")))}");
        }

        [MenuItem("NatureCalls/Run Stall Door Input Probe")]
        public static void RunInputProbe()
        {
            if (!EditorApplication.isPlaying) throw new InvalidOperationException("Enter Play Mode first.");
            var map = UnityEngine.Object.FindFirstObjectByType<NatureCalls.Map.Playground>();
            if (map == null || map.OfflinePlayer == null) throw new InvalidOperationException("Offline practice player is not active.");
            if (Keyboard.current == null) throw new InvalidOperationException("The Input System has no current keyboard.");
            probePlayer = map.OfflinePlayer;
            probeController = probePlayer.GetComponent<CharacterController>();
            if (probeController == null) throw new InvalidOperationException("Offline practice player has no CharacterController.");
            probeDoors = UnityEngine.Object.FindObjectsByType<StallDoor>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .OrderBy(door => door.name, StringComparer.Ordinal).ToArray();
            if (probeDoors.Length != 3) throw new InvalidOperationException($"Expected 3 active stall doors, found {probeDoors.Length}.");
            if (probeDoors.Select(door => door.DoorLeaf).Distinct().Count() != 3 ||
                probeDoors.Select(door => door.DoorColliderPivot).Distinct().Count() != 3)
                throw new InvalidOperationException("The three stalls do not have independent door leaves and collider pivots.");
            if (probeDoors.Any(door => door.State != StallDoorState.Closed))
                throw new InvalidOperationException("All stall doors must begin Closed.");

            probeDoorIndex = 0;
            probeCycle = 0;
            probePhase = 0;
            probeStage = 0;
            probeDoor = probeDoors[probeDoorIndex];
            MoveProbePlayer(false);
            QueueE();
        }

        private static void TickInputProbe()
        {
            probeFrames++;
            if (probeFrames == 2 && Keyboard.current != null)
                InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
            if (probeFrames < 8) return;

            var expected = probePhase == 0 ? StallDoorState.Open : StallDoorState.Closed;
            if (probeDoor == null || probeDoor.State != expected)
            {
                FinishProbe(false, expected);
                return;
            }

            if (probePhase == 0)
            {
                if (!MoveThroughOpenDoor())
                {
                    FinishProbe(false, StallDoorState.Open);
                    return;
                }
                MoveProbePlayer(false);
                probePhase = 1;
                probeStage++;
                QueueE();
                return;
            }

            if (!ClosedDoorBlocksPlayer())
            {
                FinishProbe(false, StallDoorState.Closed);
                return;
            }
            probeStage++;
            probeCycle++;
            if (probeCycle >= 2)
            {
                probeCycle = 0;
                probeDoorIndex++;
                if (probeDoorIndex >= probeDoors.Length)
                {
                    FinishProbe(true, StallDoorState.Closed);
                    return;
                }
                probeDoor = probeDoors[probeDoorIndex];
            }
            probePhase = 0;
            MoveProbePlayer(false);
            QueueE();
        }

        private static void QueueE()
        {
            EditorApplication.update -= TickInputProbe;
            probeFrames = 0;
            InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(Key.E));
            EditorApplication.update += TickInputProbe;
        }

        private static void MoveProbePlayer(bool inside)
        {
            var doorLocal = DoorwayCenterLocal(probeDoor);
            var targetLocal = new Vector3(doorLocal.x, 0, doorLocal.z + (inside ? .65f : -.65f));
            var target = probeDoor.transform.TransformPoint(targetLocal);
            probePlayer.SetControl(false);
            probePlayer.transform.position = new Vector3(target.x, probePlayer.transform.position.y, target.z);
            probePlayer.SetControl(true);
            Physics.SyncTransforms();
        }

        private static bool MoveThroughOpenDoor()
        {
            var doorLocal = DoorwayCenterLocal(probeDoor);
            for (var i = 0; i < 32; i++) probeController.Move(probeDoor.transform.forward * .05f);
            Physics.SyncTransforms();
            var playerLocal = probeDoor.transform.InverseTransformPoint(probePlayer.transform.position);
            return playerLocal.z > doorLocal.z + .35f;
        }

        private static bool ClosedDoorBlocksPlayer()
        {
            MoveProbePlayer(false);
            var doorLocal = DoorwayCenterLocal(probeDoor);
            for (var i = 0; i < 28; i++) probeController.Move(probeDoor.transform.forward * .05f);
            Physics.SyncTransforms();
            var playerLocal = probeDoor.transform.InverseTransformPoint(probePlayer.transform.position);
            return playerLocal.z < doorLocal.z + .10f;
        }

        private static Vector3 DoorwayCenterLocal(StallDoor door)
        {
            var cubicle = door.transform.Find("Cubicle");
            var renderers = cubicle.GetComponentsInChildren<Renderer>(true)
                .Where(renderer => renderer.transform != door.DoorLeaf && !renderer.transform.IsChildOf(door.DoorLeaf)).ToArray();
            var wallBounds = LocalRendererBounds(door.transform, renderers);
            var doorLocal = door.transform.InverseTransformPoint(door.DoorColliderPivot.position);
            return new Vector3(wallBounds.center.x, 0, doorLocal.z);
        }

        private static void FinishProbe(bool success, StallDoorState expected)
        {
            EditorApplication.update -= TickInputProbe;
            if (Keyboard.current != null) InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
            var actual = probeDoor != null ? probeDoor.State.ToString() : "Missing";
            Debug.Log($"STALL_DOOR_INPUT_PROBE_{(success ? "OK" : "FAILED")}|doors={probeDoors?.Length ?? 0}|cyclesPerDoor=2|" +
                $"completedTransitions={probeStage}|expected={expected}|actual={actual}|flow=outside-open,enter,close,closed-block");
            probeDoor = null;
            probeDoors = null;
            probePlayer = null;
            probeController = null;
            if (!success) throw new InvalidOperationException($"Stall door input probe expected {expected}, got {actual}.");
        }

        private static void CopyLocalPose(Transform source, Transform destination, string undoName)
        {
            if (source == null || destination == null) throw new InvalidOperationException($"Cannot copy door pose for {undoName}.");
            Undo.RecordObject(destination, undoName);
            destination.SetLocalPositionAndRotation(source.localPosition, source.localRotation);
            destination.localScale = source.localScale;
            EditorUtility.SetDirty(destination);
        }

        private static void AlignColliderPivotToDoor(StallDoor stall)
        {
            var pivot = stall.DoorColliderPivot;
            var renderers = stall.DoorLeaf.GetComponentsInChildren<Renderer>(true);
            if (pivot == null || renderers.Length == 0)
                throw new InvalidOperationException($"{stall.name} has no door collider pivot or door Renderer.");
            Undo.RecordObject(pivot, $"Align {stall.name} door collider pivot");
            pivot.SetPositionAndRotation(stall.DoorLeaf.position, stall.DoorLeaf.rotation);
            var collider = pivot.GetComponent<BoxCollider>();
            if (collider == null) throw new InvalidOperationException($"{stall.name} door collider pivot has no BoxCollider.");
            Undo.RecordObject(collider, $"Resize {stall.name} door collider");
            var bounds = LocalRendererBounds(pivot, renderers);
            collider.center = bounds.center;
            collider.size = bounds.size;
            EditorUtility.SetDirty(pivot);
            EditorUtility.SetDirty(collider);
        }

        private static void RebuildWalkableColliders(Transform stallRoot, Transform cubicle, Transform doorLeaf, Renderer[] wallRenderers)
        {
            var oldRootGroup = stallRoot.Find(ColliderGroupName);
            if (oldRootGroup != null) UnityEngine.Object.DestroyImmediate(oldRootGroup.gameObject);
            var oldCubicleGroup = cubicle.Find(ColliderGroupName);
            if (oldCubicleGroup != null) UnityEngine.Object.DestroyImmediate(oldCubicleGroup.gameObject);
            foreach (var collider in cubicle.GetComponents<BoxCollider>())
                UnityEngine.Object.DestroyImmediate(collider, true);

            var bounds = LocalRendererBounds(cubicle, wallRenderers);
            var group = new GameObject(ColliderGroupName).transform;
            group.SetParent(cubicle, false);
            var thickness = Mathf.Clamp(Mathf.Min(bounds.size.x, bounds.size.z) * .06f, .06f, .10f);
            CreateColliderBox(group, "Left wall collider",
                new Vector3(bounds.min.x + thickness * .5f, bounds.center.y, bounds.center.z),
                new Vector3(thickness, bounds.size.y, bounds.size.z));
            CreateColliderBox(group, "Right wall collider",
                new Vector3(bounds.max.x - thickness * .5f, bounds.center.y, bounds.center.z),
                new Vector3(thickness, bounds.size.y, bounds.size.z));
            CreateColliderBox(group, "Back wall collider",
                new Vector3(bounds.center.x, bounds.center.y, bounds.min.z + thickness * .5f),
                new Vector3(bounds.size.x, bounds.size.y, thickness));
        }

        private static void CreateColliderBox(Transform parent, string name, Vector3 center, Vector3 size)
        {
            var item = new GameObject(name);
            item.transform.SetParent(parent, false);
            var collider = item.AddComponent<BoxCollider>();
            collider.center = center;
            collider.size = size;
        }

        private static float CenterSceneStalls(Scene scene, StallDoor[] stalls)
        {
            var roomBounds = RoomFloorBounds(scene);
            var stallBounds = WorldRendererBounds(stalls.SelectMany(stall => stall.GetComponentsInChildren<Renderer>(true)).ToArray());
            var offset = roomBounds.center.x - stallBounds.center.x;
            foreach (var stall in stalls)
            {
                Undo.RecordObject(stall.transform, "Center WC stalls");
                stall.transform.position += Vector3.right * offset;
                EditorUtility.SetDirty(stall.transform);
            }
            return offset;
        }

        private static Bounds RoomFloorBounds(Scene scene)
        {
            var layout = scene.GetRootGameObjects().SingleOrDefault(root => root.name == "WC Layout v2");
            var floor = layout?.transform.Find("8m x 10m room shell/Floor - 2m square grid");
            if (floor == null) throw new InvalidOperationException("WC Layout v2 room floor group was not found.");
            var renderers = floor.GetComponentsInChildren<Renderer>(false);
            if (renderers.Length == 0) throw new InvalidOperationException("WC Layout v2 room floor has no active Renderer.");
            return WorldRendererBounds(renderers);
        }

        private static Bounds WorldRendererBounds(Renderer[] renderers)
        {
            if (renderers == null || renderers.Length == 0) throw new InvalidOperationException("Cannot calculate empty Renderer bounds.");
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            return bounds;
        }

        private static Bounds LocalRendererBounds(Transform root, Renderer[] renderers)
        {
            var first = root.InverseTransformPoint(renderers[0].bounds.center);
            var bounds = new Bounds(first, Vector3.zero);
            foreach (var renderer in renderers)
            {
                var value = renderer.bounds;
                for (var x = -1; x <= 1; x += 2)
                    for (var y = -1; y <= 1; y += 2)
                        for (var z = -1; z <= 1; z += 2)
                            bounds.Encapsulate(root.InverseTransformPoint(value.center + Vector3.Scale(value.extents, new Vector3(x, y, z))));
            }
            return bounds;
        }

        private static string Path(Transform root, Transform item)
        {
            if (item == root) return root.name;
            return Path(root, item.parent) + "/" + item.name;
        }

        private static string Format(Vector3 value) => $"({value.x:F3},{value.y:F3},{value.z:F3})";
    }
}
