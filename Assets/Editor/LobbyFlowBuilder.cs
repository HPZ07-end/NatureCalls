using System;
using System.IO;
using System.Linq;
using NatureCalls.Map;
using NatureCalls.Network;
using NatureCalls.Player;
using NatureCalls.UI;
using Unity.Netcode;
using Unity.Netcode.Components;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using Avatar = NatureCalls.Network.Avatar;
using PlayerInput = NatureCalls.Input.PlayerInput;

namespace NatureCalls.Editor
{
    /// <summary>
    /// Builds the DressingRoom → Lobby → map flow: outfit and map catalogs, the Lobby network prefabs,
    /// the Lobby canvas prefab, and the DressingRoom scene wiring. Safe to run again; existing catalogs keep their edits.
    /// </summary>
    public static class LobbyFlowBuilder
    {
        private const string PlayerPrefabPath = "Assets/Prefabs/Characters/Player.prefab";
        private const string LegacyVariantPath = "Assets/Prefabs/Characters/ChangingRoomPlayer.prefab";
        public const string DressingRoomPlayerPath = "Assets/Prefabs/Characters/DressingRoomPlayer.prefab";
        public const string OutfitCatalogPath = "Assets/Player/OutfitCatalog.asset";
        public const string MapCatalogPath = "Assets/Map/MapCatalog.asset";
        private const string NetworkFolder = "Assets/Prefabs/Network";
        public const string LobbyRoomPath = NetworkFolder + "/LobbyRoom.prefab";
        public const string NetworkPrefabPath = NetworkFolder + "/Network.prefab";
        private const string NetworkPrefabsListPath = "Assets/DefaultNetworkPrefabs.asset";
        public const string CanvasPath = "Assets/UI/Lobby/LobbyCanvas.prefab";
        public const string MatchHudPath = "Assets/UI/MatchHud.prefab";
        private const string BlurProfilePath = "Assets/UI/Lobby/LobbyBlur.asset";
        private const string DressingRoomPath = "Assets/Scenes/DressingRoom.unity";
        private const string PlaygroundPath = "Assets/Scenes/Playground.unity";
        private const string SmokePath = "Assets/Scenes/Smoke.unity";
        private static readonly string[] ObsoleteAssets =
        {
            "Assets/Scenes/ChangingRoom.unity", "Assets/UI/Lobby/LobbyUI.unity", "Assets/Facility/ChangingRoom"
        };
        private static readonly string[] ObsoleteSceneRoots = { "DressingRoomUIController", "DressingRoomUI", "RoomLobbyPlaceholder" };

        [MenuItem("NatureCalls/Build Lobby Flow")]
        public static void Build()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                throw new OperationCanceledException("Lobby flow build cancelled; scene changes were not saved.");
            var outfits = EnsureOutfitCatalog();
            AssignOutfitCatalog(outfits);
            var dressingPlayer = EnsureDressingRoomPlayer();
            var maps = EnsureMapCatalog();
            var lobbyRoom = BuildLobbyRoomPrefab(maps);
            var prefabsList = RegisterNetworkPrefab(lobbyRoom);
            UnregisterNetworkPrefab(prefabsList, dressingPlayer);
            var network = BuildNetworkPrefab(lobbyRoom, prefabsList);
            var canvas = BuildCanvasPrefab(maps, network);
            ConfigureDressingRoom(canvas, dressingPlayer);
            ConfigurePlayground();
            DeleteObsoleteAssets();
            ConfigureBuildScenes();
            AssetDatabase.SaveAssets();
            Debug.Log("NatureCalls lobby flow built.");
        }

        // ---- Catalogs and players ----

