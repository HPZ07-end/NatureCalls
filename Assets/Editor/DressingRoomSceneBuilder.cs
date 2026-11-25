using System;
using System.IO;
using System.Linq;
using NatureCalls.Camera;
using NatureCalls.Facility;
using NatureCalls.Map;
using NatureCalls.UI;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace NatureCalls.Editor
{
    /// <summary>Creates fresh scene contents, retaining the startup/return scene path and GUID.</summary>
    public static class DressingRoomSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/DressingRoom.unity";
        private const string Materials = "Assets/Materials/DressingRoom";
        private const string StoolPath = "Assets/Prefabs/Levels/DressingRoomStool.prefab";
        private static Material plaster, trim, wood, ceramic, mirrorMaterial;
        private static RenderTexture mirrorTexture;

        [MenuItem("NatureCalls/Rebuild Dressing Room")]
        public static void Build()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var player = Require<GameObject>(LobbyFlowBuilder.DressingRoomPlayerPath);
            var canvasPrefab = Require<GameObject>(LobbyFlowBuilder.CanvasPath);
            foreach (var model in new[] { "Facilities/Facilities-Sink", "Facilities/Facilities-SinkMirror", "Facilities/Facilities-SoapDispenser", "Fixtures/Fixtures-WallLight", "Structure/Structure-DressingDoor", "Structure/Structure-DressingDoorFrame", "Structure/Structure-FloorTile" })
                Require<GameObject>("Assets/Art/Models/" + model + ".fbx");
            var oldGuid = AssetDatabase.AssetPathToGUID(ScenePath);
            if (!AssetDatabase.IsValidFolder(Materials)) AssetDatabase.CreateFolder("Assets/Materials", "DressingRoom");
            plaster = Material("Plaster", new Color(.87f, .85f, .78f));
            trim = Material("Trim", new Color(.24f, .42f, .38f));
            wood = Material("Stool", new Color(.50f, .36f, .20f));
            ceramic = Material("Tiles", new Color(.74f, .80f, .74f));
            ConfigureMirrorMaterial();
            var stoolPrefab = BuildStool();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.52f, .55f, .50f);
            var room = new GameObject("DressingRoom");
            BuildShell(room.transform);
            var world = new GameObject("DressingRoomPractice").AddComponent<NatureCalls.Map.Playground>();
            world.PlayerPrefab = player; world.StartOffline = true; world.Camera = CameraRig();
            var spawn = Child(world.transform, "Player Spawn");
            spawn.position = new Vector3(-1.15f, .08f, -1.25f); world.Spawns = new[] { spawn };
            var canvas = (GameObject)PrefabUtility.InstantiatePrefab(canvasPrefab, scene);
            var lobby = canvas.GetComponent<LobbyController>();
            var interaction = world.gameObject.AddComponent<DressingRoomInteraction>(); interaction.World = world; interaction.Lobby = lobby;
            BuildWashStation(room.transform, lobby, world.Camera.Output, "A", -.95f);
            BuildWashStation(room.transform, lobby, world.Camera.Output, "B", .35f);
            var stool = (GameObject)PrefabUtility.InstantiatePrefab(stoolPrefab, scene);
            stool.transform.SetParent(room.transform, false); stool.transform.position = new Vector3(-1.9f, 0, .15f);
            BuildLobbyDoor(room.transform, lobby);
            Prop(room.transform, "Entrance Door", "Structure/Structure-DressingDoor", new Vector3(-1.62f, 0, -1.88f), 0);
            Prop(room.transform, "Entrance Frame", "Structure/Structure-DressingDoorFrame", new Vector3(-1.62f, 0, -1.88f), 0);
            Sign(room.transform, "入口", new Vector3(-1.15f, 2.42f, -1.87f), 180);
            AddHints(canvas.transform, world, lobby);
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new IOException("Could not save " + ScenePath);
            if (!string.IsNullOrEmpty(oldGuid) && oldGuid != AssetDatabase.AssetPathToGUID(ScenePath)) throw new InvalidOperationException("DressingRoom GUID changed unexpectedly.");
            var scenes = EditorBuildSettings.scenes.Where(value => value.path != ScenePath).ToList();
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true)); EditorBuildSettings.scenes = scenes.ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log("DRESSING_ROOM_REBUILT|ceiling=1|mirrors=2|stools=1|guid=" + AssetDatabase.AssetPathToGUID(ScenePath));
            ToiletLobbySetup.Build();
        }

        private static void BuildShell(Transform room)
        {
            Box(room, "Floor Collision", new Vector3(0, -.09f, 0), new Vector3(5, .18f, 4), ceramic);
            for (int x = 0; x < 5; x++) for (int z = 0; z < 4; z++)
                Prop(room, $"Floor Tile {x}-{z}", "Structure/Structure-FloorTile", new Vector3(x - 2.5f, .005f, z - 2), 0);
            Box(room, "Left Wall", new Vector3(-2.6f, 1.4f, 0), new Vector3(.2f, 2.8f, 4.4f), plaster);
            Box(room, "Right Wall", new Vector3(2.6f, 1.4f, 0), new Vector3(.2f, 2.8f, 4.4f), plaster);
            Box(room, "Front Wall", new Vector3(0, 1.4f, -2.1f), new Vector3(5.4f, 2.8f, .2f), plaster);
            Box(room, "Back Wall", new Vector3(0, 1.4f, 2.1f), new Vector3(5.4f, 2.8f, .2f), plaster);
            Box(room, "Ceiling", new Vector3(0, 2.9f, 0), new Vector3(5.4f, .2f, 4.4f), plaster);
            Box(room, "Wash Wall Tiles", new Vector3(2.48f, .70f, 0), new Vector3(.025f, 1.4f, 4), ceramic);
            Box(room, "Wash Wall Trim", new Vector3(2.455f, 1.42f, 0), new Vector3(.035f, .055f, 4), trim);
            for (int z = 0; z < 8; z++) Box(room, "Tile Joint " + z, new Vector3(2.46f, .70f, z * .5f - 1.75f), new Vector3(.008f, 1.38f, .006f), plaster, false);
            for (int y = 1; y < 4; y++) Box(room, "Horizontal Tile Joint " + y, new Vector3(2.46f, y * .35f, 0), new Vector3(.008f, .006f, 4), plaster, false);
            foreach (float z in new[] { -.8f, .9f })
            {
                Box(room, "Ceiling Light", new Vector3(0, 2.775f, z), new Vector3(.85f, .04f, .22f), ceramic, false);
                var lamp = new GameObject("Room Light").AddComponent<Light>(); lamp.transform.SetParent(room, false);
                lamp.transform.position = new Vector3(.2f, 2.45f, z); lamp.type = LightType.Point;
                lamp.range = 5; lamp.intensity = 2.4f; lamp.color = new Color(1, .94f, .82f); lamp.shadows = LightShadows.Soft;
            }
        }
        private static CameraController CameraRig()
        {
            var rig = new GameObject("Local Camera").AddComponent<CameraController>();
            rig.Output = Child(rig.transform, "Main Camera").gameObject.AddComponent<UnityEngine.Camera>(); rig.Output.tag = "MainCamera";
            rig.Output.nearClipPlane = .05f; rig.Output.farClipPlane = 50; rig.Output.fieldOfView = 65;
            rig.Output.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            rig.Output.gameObject.AddComponent<AudioListener>(); rig.Brain = rig.Output.gameObject.AddComponent<CinemachineBrain>();
            rig.Brain.UpdateMethod = CinemachineBrain.UpdateMethods.LateUpdate; rig.Brain.BlendUpdateMethod = CinemachineBrain.BrainUpdateMethods.LateUpdate;
            rig.Eye = Child(rig.transform, "Eye Target"); rig.Shoulder = Child(rig.transform, "Shoulder Target");
            rig.FirstPerson = Child(rig.transform, "First Person Rig").gameObject.AddComponent<CinemachineCamera>();
            rig.FirstPerson.Follow = rig.Eye; rig.FirstPerson.Lens.FieldOfView = 65; rig.FirstPerson.Lens.NearClipPlane = .05f;
            rig.FirstPerson.gameObject.AddComponent<CinemachineHardLockToTarget>(); rig.FirstPerson.gameObject.AddComponent<CinemachineRotateWithFollowTarget>();
            rig.ThirdPerson = Child(rig.transform, "Third Person Rig").gameObject.AddComponent<CinemachineCamera>();
            rig.ThirdPerson.Follow = rig.Shoulder; rig.ThirdPerson.Lens.FieldOfView = 65; rig.ThirdPerson.Lens.NearClipPlane = .05f;
            rig.ThirdPerson.gameObject.AddComponent<CinemachineThirdPersonFollow>(); return rig;
        }
        private static void BuildWashStation(Transform room, LobbyController lobby, UnityEngine.Camera viewer, string id, float z)
        {
            var station = Child(room, "Wash Station " + id);
            var sink = Prop(station, "Sink " + id, "Facilities/Facilities-Sink", new Vector3(2.43f, 0, z), 90);
            var obstruction = sink.AddComponent<BoxCollider>(); obstruction.center = new Vector3(0, .82f, -.21f); obstruction.size = new Vector3(.64f, .37f, .49f);
            var mirror = Prop(station, "Mirror " + id, "Facilities/Facilities-SinkMirror", new Vector3(2.43f, 1.18f, z), 90);
            var glass = mirror.GetComponentsInChildren<Renderer>().Single(renderer => renderer.sharedMaterials.Any(material => material != null && material.name.Contains("Mirror")));
            var bounds = glass.bounds;
            var normal = -mirror.transform.forward;
            var size = new Vector2(2 * Vector3.Dot(Abs(mirror.transform.right), bounds.extents), 2 * bounds.extents.y);
            float depth = Vector3.Dot(Abs(normal), bounds.extents);
            var plane = Child(mirror.transform, "Reflection Surface");
            plane.SetPositionAndRotation(bounds.center + normal * (depth + .001f), mirror.transform.rotation);
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(Materials + "/MirrorSurface.asset");
            if (mesh == null) { mesh = new Mesh { name = "Mirror Surface Full UV" }; AssetDatabase.CreateAsset(mesh, Materials + "/MirrorSurface.asset"); }
            mesh.Clear();
            mesh.vertices = new[] { new Vector3(-size.x/2, -size.y/2, 0), new Vector3(size.x/2, -size.y/2, 0), new Vector3(-size.x/2, size.y/2, 0), new Vector3(size.x/2, size.y/2, 0) };
            mesh.uv = new[] { new Vector2(1, 0), new Vector2(0, 0), new Vector2(1, 1), new Vector2(0, 1) };
            mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); EditorUtility.SetDirty(mesh);
            plane.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var surface = plane.gameObject.AddComponent<MeshRenderer>(); surface.sharedMaterial = mirrorMaterial;
            surface.shadowCastingMode = ShadowCastingMode.Off; surface.receiveShadows = false;
            glass.enabled = false; // Scene override only; preserve the imported FBX and frame.
            var camera = Child(mirror.transform, "Reflection Camera").gameObject.AddComponent<UnityEngine.Camera>();
            camera.transform.SetPositionAndRotation(new Vector3(2.34f, 1.48f, z), Quaternion.Euler(0, 270, 0));
            camera.targetTexture = mirrorTexture; camera.enabled = false; camera.depth = -2; camera.fieldOfView = 60; camera.nearClipPlane = .035f; camera.farClipPlane = 20;
            camera.cullingMask = ~(1 << 5); camera.allowHDR = false;
            var reflection = mirror.AddComponent<PlanarMirror>(); reflection.ReflectionCamera = camera; reflection.Surface = surface;
            reflection.ViewerCamera = viewer; reflection.Size = size;
            var target = mirror.AddComponent<BoxCollider>(); target.isTrigger = true; target.center = new Vector3(0, .4f, -.025f); target.size = new Vector3(.72f, .82f, .18f);
            var interaction = mirror.AddComponent<DressingMirror>(); interaction.InteractionDistance = 1.9f;
            interaction.StandingPoint = Child(station, "MirrorStandingPoint"); interaction.StandingPoint.position = new Vector3(1.28f, .02f, z);
            interaction.StandingPoint.rotation = Quaternion.Euler(0, 90, 0);
            UnityEventTools.AddPersistentListener(interaction.Interacted, lobby.OpenOutfitPanel);
            Prop(station, "Soap " + id, "Facilities/Facilities-SoapDispenser", new Vector3(2.42f, 1.06f, z + .43f), 90);
            Prop(station, "Mirror Light " + id, "Fixtures/Fixtures-WallLight", new Vector3(2.42f, 2.18f, z), 90);
        }
        private static void BuildLobbyDoor(Transform room, LobbyController lobby)
        {
            var door = Prop(room, "Lobby Door", "Structure/Structure-DressingDoor", new Vector3(2.40f, 0, 1.94f), 90);
            Prop(room, "Lobby Door Frame", "Structure/Structure-DressingDoorFrame", door.transform.position, 90);
            var target = door.AddComponent<BoxCollider>(); target.isTrigger = true; target.center = new Vector3(.45f, 1, 0); target.size = new Vector3(.95f, 2, .25f);
            var interaction = door.AddComponent<LobbyDoor>(); interaction.InteractionDistance = 1.85f;
            interaction.StandingPoint = Child(room, "DoorInteractionPoint");
            interaction.StandingPoint.position = new Vector3(1.25f, .02f, 1.48f);
            interaction.StandingPoint.rotation = Quaternion.Euler(0, 90, 0);
            UnityEventTools.AddPersistentListener(interaction.Interacted, lobby.OpenLobby);
            Sign(room, "大厅", new Vector3(2.36f, 2.43f, 1.48f), 90);
        }
        private static GameObject BuildStool()
        {
            var stool = new GameObject("DressingRoomStool");
            try
            {
                var collider = stool.AddComponent<BoxCollider>(); collider.center = new Vector3(0, .18f, 0); collider.size = new Vector3(.50f, .36f, .44f);
                stool.AddComponent<DressingRoomStool>(); Box(stool.transform, "Seat", new Vector3(0, .325f, 0), new Vector3(.50f, .07f, .44f), wood, false);
                foreach (float x in new[] { -.18f, .18f }) foreach (float z in new[] { -.15f, .15f })
                    Box(stool.transform, "Leg", new Vector3(x, .145f, z), new Vector3(.065f, .29f, .065f), wood, false);
                return PrefabUtility.SaveAsPrefabAsset(stool, StoolPath);
            }
            finally { Object.DestroyImmediate(stool); }
        }
        private static void AddHints(Transform parent, NatureCalls.Map.Playground world, LobbyController lobby)
        {
            var text = new GameObject("Dressing Room Interaction Hint", typeof(RectTransform), typeof(Text)).GetComponent<Text>(); text.transform.SetParent(parent, false);
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); text.fontSize = 24; text.alignment = TextAnchor.MiddleCenter; text.color = Color.white; text.raycastTarget = false;
            text.rectTransform.anchorMin = text.rectTransform.anchorMax = new Vector2(.5f, 0); text.rectTransform.pivot = new Vector2(.5f, 0);
            text.rectTransform.anchoredPosition = new Vector2(0, 24); text.rectTransform.sizeDelta = new Vector2(1100, 88);
            text.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(1, -1);
            var hints = text.gameObject.AddComponent<DressingRoomHints>(); hints.World = world; hints.Lobby = lobby; hints.Label = text;
        }
        private static void ConfigureMirrorMaterial()
        {
            var path = Materials + "/Mirror.renderTexture"; mirrorTexture = AssetDatabase.LoadAssetAtPath<RenderTexture>(path);
            if (mirrorTexture == null) { mirrorTexture = new RenderTexture(896, 1024, 24) { name = "DressingRoom Mirror" }; AssetDatabase.CreateAsset(mirrorTexture, path); }
            mirrorMaterial = AssetDatabase.LoadAssetAtPath<Material>(Materials + "/Mirror.mat");
            if (mirrorMaterial == null) { mirrorMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit")); AssetDatabase.CreateAsset(mirrorMaterial, Materials + "/Mirror.mat"); }
            mirrorMaterial.SetTexture("_BaseMap", mirrorTexture); mirrorMaterial.SetColor("_BaseColor", Color.white); EditorUtility.SetDirty(mirrorMaterial);
            mirrorMaterial.SetTextureScale("_BaseMap", Vector2.one); mirrorMaterial.SetTextureOffset("_BaseMap", Vector2.zero);
        }
        private static Material Material(string name, Color color)
        {
            var path = Materials + "/" + name + ".mat"; var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(material, path); }
            material.SetColor("_BaseColor", color); material.SetFloat("_Smoothness", .25f); EditorUtility.SetDirty(material); return material;
        }
        private static T Require<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>(path) ?? throw new FileNotFoundException("Required DressingRoom asset missing", path);
        private static Vector3 Abs(Vector3 value) => new Vector3(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));
        private static Transform Child(Transform parent, string name) { var child = new GameObject(name).transform; child.SetParent(parent, false); return child; }
        private static GameObject Prop(Transform parent, string name, string model, Vector3 position, float yaw)
        {
            var value = (GameObject)PrefabUtility.InstantiatePrefab(Require<GameObject>("Assets/Art/Models/" + model + ".fbx")); value.name = name;
            value.transform.SetParent(parent, false); value.transform.SetPositionAndRotation(position, Quaternion.Euler(0, yaw, 0)); return value;
        }
        private static GameObject Box(Transform parent, string name, Vector3 position, Vector3 size, Material material, bool collision = true)
        {
            var value = GameObject.CreatePrimitive(PrimitiveType.Cube); value.name = name; value.transform.SetParent(parent, false);
            value.transform.localPosition = position; value.transform.localScale = size; value.GetComponent<Renderer>().sharedMaterial = material;
            if (!collision) Object.DestroyImmediate(value.GetComponent<Collider>()); return value;
        }
        private static void Sign(Transform parent, string label, Vector3 position, float yaw)
        {
            var sign = Child(parent, label + " Sign").gameObject.AddComponent<TextMesh>(); sign.text = label; sign.fontSize = 64; sign.characterSize = .045f;
            sign.color = trim.color; sign.anchor = TextAnchor.MiddleCenter; sign.transform.SetPositionAndRotation(position, Quaternion.Euler(0, yaw, 0));
        }
    }
}
