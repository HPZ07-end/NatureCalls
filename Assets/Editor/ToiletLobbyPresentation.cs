using System;
using System.Linq;
using NatureCalls.Facility;
using NatureCalls.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace NatureCalls.Editor
{
    internal static class ToiletLobbyPresentation
    {
        public static void Configure(ToiletLobby room)
        {
            ResizeHall(room);
            if (room.transform.Find("Lobby Hall Light") == null)
            {
                var hall = new GameObject("Lobby Hall Light").AddComponent<Light>(); hall.transform.SetParent(room.transform);
                hall.transform.position = new Vector3(8, 2.55f, -1.25f); hall.type = LightType.Point;
                hall.intensity = 2.4f; hall.range = 4; hall.color = new Color(1, .95f, .83f);
            }
            foreach (var text in room.GetComponentsInChildren<TextMesh>())
                if (text.text == "局域网 LAN" || text.text == "房间号 Relay") Object.DestroyImmediate(text.gameObject);
            for (int i = 0; i < 2; i++) if (room.Doors[i] == null) AddDoor(room, i);
            room.RoomLettering = room.GetComponentsInChildren<TextMesh>().Select(text => text.GetComponent<Renderer>()).ToArray();
            Layout(room.View);
            EditorUtility.SetDirty(room);
        }
        private static void ResizeHall(ToiletLobby room)
        {
            foreach (string name in new[] { "Floor", "Ceiling", "Left Wall", "Right Wall" })
            {
                var part = room.transform.Find(name);
                var position = part.position; position.z = -1; part.position = position;
                var size = part.localScale; size.z = 6.4f; part.localScale = size;
            }
            foreach (string name in new[] { "Entrance Wall", "Return Door", "返回换装间" })
            {
                var part = room.transform.Find(name); if (part == null) continue;
                var position = part.position; position.z = name == "Entrance Wall" ? -4.1f : -3.98f; part.position = position;
            }
            room.Entry.position = new Vector3(8, .03f, -3.1f);
            room.transform.Find("Return Approach").position = room.Entry.position;
            if (room.transform.Find("Entrance Light") == null)
            {
                var light = new GameObject("Entrance Light").AddComponent<Light>(); light.transform.SetParent(room.transform);
                light.transform.position = new Vector3(8, 2.6f, -3); light.type = LightType.Point; light.range = 4; light.intensity = 1.4f;
            }
        }
        private static Material Mat(string name, Color color, Texture texture = null)
        {
            string path = "Assets/Materials/DressingRoom/" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) { mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(mat, path); }
            mat.SetColor("_BaseColor", color); mat.SetFloat("_Smoothness", .15f);
            if (texture != null) mat.SetTexture("_BaseMap", texture);
            EditorUtility.SetDirty(mat); return mat;
        }
        private static void AddDoor(ToiletLobby room, int index)
        {
            var parent = room.Water[index].parent;
            float x = room.Water[index].position.x;
            var door = parent.gameObject.AddComponent<StallDoor>(); room.Doors[index] = door;
            var hinge = new GameObject("Door Leaf Hinge").transform; hinge.SetParent(parent); hinge.position = new Vector3(x - .55f, 0, -.35f);
            var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Models/Facilities/Facilities-StallDoor.fbx"));
            model.transform.SetParent(hinge, false);
            var bounds = BoundsOf(model); model.transform.localScale = new Vector3(1.1f / bounds.size.x, 2 / bounds.size.y, 1);
            bounds = BoundsOf(model); model.transform.position += new Vector3(hinge.position.x - bounds.min.x, -bounds.min.y, -.35f - bounds.center.z);
            door.DoorLeaf = hinge;
            var collision = new GameObject("Door Collider Hinge", typeof(BoxCollider)); collision.transform.SetParent(parent); collision.transform.position = hinge.position;
            collision.GetComponent<BoxCollider>().center = new Vector3(.55f, 1, 0); collision.GetComponent<BoxCollider>().size = new Vector3(1.1f, 2, .1f);
            door.DoorColliderPivot = collision.transform; door.InteractionDistance = 1.7f;
            var tiles = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/DressingRoom/Tiles.mat");
            foreach (float offset in new[] { -.78f, .78f })
            {
                var panel = GameObject.CreatePrimitive(PrimitiveType.Cube); panel.name = "Stall Front Panel"; panel.transform.SetParent(parent);
                panel.transform.position = new Vector3(x + offset, 1.1f, -.35f); panel.transform.localScale = new Vector3(.44f, 2.2f, .12f); panel.GetComponent<Renderer>().sharedMaterial = tiles;
            }
            var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Models/Signs/Signs-ConnectionHanging.fbx");
            if (source == null) throw new InvalidOperationException("Import the authored hanging sign first.");
            var importer = (ModelImporter)AssetImporter.GetAtPath("Assets/Art/Models/Signs/Signs-ConnectionHanging.fbx");
            if (!importer.isReadable) { importer.isReadable = true; importer.SaveAndReimport(); source = AssetDatabase.LoadAssetAtPath<GameObject>(importer.assetPath); }
            var sign = (GameObject)PrefabUtility.InstantiatePrefab(source); sign.name = index == 0 ? "本地局域网连接" : "远程房间号连接";
            sign.transform.SetParent(hinge, false); sign.transform.localPosition = new Vector3(.55f, 1.48f, -.11f);
            var kind = index == 0 ? "LAN" : "Relay";
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Textures/Signs/Signs-Connection-" + kind + ".png");
            var face = Mat("HangingSign" + kind, Color.white, texture);
            var board = Mat("HangingSignBoard", new Color(.24f, .34f, .27f)); var rope = Mat("HangingSignRope", new Color(.36f, .25f, .14f));
            foreach (var renderer in sign.GetComponentsInChildren<Renderer>())
            {
                var previous = renderer.sharedMaterials;
                renderer.sharedMaterials = previous.Select(mat => mat != null && mat.name.Contains("Face") ? face : mat != null && mat.name.Contains("Rope") ? rope : board).ToArray();
                if (previous.Any(mat => mat != null && mat.name.Contains("Face")))
                {
                    var filter = renderer.GetComponent<MeshFilter>();
                    if (filter != null && filter.sharedMesh.normals.Length > 0 && Vector3.Dot(renderer.transform.TransformDirection(filter.sharedMesh.normals[0]), Vector3.back) < 0)
                        sign.transform.Rotate(0, 180, 0);
                }
            }
            Debug.Log("LOBBY_HANGING_SIGN|" + sign.name + "|bounds=" + BoundsOf(sign));
        }
        private static Bounds BoundsOf(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(); var bounds = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds); return bounds;
        }
        private static void Place(RectTransform rect, float x, float y, float w, float h)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, 1); rect.pivot = new Vector2(.5f, 1);
            rect.anchoredPosition = new Vector2(x, y); rect.sizeDelta = new Vector2(w, h);
        }
        private static void Text(Text text, int size, float x, float y, float w, float h)
        {
            Place(text.rectTransform, x, y, w, h); text.fontSize = size; text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color(.98f, .96f, .87f); text.fontStyle = FontStyle.Normal;
        }
        private static void Button(Button button, float x, float y, float w, int font)
        {
            Place((RectTransform)button.transform, x, y, w, 60);
            var text = button.GetComponentInChildren<Text>(true); text.fontSize = font; text.color = new Color(.98f, .96f, .87f);
        }
        private static void Free(GameObject group)
        {
            if (group.TryGetComponent<VerticalLayoutGroup>(out var layout)) Object.DestroyImmediate(layout);
        }
        private static void Layout(LobbyView v)
        {
            foreach (var panel in new[] { v.LobbyPanel, v.MapPanel }) ((RectTransform)panel.transform).sizeDelta = new Vector2(640, 620);
            Text(v.LobbyPanel.transform.Find("Title").GetComponent<Text>(), 42, 0, -20, 610, 65);
            Free(v.EntryGroup); Place((RectTransform)v.EntryGroup.transform, 0, -110, 620, 390);
            Button(v.OfflineButton, 0, -5, 520, 28);
            Button(v.CreateButton, -145, -85, 280, 38); Button(v.JoinButton, 145, -85, 280, 38);
            Place((RectTransform)v.CodeInput.transform, 0, -165, 550, 75);
            v.CodeInput.textComponent.fontSize = 38; v.CodeInput.placeholder.GetComponent<Text>().fontSize = 36;
            Button(v.CloseButton, 0, -270, 380, 28);
            Free(v.RoomGroup); Place((RectTransform)v.RoomGroup.transform, 0, -100, 620, 410);
            Text(v.RoomCode, 29, 0, 0, 610, 55);
            for (int i = 0; i < 4; i++)
            {
                Place((RectTransform)v.Slots[i].transform.parent, i % 2 == 0 ? -150 : 150, -80 - i / 2 * 95, 285, 85);
                v.Slots[i].fontSize = 32;
            }
            Button(v.PrimaryButton, 0, -295, 560, 38); Button(v.LeaveButton, 0, -375, 500, 28);
            Text(v.Status, 27, 0, -540, 610, 75);
            Text(v.MapPanel.transform.Find("Title").GetComponent<Text>(), 42, 0, -15, 610, 60);
            Place(v.MapOptions, 0, -150, 600, 360);
            var mapLayout = v.MapOptions.GetComponent<VerticalLayoutGroup>(); mapLayout.spacing = 10;
            v.MapOptionTemplate.GetComponent<LayoutElement>().preferredHeight = 54;
            v.MapOptionTemplate.GetComponentInChildren<Text>(true).fontSize = 38;
            Button(v.MapBack, 0, -540, 500, 30);
        }
    }
}