        private static OutfitCatalog EnsureOutfitCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<OutfitCatalog>(OutfitCatalogPath);
            if (catalog != null) return catalog;
            catalog = ScriptableObject.CreateInstance<OutfitCatalog>();
            catalog.SetOutfits(new[]
            {
                new OutfitCatalog.Outfit { Name = "经典蓝", Color = new Color(0.18f, 0.48f, 0.95f) },
                new OutfitCatalog.Outfit { Name = "清洁绿", Color = new Color(0.16f, 0.72f, 0.42f) },
                new OutfitCatalog.Outfit { Name = "维修橙", Color = new Color(1f, 0.45f, 0.1f) },
                new OutfitCatalog.Outfit { Name = "派对粉", Color = new Color(0.95f, 0.24f, 0.58f) },
            });
            AssetDatabase.CreateAsset(catalog, OutfitCatalogPath);
            return catalog;
        }

        private static void AssignOutfitCatalog(OutfitCatalog catalog)
        {
            var root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            try
            {
                SetReference(root.GetComponent<PlayerAppearance>(), "catalog", catalog);
                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static GameObject EnsureDressingRoomPlayer()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(DressingRoomPlayerPath) == null)
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(LegacyVariantPath) == null)
                    throw new FileNotFoundException("Neither DressingRoomPlayer nor the legacy ChangingRoomPlayer variant exists.", LegacyVariantPath);
                var error = AssetDatabase.MoveAsset(LegacyVariantPath, DressingRoomPlayerPath);
                if (!string.IsNullOrEmpty(error)) throw new InvalidOperationException(error);
            }
            var root = PrefabUtility.LoadPrefabContents(DressingRoomPlayerPath);
            try
            {
                // The DressingRoom player is local only: a NetworkObject left on it would be swept into the
                // host's spawn when a Lobby room starts.
                RemoveComponent<Avatar>(root);
                RemoveComponent<NetworkTransform>(root);
                RemoveComponent<NetworkObject>(root);
                var appearance = root.GetComponent<PlayerAppearance>();
                var outfit = GetOrAdd<PlayerOutfitController>(root);
                SetReference(outfit, "appearance", appearance);
                root.GetComponent<PlayerInput>().CaptureMouse = true;
                PrefabUtility.SaveAsPrefabAsset(root, DressingRoomPlayerPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            return AssetDatabase.LoadAssetAtPath<GameObject>(DressingRoomPlayerPath);
        }

        private static void RemoveComponent<T>(GameObject root) where T : Component
        {
            var component = root.GetComponent<T>();
            if (component != null) Object.DestroyImmediate(component);
        }

        private static void UnregisterNetworkPrefab(NetworkPrefabsList list, GameObject prefab)
        {
            foreach (var entry in list.PrefabList.Where(entry => entry.Prefab == prefab).ToArray())
            {
                list.Remove(entry);
                EditorUtility.SetDirty(list);
            }
        }

        private static MapCatalog EnsureMapCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<MapCatalog>(MapCatalogPath);
            if (catalog != null) return catalog;
            catalog = ScriptableObject.CreateInstance<MapCatalog>();
            catalog.SetMaps(new[] { new MapCatalog.Entry { DisplayName = "测试厕所（Playground）", ScenePath = PlaygroundPath } });
            AssetDatabase.CreateAsset(catalog, MapCatalogPath);
            return catalog;
        }

        // ---- Network prefabs ----

        private static GameObject BuildLobbyRoomPrefab(MapCatalog maps)
        {
            EnsureFolder(NetworkFolder);
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(LobbyRoomPath);
            var root = existing != null ? PrefabUtility.LoadPrefabContents(LobbyRoomPath) : new GameObject("LobbyRoom");
            try
            {
                if (root.GetComponent<NetworkObject>() == null) root.AddComponent<NetworkObject>();
                var room = GetOrAdd<LobbyRoom>(root);
                SetReference(room, "maps", maps);
                PrefabUtility.SaveAsPrefabAsset(root, LobbyRoomPath);
            }
            finally
            {
                if (existing != null) PrefabUtility.UnloadPrefabContents(root);
                else Object.DestroyImmediate(root);
            }
            return AssetDatabase.LoadAssetAtPath<GameObject>(LobbyRoomPath);
        }

        private static NetworkPrefabsList RegisterNetworkPrefab(GameObject prefab)
        {
            var list = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(NetworkPrefabsListPath);
            if (list == null) throw new FileNotFoundException("Network prefab list is missing.", NetworkPrefabsListPath);
            if (!list.Contains(prefab))
            {
                list.Add(new NetworkPrefab { Prefab = prefab });
                EditorUtility.SetDirty(list);
            }
            return list;
        }

        private static GameObject BuildNetworkPrefab(GameObject lobbyRoom, NetworkPrefabsList prefabsList)
        {
            var network = new GameObject("Network");
            try
            {
                var manager = network.AddComponent<NetworkManager>();
                var transport = network.AddComponent<UnityTransport>();
                manager.NetworkConfig = new NetworkConfig
                {
                    PlayerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath), NetworkTransport = transport,
                    ConnectionApproval = true, EnableSceneManagement = false,
                    TickRate = 30, ProtocolVersion = 1
                };
                manager.NetworkConfig.Prefabs.NetworkPrefabsLists.Clear();
                manager.NetworkConfig.Prefabs.NetworkPrefabsLists.Add(prefabsList);
                var session = network.AddComponent<Session>();
                session.Manager = manager;
                session.Transport = transport;
                session.LobbyRoomPrefab = lobbyRoom;
                PrefabUtility.SaveAsPrefabAsset(network, NetworkPrefabPath);
            }
            finally { Object.DestroyImmediate(network); }
            return AssetDatabase.LoadAssetAtPath<GameObject>(NetworkPrefabPath);
        }

        // ---- Lobby canvas prefab ----

        private static GameObject BuildCanvasPrefab(MapCatalog maps, GameObject network)
        {
            // An existing, fully wired canvas may have been restyled by art: keep it and only refresh its references.
            var saved = AssetDatabase.LoadAssetAtPath<GameObject>(CanvasPath);
            if (saved != null && IsComplete(saved.GetComponent<LobbyView>()))
            {
                var existing = PrefabUtility.LoadPrefabContents(CanvasPath);
                try
                {
                    var current = existing.GetComponent<LobbyController>();
                    SetReference(current, "maps", maps);
                    SetReference(current, "networkPrefab", network);
                    PrefabUtility.SaveAsPrefabAsset(existing, CanvasPath);
                }
                finally { PrefabUtility.UnloadPrefabContents(existing); }
                return AssetDatabase.LoadAssetAtPath<GameObject>(CanvasPath);
            }
            var root = new GameObject("LobbyCanvas", typeof(RectTransform));
            try
            {
                var canvas = root.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 10;
                var scaler = root.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.matchWidthOrHeight = .5f;
                root.AddComponent<GraphicRaycaster>();
                var view = root.AddComponent<LobbyView>();

                var blur = root.AddComponent<BackgroundBlur>();
                var volumeObject = new GameObject("Blur Volume");
                volumeObject.transform.SetParent(root.transform, false);
                var volume = volumeObject.AddComponent<Volume>();
                volume.isGlobal = true;
                volume.priority = 10;
                volume.weight = 0;
                volume.sharedProfile = EnsureBlurProfile();
                var dim = Stretch(MakeImage("Dim", root.transform, new Color(0.04f, 0.05f, 0.08f, .55f)).gameObject);
                SetReference(blur, "volume", volume);
                SetReference(blur, "dim", dim);
                view.Blur = blur;

                view.MirrorPrompt = Prompt(root.transform, "MirrorPrompt", "按 E 照镜子换装");
                view.DoorPrompt = Prompt(root.transform, "DoorPrompt", "按 E 进入联机大厅");

                // Outfit panel: lower right, so the mirror stays visible.
                var outfit = Box(root.transform, "OutfitPanel", new Vector2(1, 0), new Vector2(1, 0), new Vector2(-40, 40), new Vector2(520, 560));
                view.OutfitPanel = outfit.gameObject;
                MakeLabel(outfit, "Title", "换装", 40, TextAnchor.UpperCenter, new Vector2(0, -24), new Vector2(480, 56));
                view.OutfitOptions = Column(outfit, "OutfitOptions", new Vector2(0, -96), new Vector2(440, 300));
                view.OutfitOptionTemplate = MakeButton(view.OutfitOptions, "OutfitOption", "外观");
                view.OutfitCurrent = MakeLabel(outfit, "OutfitCurrent", "当前预览：", 26, TextAnchor.MiddleCenter, new Vector2(0, -410), new Vector2(480, 40));
                view.OutfitConfirm = MakeButton(outfit, "OutfitConfirm", "确认", new Vector2(-110, 40), new Vector2(200, 64), bottom: true);
                view.OutfitCancel = MakeButton(outfit, "OutfitCancel", "取消", new Vector2(110, 40), new Vector2(200, 64), bottom: true);

                // Lobby panel: centered over the blurred room.
                var lobby = Box(root.transform, "LobbyPanel", new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(900, 820));
                view.LobbyPanel = lobby.gameObject;
                MakeLabel(lobby, "Title", "联机大厅", 48, TextAnchor.UpperCenter, new Vector2(0, -28), new Vector2(860, 64));
                var entry = Column(lobby, "EntryGroup", new Vector2(0, -120), new Vector2(560, 520));
                view.EntryGroup = entry.gameObject;
                view.OfflineButton = MakeButton(entry, "Offline", "单人离线测试");
                view.CreateButton = MakeButton(entry, "CreateRoom", "创建房间");
                view.CodeInput = MakeInput(entry, "CodeInput", "房间号 / 房主局域网地址");
                view.JoinButton = MakeButton(entry, "JoinRoom", "加入房间");
                view.CloseButton = MakeButton(entry, "Close", "返回准备房");
                var room = Column(lobby, "RoomGroup", new Vector2(0, -120), new Vector2(760, 560));
                view.RoomGroup = room.gameObject;
                view.RoomCode = MakeLabel(room, "RoomCode", "房间号：", 30, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(760, 48));
                Height(view.RoomCode.gameObject, 48);
                view.Slots = new Text[4];
                for (int slot = 0; slot < 4; slot++)
                {
                    var box = MakeImage("Slot" + (slot + 1), room, new Color(1, 1, 1, .08f));
                    Height(box.gameObject, 84);
                    view.Slots[slot] = Stretch(MakeLabel(box.transform, "Label", "空位", 26, TextAnchor.MiddleCenter, Vector2.zero, Vector2.zero).gameObject).GetComponent<Text>();
                }
                view.PrimaryButton = MakeButton(room, "Primary", "准备");
                view.PrimaryLabel = view.PrimaryButton.GetComponentInChildren<Text>();
                view.LeaveButton = MakeButton(room, "Leave", "离开房间");
                view.Status = MakeLabel(lobby, "Status", "", 24, TextAnchor.MiddleCenter, new Vector2(0, 36), new Vector2(860, 80), bottom: true);

                // Map panel: shown after the game starts, above the lobby panel.
                var map = Box(root.transform, "MapPanel", new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(760, 680));
                view.MapPanel = map.gameObject;
                MakeLabel(map, "Title", "选择地图", 44, TextAnchor.UpperCenter, new Vector2(0, -28), new Vector2(720, 60));
                view.MapHint = MakeLabel(map, "MapHint", "选择本局地图", 26, TextAnchor.MiddleCenter, new Vector2(0, -100), new Vector2(720, 40));
                view.MapOptions = Column(map, "MapOptions", new Vector2(0, -160), new Vector2(520, 360));
                view.MapOptionTemplate = MakeButton(view.MapOptions, "MapOption", "地图");
                view.MapBack = MakeButton(map, "MapBack", "返回", new Vector2(0, 40), new Vector2(240, 64), bottom: true);

                var controller = root.AddComponent<LobbyController>();
                SetReference(controller, "maps", maps);
                SetReference(controller, "networkPrefab", network);
                PrefabUtility.SaveAsPrefabAsset(root, CanvasPath);
            }
            finally { Object.DestroyImmediate(root); }
            return AssetDatabase.LoadAssetAtPath<GameObject>(CanvasPath);
        }

        private static bool IsComplete(LobbyView view)
        {
            if (view == null) return false;
            var serialized = new SerializedObject(view);
            var property = serialized.GetIterator();
            for (bool enter = true; property.NextVisible(enter); enter = false)
            {
                if (property.propertyType == SerializedPropertyType.ObjectReference && property.name != "m_Script" && property.objectReferenceValue == null)
                    return false;
                if (property.isArray && property.propertyType == SerializedPropertyType.Generic)
                    for (int index = 0; index < property.arraySize; index++)
                        if (property.GetArrayElementAtIndex(index).objectReferenceValue == null) return false;
            }
            return true;
        }

        private static VolumeProfile EnsureBlurProfile()
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(BlurProfilePath);
            if (profile != null) return profile;
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, BlurProfilePath);
            var depth = profile.Add<DepthOfField>(true);
            depth.name = nameof(DepthOfField);
            depth.mode.Override(DepthOfFieldMode.Gaussian);
            depth.gaussianStart.Override(0f);
            depth.gaussianEnd.Override(1f);
            depth.gaussianMaxRadius.Override(1.5f);
            depth.highQualitySampling.Override(true);
            AssetDatabase.AddObjectToAsset(depth, profile);
            EditorUtility.SetDirty(profile);
            return profile;
        }

        // ---- DressingRoom scene ----

        private static void ConfigureDressingRoom(GameObject canvasPrefab, GameObject dressingPlayer)
        {
            var scene = EditorSceneManager.OpenScene(DressingRoomPath, OpenSceneMode.Single);
            GameObject canvas = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                // Keep an existing instance of the canvas prefab so reruns do not churn the scene.
                if (canvas == null && PrefabUtility.GetCorrespondingObjectFromSource(root) == canvasPrefab)
                {
                    canvas = root;
                    continue;
                }
                if (ObsoleteSceneRoots.Contains(root.name) || root.GetComponent<LobbyController>() != null)
                {
                    Object.DestroyImmediate(root);
                    continue;
                }
                foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                    GameObjectUtility.RemoveMonoBehavioursWithMissingScript(transform.gameObject);
            }

            if (canvas == null) canvas = (GameObject)PrefabUtility.InstantiatePrefab(canvasPrefab, scene);
            var controller = canvas.GetComponent<LobbyController>();

            var mirror = Object.FindFirstObjectByType<DressingMirror>(FindObjectsInactive.Include);
            var door = Object.FindFirstObjectByType<LobbyDoor>(FindObjectsInactive.Include);
            if (mirror == null || door == null) throw new InvalidOperationException("DressingRoom needs its mirror and door interaction zones.");
            Rewire(mirror.PlayerEntered, controller.ShowMirrorPrompt);
            Rewire(mirror.PlayerExited, controller.HideMirrorPrompt);
            Rewire(mirror.Interacted, controller.OpenOutfitPanel);
            Rewire(door.PlayerEntered, controller.ShowDoorPrompt);
            Rewire(door.PlayerExited, controller.HideDoorPrompt);
            Rewire(door.Interacted, controller.OpenLobby);
            EditorUtility.SetDirty(mirror);
            EditorUtility.SetDirty(door);

            var world = Object.FindFirstObjectByType<NatureCalls.Map.Playground>(FindObjectsInactive.Include);
            if (world == null) throw new InvalidOperationException("DressingRoom needs its local practice Playground.");
            world.PlayerPrefab = dressingPlayer;
            world.StartOffline = true;
            EditorUtility.SetDirty(world);
            var cameraData = world.Camera.Output.GetUniversalAdditionalCameraData();
            cameraData.renderPostProcessing = true;
            EditorUtility.SetDirty(cameraData);

            if (Object.FindFirstObjectByType<EventSystem>(FindObjectsInactive.Include) == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save " + DressingRoomPath);
        }

        // ---- Map HUD ----

        private static GameObject BuildMatchHudPrefab()
        {
            var saved = AssetDatabase.LoadAssetAtPath<GameObject>(MatchHudPath);
            if (saved != null && saved.GetComponent<MatchExit>() != null)
            {
                var existing = PrefabUtility.LoadPrefabContents(MatchHudPath);
                try
                {
                    EnsureStatusPlaceholder(existing.transform);
                    EnsureMatchClock(existing);
                    PrefabUtility.SaveAsPrefabAsset(existing, MatchHudPath);
                }
                finally { PrefabUtility.UnloadPrefabContents(existing); }
                return AssetDatabase.LoadAssetAtPath<GameObject>(MatchHudPath);
            }
            var root = new GameObject("MatchHud", typeof(RectTransform));
            try
            {
                var canvas = root.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 5;
                var scaler = root.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.matchWidthOrHeight = .5f;
                root.AddComponent<GraphicRaycaster>();
                var button = MakeButton(root.transform, "Exit", "退出", Vector2.zero, new Vector2(180, 60), bottom: false);
                var rect = (RectTransform)button.transform;
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
                rect.anchoredPosition = new Vector2(24, -24);
                SetReference(root.AddComponent<MatchExit>(), "button", button);
                EnsureStatusPlaceholder(root.transform);
                EnsureMatchClock(root);
                PrefabUtility.SaveAsPrefabAsset(root, MatchHudPath);
            }
            finally { Object.DestroyImmediate(root); }
            return AssetDatabase.LoadAssetAtPath<GameObject>(MatchHudPath);
        }

        [MenuItem("NatureCalls/Configure Match HUD Status Placeholder")]
        public static void ConfigureMatchHudStatusPlaceholder()
        {
            if (Application.isPlaying)
                throw new InvalidOperationException("Exit Play Mode before configuring the Match HUD.");
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            BuildMatchHudPrefab();
            ConfigurePlayground();
            AssetDatabase.SaveAssets();
            Debug.Log("NatureCalls Match HUD status placeholder configured.");
        }

        private static void EnsureStatusPlaceholder(Transform parent)
        {
            if (parent.Find("PlayerStatusPanel") != null)
                return;

            var panel = Box(parent, "PlayerStatusPanel", new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, -100), new Vector2(330, 144));
            MakeStatusBar(panel, "Urgency", "急迫值", .65f, new Color(.95f, .45f, .10f), -18f);
            MakeStatusBar(panel, "Disgust", "恶心值", .20f, new Color(.35f, .70f, .30f), -80f);
        }

        // Top-centre round timer.
        private static void EnsureMatchClock(GameObject hud)
        {
            if (hud.GetComponent<MatchClock>() != null) return;
            var label = MakeLabel(hud.transform, "MatchClock", "剩余时间  03:00", 40, TextAnchor.MiddleCenter, new Vector2(0, -24), new Vector2(420, 64));
            var outline = label.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0, 0, 0, .8f);
            SetReference(hud.AddComponent<MatchClock>(), "label", label);
        }

        /// <summary>Adds the round timer to the map HUD and stops the DressingRoom floor from flickering.</summary>
        [MenuItem("NatureCalls/Configure Match Clock")]
        public static void ConfigureMatchClock()
        {
            BuildMatchHudPrefab();
            HideCoplanarDressingRoomPlane();
            AssetDatabase.SaveAssets();
        }

        // The DressingRoom's default-material Plane lies exactly on the Floor's top face (y = 0), so the two
        // surfaces z-fight. Its renderer is turned off; its collider stays.
        private static void HideCoplanarDressingRoomPlane()
        {
            var scene = EditorSceneManager.OpenScene(DressingRoomPath, OpenSceneMode.Single);
            var floor = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true)).FirstOrDefault(item => item.name == "Floor");
            var plane = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true)).FirstOrDefault(item => item.name == "Plane");
            if (floor == null || plane == null || !plane.TryGetComponent(out MeshRenderer renderer) || !floor.TryGetComponent(out Renderer floorRenderer)) return;
            if (Mathf.Abs(floorRenderer.bounds.max.y - renderer.bounds.max.y) > .001f || !renderer.enabled) return;
            renderer.enabled = false;
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save " + DressingRoomPath);
            Debug.Log("DRESSING_ROOM_FLOOR_PLANE_HIDDEN|floorTop=" + floorRenderer.bounds.max.y.ToString("F3"));
        }

        private static void MakeStatusBar(Transform parent, string name, string label, float value, Color color, float y)
        {
            MakeLabel(parent, name + "Label", label + "  " + Mathf.RoundToInt(value * 100f) + "%", 24,
                TextAnchor.MiddleLeft, new Vector2(-8, y), new Vector2(286, 28));

            var track = MakeImage(name + "Track", parent, new Color(.02f, .03f, .05f, .72f));
            var trackRect = track.rectTransform;
            trackRect.anchorMin = trackRect.anchorMax = new Vector2(.5f, 1f);
            trackRect.pivot = new Vector2(.5f, 1f);
            trackRect.anchoredPosition = new Vector2(0, y - 30f);
            trackRect.sizeDelta = new Vector2(286, 18);

            var fill = MakeImage(name + "Fill", track.transform, color);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = 0;
            fill.fillAmount = value;
            Stretch(fill.gameObject);
        }

        /// <summary>Adds the exit HUD and an EventSystem to the open map scene, replacing the old IMGUI menu.</summary>
        public static void AddMatchHud()
        {
            var hud = BuildMatchHudPrefab();
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            bool present = false;
            foreach (var root in scene.GetRootGameObjects())
            {
                if (!present && PrefabUtility.GetCorrespondingObjectFromSource(root) == hud) { present = true; continue; }
                if (root.name == "Controls" || root.GetComponent<MatchExit>() != null) Object.DestroyImmediate(root);
            }
            if (!present) PrefabUtility.InstantiatePrefab(hud, scene);
            if (Object.FindFirstObjectByType<EventSystem>(FindObjectsInactive.Include) == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }

        private static void ConfigurePlayground()
        {
            var scene = EditorSceneManager.OpenScene(PlaygroundPath, OpenSceneMode.Single);
            AddMatchHud();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save " + PlaygroundPath);
        }

        private static void Rewire(UnityEvent unityEvent, UnityAction action)
        {
            for (int index = unityEvent.GetPersistentEventCount() - 1; index >= 0; index--)
                UnityEventTools.RemovePersistentListener(unityEvent, index);
            UnityEventTools.AddPersistentListener(unityEvent, action);
        }

        private static void DeleteObsoleteAssets()
        {
            foreach (var path in ObsoleteAssets)
                if ((AssetDatabase.LoadMainAssetAtPath(path) != null || AssetDatabase.IsValidFolder(path)) && !AssetDatabase.DeleteAsset(path))
                    throw new IOException("Could not delete " + path);
        }

        private static void ConfigureBuildScenes()
        {
            EditorBuildSettings.scenes = new[] { DressingRoomPath, PlaygroundPath, SmokePath }
                .Select(path => new EditorBuildSettingsScene(path, true)).ToArray();
        }

        // ---- uGUI helpers ----

        private static readonly Font UiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        private static Sprite BuiltinSprite(string name) => AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/" + name);

        private static RectTransform MakeRect(string name, Transform parent)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        private static GameObject Stretch(GameObject target)
        {
            var rect = target.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return target;
        }

        private static Image MakeImage(string name, Transform parent, Color color)
        {
            var image = MakeRect(name, parent).gameObject.AddComponent<Image>();
            image.sprite = BuiltinSprite("UISprite.psd");
            image.type = UnityEngine.UI.Image.Type.Sliced;
            image.color = color;
            return image;
        }

        private static RectTransform Box(Transform parent, string name, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            var rect = MakeImage(name, parent, new Color(0.1f, 0.12f, 0.16f, .94f)).rectTransform;
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        private static Text MakeLabel(Transform parent, string name, string value, int size, TextAnchor alignment, Vector2 position, Vector2 dimensions, bool bottom = false)
        {
            var rect = MakeRect(name, parent);
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, bottom ? 0 : 1);
            rect.pivot = new Vector2(.5f, bottom ? 0 : 1);
            rect.anchoredPosition = position;
            rect.sizeDelta = dimensions;
            var text = rect.gameObject.AddComponent<Text>();
            text.font = UiFont;
            text.fontSize = size;
            text.alignment = alignment;
            text.color = Color.white;
            text.text = value;
            text.raycastTarget = false;
            return text;
        }

        private static GameObject Prompt(Transform parent, string name, string value)
        {
            var label = MakeLabel(parent, name, value, 34, TextAnchor.MiddleCenter, new Vector2(0, 120), new Vector2(900, 64), bottom: true);
            var outline = label.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0, 0, 0, .8f);
            return label.gameObject;
        }

        private static RectTransform Column(Transform parent, string name, Vector2 position, Vector2 size)
        {
            var rect = MakeRect(name, parent);
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, 1);
            rect.pivot = new Vector2(.5f, 1);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var layout = rect.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 14;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            return rect;
        }

        private static void Height(GameObject target, float height) =>
            GetOrAdd<LayoutElement>(target).preferredHeight = height;

        private static Button MakeButton(Transform parent, string name, string label) =>
            MakeButton(parent, name, label, Vector2.zero, new Vector2(400, 64), bottom: false, inLayout: true);

        private static Button MakeButton(Transform parent, string name, string label, Vector2 position, Vector2 size, bool bottom, bool inLayout = false)
        {
            var image = MakeImage(name, parent, new Color(0.26f, 0.42f, 0.78f));
            var rect = image.rectTransform;
            if (inLayout) Height(image.gameObject, 64);
            else
            {
                rect.anchorMin = rect.anchorMax = new Vector2(.5f, bottom ? 0 : 1);
                rect.pivot = new Vector2(.5f, bottom ? 0 : 1);
                rect.anchoredPosition = position;
                rect.sizeDelta = size;
            }
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            Stretch(MakeLabel(rect, "Label", label, 28, TextAnchor.MiddleCenter, Vector2.zero, Vector2.zero).gameObject);
            return button;
        }

        private static InputField MakeInput(Transform parent, string name, string placeholder)
        {
            var image = MakeImage(name, parent, Color.white);
            image.sprite = BuiltinSprite("InputFieldBackground.psd");
            Height(image.gameObject, 64);
            var field = image.gameObject.AddComponent<InputField>();
            var text = Stretch(MakeLabel(image.transform, "Text", "", 28, TextAnchor.MiddleLeft, Vector2.zero, Vector2.zero).gameObject).GetComponent<Text>();
            text.color = new Color(0.1f, 0.1f, 0.12f);
            text.supportRichText = false;
            text.rectTransform.offsetMin = new Vector2(16, 0);
            var hint = Stretch(MakeLabel(image.transform, "Placeholder", placeholder, 26, TextAnchor.MiddleLeft, Vector2.zero, Vector2.zero).gameObject).GetComponent<Text>();
            hint.color = new Color(0.1f, 0.1f, 0.12f, .5f);
            hint.fontStyle = FontStyle.Italic;
            hint.rectTransform.offsetMin = new Vector2(16, 0);
            field.textComponent = text;
            field.placeholder = hint;
            field.characterLimit = 64;
            return field;
        }

        private static void SetReference(Object target, string property, Object value)
        {
            var serialized = new SerializedObject(target);
            var found = serialized.FindProperty(property) ?? throw new InvalidOperationException($"{target.GetType().Name}.{property} not found.");
            found.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        private static T GetOrAdd<T>(Component owner) where T : Component => GetOrAdd<T>(owner.gameObject);
        private static T GetOrAdd<T>(GameObject owner) where T : Component
        {
            // GetComponent can return Unity's fake null in the Editor, which ?? does not treat as null.
            var found = owner.GetComponent<T>();
            return found != null ? found : owner.AddComponent<T>();
        }
    }
}
