using System;
using System.Linq;
using NatureCalls.Map;
using NatureCalls.UI;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace NatureCalls.Editor
{
    public static class ToiletLobbySetup
    {
        [MenuItem("NatureCalls/Build Toilet Lobby")]
        public static void Build()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(DressingRoomSceneBuilder.ScenePath, OpenSceneMode.Single);
            if (Object.FindFirstObjectByType<ToiletLobby>() != null) { Refine(); return; }
            var view = Object.FindFirstObjectByType<LobbyView>(); var lobby = view.GetComponent<LobbyController>();
            var world = Object.FindFirstObjectByType<NatureCalls.Map.Playground>();
            var root = new GameObject("Toilet Lobby");
            var room = root.AddComponent<ToiletLobby>(); room.World = world; room.Lobby = lobby; room.View = view;
            var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/DressingRoom/Plaster.mat");
            var tiles = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/DressingRoom/Tiles.mat");
            Box(root.transform, "Floor", new Vector3(8, -.1f, 0), new Vector3(4.4f, .2f, 4.4f), tiles);
            Box(root.transform, "Ceiling", new Vector3(8, 3.1f, 0), new Vector3(4.4f, .2f, 4.4f), material);
            Box(root.transform, "Left Wall", new Vector3(5.8f, 1.5f, 0), new Vector3(.2f, 3, 4.4f), material);
            Box(root.transform, "Right Wall", new Vector3(10.2f, 1.5f, 0), new Vector3(.2f, 3, 4.4f), material);
            Box(root.transform, "Back Wall", new Vector3(8, 1.5f, 2.1f), new Vector3(4.4f, 3, .2f), material);
            Box(root.transform, "Entrance Wall", new Vector3(8, 1.5f, -2.1f), new Vector3(4.4f, 3, .2f), material);
            Box(root.transform, "Stall Partition", new Vector3(8, 1.15f, .85f), new Vector3(.12f, 2.3f, 2.4f), tiles);
            room.Entry = Point(root.transform, "Entry", new Vector3(8, .03f, -1.3f));
            var originalDoor = Object.FindObjectsByType<LobbyDoor>(FindObjectsSortMode.None).Single();
            room.ReturnPoint = originalDoor.StandingPoint;
            UnityEventTools.RemovePersistentListener(originalDoor.Interacted, 0);
            UnityEventTools.AddPersistentListener(originalDoor.Interacted, room.Enter);
            var exit = Box(root.transform, "Return Door", new Vector3(8, 1, -1.98f), new Vector3(.9f, 2, .08f), tiles);
            var trigger = exit.GetComponent<BoxCollider>(); trigger.isTrigger = true;
            var door = exit.AddComponent<LobbyDoor>(); door.InteractionDistance = 1.8f;
            door.StandingPoint = Point(root.transform, "Return Approach", new Vector3(8, .03f, -1.1f));
            UnityEventTools.AddPersistentListener(door.Interacted, room.Return);
            Sign(root.transform, "返回换装间", new Vector3(8, 2.35f, -1.94f), 180);
            var waterMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/DressingRoom/LobbyWater.mat");
            if (waterMaterial == null)
            {
                waterMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                waterMaterial.SetColor("_BaseColor", new Color(.12f, .33f, .34f)); waterMaterial.SetFloat("_Smoothness", .92f);
                AssetDatabase.CreateAsset(waterMaterial, "Assets/Materials/DressingRoom/LobbyWater.mat");
            }
            var toiletModel = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Models/Facilities/Facilities-Toilet.fbx");
            if (toiletModel == null) throw new InvalidOperationException("Missing existing toilet model.");
            for (int index = 0; index < 2; index++)
            {
                float x = 7 + index * 2;
                var stall = new GameObject(index == 0 ? "LAN Stall" : "Relay Stall"); stall.transform.SetParent(root.transform);
                var toilet = (GameObject)PrefabUtility.InstantiatePrefab(toiletModel, scene); toilet.transform.SetParent(stall.transform);
                toilet.name = "Connection Toilet"; toilet.transform.position = new Vector3(x, 0, 1.1f);
                Box(stall.transform, "Toilet Collision", new Vector3(x, .3f, 1.1f), new Vector3(.6f, .6f, .8f), null).GetComponent<Renderer>().enabled = false;
                var water = GameObject.CreatePrimitive(PrimitiveType.Cylinder); water.name = "Water Surface"; water.transform.SetParent(stall.transform);
                water.transform.position = new Vector3(x, .46f, .9f); water.transform.localScale = new Vector3(.47f, .004f, .58f);
                Object.DestroyImmediate(water.GetComponent<Collider>()); water.GetComponent<Renderer>().sharedMaterial = waterMaterial;
                room.Water[index] = water.transform;
                room.Approach[index] = Point(stall.transform, "Approach", new Vector3(x, .03f, .05f));
                Sign(stall.transform, index == 0 ? "局域网 LAN" : "房间号 Relay", new Vector3(x, 2.35f, 1.98f), 0);
                var lamp = new GameObject("Room Light").AddComponent<Light>(); lamp.transform.SetParent(stall.transform);
                lamp.transform.position = new Vector3(x, 2.65f, .1f); lamp.type = LightType.Point; lamp.range = 4; lamp.intensity = 2.2f;
            }
            StylePanel(view.LobbyPanel, 900, 1000); StylePanel(view.MapPanel, 900, 1000);
            Place((RectTransform)view.EntryGroup.transform, new Vector2(0, -130), new Vector2(800, 520));
            Place((RectTransform)view.RoomGroup.transform, new Vector2(0, -120), new Vector2(800, 700));
            view.Status.rectTransform.anchoredPosition = new Vector2(0, 25); view.Status.rectTransform.sizeDelta = new Vector2(860, 100);
            view.CodeInput.textComponent.color = Color.white; view.CodeInput.placeholder.GetComponent<Text>().color = new Color(1, 1, 1, .65f);
            view.CodeInput.characterLimit = 64;
            var inputImage = view.CodeInput.GetComponent<Image>(); if (inputImage != null) inputImage.color = new Color(1, 1, 1, .06f);
            Place(view.MapOptions, new Vector2(0, -170), new Vector2(800, 640));
            view.MapPanel.GetComponent<RectTransform>().sizeDelta = new Vector2(900, 1000);
            foreach (var item in view.LobbyPanel.GetComponentsInChildren<Transform>(true)) item.gameObject.layer = 5;
            foreach (var item in view.MapPanel.GetComponentsInChildren<Transform>(true)) item.gameObject.layer = 5;
            EditorUtility.SetDirty(view); EditorUtility.SetDirty(originalDoor); EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets(); Refine();
            Debug.Log("TOILET_LOBBY_BUILT|stalls=2|transport=LAN,Relay|waterUI=1|transition=none");
        }
        private static void Refine()
        {
            var room = Object.FindFirstObjectByType<ToiletLobby>();
            var subtitle = room.View.LobbyPanel.transform.Find("Flush Confirm");
            if (subtitle != null) Object.DestroyImmediate(subtitle.gameObject);
            for (int index = 0; index < 2; index++)
            {
                var parent = room.Water[index].parent;
                var added = parent.Find("Physical Flush Button");
                if (added != null) Object.DestroyImmediate(added.gameObject);
                var native = parent.GetComponentsInChildren<Transform>(true).FirstOrDefault(value => value.name == "Flush Button");
                if (native == null) throw new InvalidOperationException("The toilet's authored Flush Button mesh is missing.");
                var button = native.GetComponent<LobbyFlushButton>();
                if (button == null) button = native.gameObject.AddComponent<LobbyFlushButton>();
                button.Head = native;
                button.Room = room;
                button.Mode = index;
                if (native.GetComponent<Collider>() == null)
                {
                    var collider = native.gameObject.AddComponent<BoxCollider>();
                    var bounds = native.GetComponent<MeshFilter>().sharedMesh.bounds;
                    collider.center = bounds.center; collider.size = bounds.size + Vector3.one * .012f;
                }
                room.FlushButtons[index] = button;
                PrefabUtility.RecordPrefabInstancePropertyModifications(button);
            }
            EditorUtility.SetDirty(room);
            foreach (var water in room.Water)
            {
                water.localScale = new Vector3(.28f, .004f, .30f);
                var point = water.position; point.y = .38f; point.z = .98f; water.position = point;
                var material = water.GetComponent<Renderer>().sharedMaterial;
                material.SetFloat("_Smoothness", .25f); EditorUtility.SetDirty(material);
            }
            foreach (var panel in new[] { room.View.LobbyPanel, room.View.MapPanel })
                foreach (var button in panel.GetComponentsInChildren<Button>(true))
                {
                    button.transition = Selectable.Transition.None;
                    button.GetComponent<Image>().color = Color.clear;
                }
            foreach (var slot in room.View.Slots)
                if (slot.transform.parent.TryGetComponent<Image>(out var image)) image.color = Color.clear;
            room.View.CodeInput.transition = Selectable.Transition.None;
            room.View.CodeInput.GetComponent<Image>().color = Color.clear;
            room.View.CodeInput.textComponent.color = Color.white;
            room.View.CodeInput.textComponent.alignment = TextAnchor.MiddleCenter;
            var placeholder = room.View.CodeInput.placeholder.GetComponent<Text>();
            placeholder.color = new Color(1, 1, 1, .7f); placeholder.alignment = TextAnchor.MiddleCenter;
            placeholder.fontStyle = FontStyle.Normal;
            ToiletLobbyPresentation.Configure(room);
            foreach (var panel in new[] { room.View.LobbyPanel, room.View.MapPanel })
                foreach (var component in panel.GetComponentsInChildren<Component>(true))
                    if (component != null && PrefabUtility.IsPartOfPrefabInstance(component))
                        PrefabUtility.RecordPrefabInstancePropertyModifications(component);
            EditorSceneManager.MarkSceneDirty(room.gameObject.scene); EditorSceneManager.SaveScene(room.gameObject.scene); AssetDatabase.SaveAssets();
        }
        private static void StylePanel(GameObject panel, float width, float height)
        {
            if (panel.TryGetComponent<Image>(out var bg)) Object.DestroyImmediate(bg);
            var rect = (RectTransform)panel.transform; rect.sizeDelta = new Vector2(width, height); rect.pivot = new Vector2(.5f, .5f);
            var canvas = panel.AddComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace; canvas.overrideSorting = true; canvas.sortingOrder = 20;
            panel.AddComponent<GraphicRaycaster>();
            panel.AddComponent<CanvasGroup>();
            foreach (var button in panel.GetComponentsInChildren<Button>(true))
            {
                button.GetComponent<Image>().color = Color.clear;
                var colors = button.colors; colors.normalColor = Color.white; colors.highlightedColor = new Color(1, .86f, .5f); button.colors = colors;
            }
            foreach (var text in panel.GetComponentsInChildren<Text>(true)) { text.color = Color.white; var shadow = text.GetComponent<Shadow>(); if (shadow == null) shadow = text.gameObject.AddComponent<Shadow>(); shadow.effectDistance = new Vector2(1, -1); }
        }
        private static Button MakeButton(Transform parent, string name, string label, Vector2 at)
        {
            var rect = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button)).GetComponent<RectTransform>(); rect.SetParent(parent, false);
            Place(rect, at, new Vector2(780, 70)); rect.GetComponent<Image>().color = Color.clear;
            var text = new GameObject("Label", typeof(RectTransform), typeof(Text)).GetComponent<Text>(); text.transform.SetParent(rect, false);
            text.rectTransform.anchorMin = Vector2.zero; text.rectTransform.anchorMax = Vector2.one; text.rectTransform.offsetMin = text.rectTransform.offsetMax = Vector2.zero;
            text.text = label; text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); text.fontSize = 36; text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false; return rect.GetComponent<Button>();
        }
        private static void Place(RectTransform rect, Vector2 at, Vector2 size) { rect.anchorMin = rect.anchorMax = new Vector2(.5f, 1); rect.pivot = new Vector2(.5f, 1); rect.anchoredPosition = at; rect.sizeDelta = size; }
        private static Transform Point(Transform parent, string name, Vector3 at) { var point = new GameObject(name).transform; point.SetParent(parent); point.position = at; return point; }
        private static GameObject Box(Transform parent, string name, Vector3 at, Vector3 size, Material material)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube); box.name = name; box.transform.SetParent(parent); box.transform.position = at; box.transform.localScale = size;
            if (material != null) box.GetComponent<Renderer>().sharedMaterial = material; return box;
        }
        private static void Sign(Transform parent, string text, Vector3 at, float yaw)
        {
            var label = new GameObject(text).AddComponent<TextMesh>(); label.transform.SetParent(parent); label.transform.SetPositionAndRotation(at, Quaternion.Euler(0, yaw, 0));
            label.text = text; label.fontSize = 64; label.characterSize = .035f; label.anchor = TextAnchor.MiddleCenter; label.color = new Color(.18f, .35f, .3f);
        }
    }
}
