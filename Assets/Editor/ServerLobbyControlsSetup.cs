using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using NatureCalls.UI;

namespace NatureCalls.Editor
{
    /// <summary>Adds the PR #35 room-code and LAN-discovery controls to the authored lobby prefab.</summary>
    public static class ServerLobbyControlsSetup
    {
        public const string CanvasPath = "Assets/UI/Lobby/LobbyCanvas.prefab";

        public static void Configure()
        {
            var root = PrefabUtility.LoadPrefabContents(CanvasPath);
            try
            {
                var view = root.GetComponentInChildren<LobbyView>(true);
                if (view == null) throw new System.InvalidOperationException("LobbyCanvas has no LobbyView.");
                var entry = view.EntryGroup.transform as RectTransform;
                var createServer = view.CreateServerButton;
                if (createServer == null)
                {
                    createServer = Object.Instantiate(view.CreateButton, entry);
                    createServer.name = "CreateServerRoom";
                    createServer.GetComponentInChildren<Text>(true).text = "创建服务器房间";
                    createServer.transform.SetSiblingIndex(view.CreateButton.transform.GetSiblingIndex() + 1);
                }
                var createImage = createServer.GetComponent<Image>();
                if (createImage != null) { var color = createImage.color; color.a = 0f; createImage.color = color; }

                var lanRooms = view.LanRooms;
                if (lanRooms == null)
                {
                    var objectValue = new GameObject("LanRooms", typeof(RectTransform), typeof(VerticalLayoutGroup));
                    objectValue.transform.SetParent(entry, false);
                    lanRooms = objectValue.GetComponent<RectTransform>();
                    lanRooms.sizeDelta = new Vector2(560, 180);
                    lanRooms.anchoredPosition = new Vector2(0, -500);
                    var layout = objectValue.GetComponent<VerticalLayoutGroup>();
                    layout.childControlWidth = true; layout.childControlHeight = false; layout.spacing = 6;
                }
                var template = view.LanRoomTemplate;
                if (template == null)
                {
                    template = Object.Instantiate(view.JoinButton, lanRooms);
                    template.name = "LanRoomTemplate";
                    template.GetComponentInChildren<Text>(true).text = "局域网房间";
                    template.gameObject.SetActive(false);
                }

                var serialized = new SerializedObject(view);
                serialized.FindProperty(nameof(LobbyView.CreateServerButton)).objectReferenceValue = createServer;
                serialized.FindProperty(nameof(LobbyView.LanRooms)).objectReferenceValue = lanRooms;
                serialized.FindProperty(nameof(LobbyView.LanRoomTemplate)).objectReferenceValue = template;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(view);
                PrefabUtility.SaveAsPrefabAsset(root, CanvasPath);
                AssetDatabase.SaveAssets();
                Debug.Log("SERVER_LOBBY_CONTROLS_READY|createServer=1|codeInput=preserved|joinRoom=preserved|lanRooms=1|guid=" + AssetDatabase.AssetPathToGUID(CanvasPath));
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
