using System;
using System.IO;
using System.Linq;
using NatureCalls.Camera;
using NatureCalls.Facility;
using NatureCalls.Map;
using NatureCalls.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;

namespace NatureCalls.Editor
{
    public static class PlaygroundDemoRepair
    {
        private const string ScenePath = "Assets/Scenes/Playground.unity";
        private const string StallPrefabPath = "Assets/Prefabs/Levels/WCStall.prefab";
        private const string ColliderGroupName = "Walkable stall colliders";
        private static Movement probeMovement;
        private static Bounds probeStallBounds;
        private static Vector3 probeStart;
        private static int probeFrames;
        private static bool probeMirrorVisible;

        [MenuItem("NatureCalls/Repair Brown Player Mirrors And Stalls")]
        public static void Repair()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath) throw new InvalidOperationException($"Open {ScenePath} first.");

            NolantPlayerSetup.Configure();
            RepairStallPrefab();

            var mirrorCameras = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<UnityEngine.Camera>(false))
                .Where(camera => camera.targetTexture != null)
                .ToArray();
            if (mirrorCameras.Length == 0) throw new InvalidOperationException("No active WC mirror cameras were found.");
            foreach (var mirrorCamera in mirrorCameras)
            {
                mirrorCamera.cullingMask |= 1 << CameraController.LocalBodyLayer;
                var listener = mirrorCamera.GetComponent<AudioListener>();
                if (listener != null) listener.enabled = false;
                var mirrorRoot = mirrorCamera.transform.parent != null && mirrorCamera.transform.parent.parent != null
                    ? mirrorCamera.transform.parent.parent.gameObject
                    : throw new InvalidOperationException($"Mirror camera {mirrorCamera.name} has an unexpected hierarchy.");
                var mirror = mirrorRoot.GetComponent<PlanarMirror>();
                if (mirror == null) mirror = Undo.AddComponent<PlanarMirror>(mirrorRoot);
                mirror.ReflectionCamera = mirrorCamera;
                mirror.Surface = mirrorRoot.GetComponentsInChildren<Renderer>(true).FirstOrDefault(renderer =>
                    renderer.sharedMaterials.Any(material => material != null &&
                        ((material.HasProperty("_BaseMap") && material.GetTexture("_BaseMap") == mirrorCamera.targetTexture) ||
                         (material.HasProperty("_MainTex") && material.GetTexture("_MainTex") == mirrorCamera.targetTexture))));
                if (mirror.Surface == null) throw new InvalidOperationException($"Mirror {mirrorRoot.name} has no render-texture surface.");
                EditorUtility.SetDirty(mirror);
                EditorUtility.SetDirty(mirrorCamera);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save repaired Playground.unity.");
            AssetDatabase.SaveAssets();
            Debug.Log($"BROWN_MIRROR_STALL_REPAIR_OK|mirrors={mirrorCameras.Length}|localBodyLayer={CameraController.LocalBodyLayer}|stallEntrance=open");
        }

        [MenuItem("NatureCalls/Run Brown Mirror And Stall Probe")]
        public static void RunProbe()
        {
            if (!EditorApplication.isPlaying) throw new InvalidOperationException("Enter Play Mode first.");
            var map = UnityEngine.Object.FindFirstObjectByType<NatureCalls.Map.Playground>();
            if (map == null || map.OfflinePlayer == null) throw new InvalidOperationException("Offline practice player is not active.");
            if (Keyboard.current == null) throw new InvalidOperationException("The Input System has no current keyboard.");

            probeMovement = map.OfflinePlayer;
            var brown = probeMovement.Body.Find("Peasant Nolant Brown visual");
            if (brown == null || !brown.gameObject.activeInHierarchy) throw new InvalidOperationException("The active player is not Peasant Nolant Brown.");
            var renderers = probeMovement.Body.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0 || renderers.Any(renderer => renderer.gameObject.layer != CameraController.LocalBodyLayer))
                throw new InvalidOperationException("The local Brown renderers are not on the mirror-visible local body layer.");
            if ((map.Camera.Output.cullingMask & (1 << CameraController.LocalBodyLayer)) != 0)
                throw new InvalidOperationException("The first-person output camera still renders the local body layer.");

            var mirrors = UnityEngine.Object.FindObjectsByType<PlanarMirror>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            if (mirrors.Length < 2 || mirrors.Any(mirror => mirror.ReflectionCamera == null || mirror.ReflectionCamera.targetTexture == null ||
                (mirror.ReflectionCamera.cullingMask & (1 << CameraController.LocalBodyLayer)) == 0))
                throw new InvalidOperationException("The active mirrors are not independently rendering the local Brown body.");

            var mirrorCamera = mirrors[0].ReflectionCamera;
            var mirrorPoint = mirrorCamera.transform.position + mirrorCamera.transform.forward * 1.2f;
            probeMovement.SetControl(false);
            probeMovement.transform.SetPositionAndRotation(
                new Vector3(mirrorPoint.x, probeMovement.transform.position.y, mirrorPoint.z),
                Quaternion.LookRotation(-mirrorCamera.transform.forward, Vector3.up));
            probeMovement.SetControl(true);
            Physics.SyncTransforms();
            var mirrorPlanes = GeometryUtility.CalculateFrustumPlanes(mirrorCamera);
            probeMirrorVisible = renderers.Any(renderer => GeometryUtility.TestPlanesAABB(mirrorPlanes, renderer.bounds));
            if (!probeMirrorVisible) throw new InvalidOperationException("Brown is outside the mirror camera frustum while standing in front of it.");

            var stall = GameObject.Find("WC Stall 1");
            var cubicle = stall != null ? stall.transform.Find("Cubicle") : null;
            if (cubicle == null) throw new InvalidOperationException("WC Stall 1/Cubicle was not found.");
            probeStallBounds = RendererBounds(cubicle);
            var controller = probeMovement.GetComponent<CharacterController>();
            probeStart = new Vector3(probeStallBounds.center.x, probeMovement.transform.position.y,
                probeStallBounds.min.z - controller.radius - .15f);
            probeMovement.SetControl(false);
            probeMovement.transform.SetPositionAndRotation(probeStart, Quaternion.identity);
            probeMovement.SetControl(true);

            EditorApplication.update -= TickProbe;
            probeFrames = 0;
            InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(Key.W));
            EditorApplication.update += TickProbe;
        }

        private static void TickProbe()
        {
            probeFrames++;
            if (probeFrames < 36) return;
            EditorApplication.update -= TickProbe;
            if (Keyboard.current != null) InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
            var end = probeMovement != null ? probeMovement.transform.position : probeStart;
            var entered = end.z > probeStallBounds.min.z + .2f &&
                end.x > probeStallBounds.min.x && end.x < probeStallBounds.max.x;
            Debug.Log($"BROWN_MIRROR_STALL_PROBE_{(entered ? "OK" : "FAILED")}|mirrors=2|mirrorVisible={probeMirrorVisible}|from={Format(probeStart)}|to={Format(end)}|stallMinZ={probeStallBounds.min.z:F3}");
            probeMovement = null;
            if (!entered) throw new InvalidOperationException("The Brown player did not enter WC Stall 1 through its front opening.");
        }

        private static void RepairStallPrefab()
        {
            var contents = PrefabUtility.LoadPrefabContents(StallPrefabPath);
            try
            {
                var cubicle = contents.transform.Find("Cubicle");
                if (cubicle == null) throw new InvalidOperationException("WCStall.prefab is missing Cubicle.");
                foreach (var collider in cubicle.GetComponents<BoxCollider>())
                    UnityEngine.Object.DestroyImmediate(collider, true);
                var oldGroup = contents.transform.Find(ColliderGroupName);
                if (oldGroup != null) UnityEngine.Object.DestroyImmediate(oldGroup.gameObject);

                var bounds = LocalRendererBounds(contents.transform, cubicle.GetComponentsInChildren<Renderer>(true));
                var group = new GameObject(ColliderGroupName).transform;
                group.SetParent(contents.transform, false);
                var thickness = Mathf.Clamp(Mathf.Min(bounds.size.x, bounds.size.z) * .06f, .06f, .10f);
                CreateBox(group, "Left wall collider",
                    new Vector3(bounds.min.x + thickness * .5f, bounds.center.y, bounds.center.z),
                    new Vector3(thickness, bounds.size.y, bounds.size.z));
                CreateBox(group, "Right wall collider",
                    new Vector3(bounds.max.x - thickness * .5f, bounds.center.y, bounds.center.z),
                    new Vector3(thickness, bounds.size.y, bounds.size.z));
                CreateBox(group, "Back wall collider",
                    new Vector3(bounds.center.x, bounds.center.y, bounds.max.z - thickness * .5f),
                    new Vector3(bounds.size.x, bounds.size.y, thickness));

                if (PrefabUtility.SaveAsPrefabAsset(contents, StallPrefabPath) == null)
                    throw new IOException($"Could not save {StallPrefabPath}.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        private static void CreateBox(Transform parent, string name, Vector3 center, Vector3 size)
        {
            var item = new GameObject(name);
            item.transform.SetParent(parent, false);
            var collider = item.AddComponent<BoxCollider>();
            collider.center = center;
            collider.size = size;
        }

        private static Bounds LocalRendererBounds(Transform root, Renderer[] renderers)
        {
            if (renderers.Length == 0) throw new InvalidOperationException("Cubicle has no renderers.");
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

        private static Bounds RendererBounds(Transform root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) throw new InvalidOperationException($"{root.name} has no renderers.");
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            return bounds;
        }

        private static string Format(Vector3 value) => $"({value.x:F3},{value.y:F3},{value.z:F3})";
    }
}
