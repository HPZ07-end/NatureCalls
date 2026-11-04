using System.Collections.Generic;
using System.Linq;
using System.Net;
using NatureCalls.Core;
using NatureCalls.Map;
using NatureCalls.Network;
using NatureCalls.Player;
using UnityEngine;
using UnityEngine.UI;
using PlayerInput = NatureCalls.Input.PlayerInput;

namespace NatureCalls.UI
{
    /// <summary>
    /// DressingRoom overlay behaviour: mirror outfit panel, Lobby (offline / create / join / room) and map selection.
    /// Reads room state from <see cref="LobbyRoom"/> and connects through <see cref="Session"/>; layout lives in <see cref="LobbyView"/>.
    /// </summary>
    [RequireComponent(typeof(LobbyView))]
    public sealed class LobbyController : MonoBehaviour
    {
        [SerializeField] private MapCatalog maps;
        [SerializeField] private GameObject networkPrefab;

        private LobbyView view;
        private readonly List<Button> outfitButtons = new();
        private readonly List<Button> mapButtons = new();
        private bool offlineMapSelection;
        private bool awaitingConnection;
        private bool wasInRoom;
        private string message = "";
        // How the last round ended; shown above the room status until the next round starts.
        private string roundResult = "";

        /// <summary>Create LAN rooms even when Unity Cloud is linked (tests, or a network without Relay access).</summary>
        public bool PreferLan;

        public bool IsOutfitPanelOpen => view.OutfitPanel.activeSelf;
        public bool IsLobbyOpen => view.LobbyPanel.activeSelf;
        public bool IsMapPanelOpen => view.MapPanel.activeSelf;
        public IReadOnlyList<Button> MapButtons => mapButtons;
        public IReadOnlyList<Button> OutfitButtons => outfitButtons;

        private void Awake()
        {
            view = GetComponent<LobbyView>();
            view.OutfitOptionTemplate.gameObject.SetActive(false);
            view.MapOptionTemplate.gameObject.SetActive(false);
            view.OutfitConfirm.onClick.AddListener(ConfirmOutfit);
            view.OutfitCancel.onClick.AddListener(CancelOutfit);
            view.OfflineButton.onClick.AddListener(StartOffline);
            view.CreateButton.onClick.AddListener(CreateRoom);
            view.JoinButton.onClick.AddListener(JoinRoom);
            view.CloseButton.onClick.AddListener(CloseLobby);
            view.PrimaryButton.onClick.AddListener(PressPrimary);
            view.LeaveButton.onClick.AddListener(LeaveRoom);
            view.MapBack.onClick.AddListener(BackFromMap);
            BuildMapButtons();
            HideAll();
        }

        private void Start()
        {
            message = Match.TakeReturnMessage();
            if (!string.IsNullOrEmpty(message)) { roundResult = message; OpenLobby(); }
        }

        // ---- Prompts and mirror (wired from DressingMirror / LobbyDoor UnityEvents) ----

        public void ShowMirrorPrompt() { if (!AnyPanelOpen) view.MirrorPrompt.SetActive(true); }
        public void HideMirrorPrompt() => view.MirrorPrompt.SetActive(false);
        public void ShowDoorPrompt() { if (!AnyPanelOpen) view.DoorPrompt.SetActive(true); }
        public void HideDoorPrompt() => view.DoorPrompt.SetActive(false);

        public void OpenOutfitPanel()
        {
            var outfits = LocalOutfits;
            if (outfits == null) { Debug.LogWarning("LOBBY_NO_OUTFIT_CONTROLLER"); return; }
            BuildOutfitButtons(outfits.Catalog);
            outfits.BeginSelection();
            HidePrompts();
            view.OutfitPanel.SetActive(true);
            SetPlayerControl(false);
            RefreshOutfit();
        }

        public void PreviewOutfit(int index)
        {
            LocalOutfits.Preview(index);
            RefreshOutfit();
        }

        public void ConfirmOutfit()
        {
            LocalOutfits.ConfirmSelection();
            if (Room != null) Room.SetOutfit(LocalProfile.OutfitIndex);
            CloseOutfitPanel();
        }

        public void CancelOutfit()
        {
            LocalOutfits.CancelSelection();
            CloseOutfitPanel();
        }

        private void CloseOutfitPanel()
        {
            view.OutfitPanel.SetActive(false);
            SetPlayerControl(true);
        }

        private void RefreshOutfit()
        {
            var outfits = LocalOutfits;
            view.OutfitCurrent.text = "当前预览：" + outfits.PreviewName;
        }

        // ---- Lobby ----

        public void OpenLobby()
        {
            HidePrompts();
            view.Blur.Set(true);
            view.LobbyPanel.SetActive(true);
            SetPlayerControl(false);
        }

        public void CloseLobby()
        {
            if (InRoom) return;
            offlineMapSelection = false;
            roundResult = "";
            view.LobbyPanel.SetActive(false);
            view.MapPanel.SetActive(false);
            view.Blur.Set(false);
            SetPlayerControl(true);
        }

        public void StartOffline()
        {
            if (InRoom || Busy) return;
            offlineMapSelection = true;
            message = "单人离线测试：请选择地图。";
        }

        public void CreateRoom()
        {
            if (InRoom || Busy) return;
            var session = EnsureSession();
            session.LobbyMode = true;
            awaitingConnection = true;
            message = "正在创建房间…";
            if (Session.CloudLinked && !PreferLan) _ = session.ConnectRelayAsync(true);
            else if (!session.StartDirect(true)) message = session.Status;
        }

        public void JoinRoom()
        {
            if (InRoom || Busy) return;
            var code = view.CodeInput.text.Trim();
            if (string.IsNullOrEmpty(code)) { message = "请输入房间号（局域网房间为房主地址）。"; return; }
            var isAddress = IPAddress.TryParse(code, out _);
            if (!isAddress && !Session.CloudLinked) { message = "未关联 Unity Cloud：请输入房主的局域网地址。"; return; }
            var session = EnsureSession();
            session.LobbyMode = true;
            awaitingConnection = true;
            message = "正在加入房间…";
            if (isAddress) { if (!session.StartDirect(false, code)) message = session.Status; }
            else _ = session.ConnectRelayAsync(false, code);
        }

        public void PressPrimary()
        {
            var room = Room;
            if (room == null) return;
            if (room.IsHostPlayer) room.RequestStart();
            else room.SetReady(!LocalMember(room).Ready);
        }

        public void LeaveRoom()
        {
            if (Session.Instance != null && Session.Instance.Active) _ = Session.Instance.LeaveAsync();
            message = "已离开房间。";
            wasInRoom = false;
        }

        // ---- Map selection ----

        public void ChooseMap(int choice)
        {
            if (offlineMapSelection)
            {
                var index = MapSelection.Resolve(choice, maps.Count, count => Random.Range(0, count));
                offlineMapSelection = false;
                Match.Begin(MatchMode.Offline, maps, index);
                return;
            }
            var room = Room;
            if (room != null && room.IsHostPlayer) room.ChooseMap(choice);
        }

        public void BackFromMap()
        {
            if (offlineMapSelection) { offlineMapSelection = false; message = ""; return; }
            var room = Room;
            if (room != null && room.IsHostPlayer) room.CancelMapSelection();
        }

        // ---- View refresh ----

        private void Update()
        {
            if (!view.LobbyPanel.activeSelf) return;
            var room = Room;
            var session = Session.Instance;
            bool inRoom = room != null;
            bool selectingMap = offlineMapSelection || (inRoom && room.Phase.Value == LobbyPhase.SelectingMap);

            view.EntryGroup.SetActive(!inRoom && !offlineMapSelection);
            view.RoomGroup.SetActive(inRoom && !selectingMap);
            view.MapPanel.SetActive(selectingMap);
            bool canChoose = offlineMapSelection || (inRoom && room.IsHostPlayer);
            foreach (var button in mapButtons) button.gameObject.SetActive(canChoose);
            view.MapBack.gameObject.SetActive(canChoose);
            view.MapHint.text = canChoose ? "选择本局地图" : "等待房主选择地图…";

            bool busy = Busy;
            view.OfflineButton.interactable = view.CreateButton.interactable = view.JoinButton.interactable = !busy;
            if (inRoom) { awaitingConnection = false; RefreshRoom(room, session); }
            else if (session != null && !busy && !session.Active && (awaitingConnection || wasInRoom))
            {
                // Connection failed, was refused, or the room closed: show the Session reason.
                awaitingConnection = false;
                message = session.Status;
            }
            wasInRoom = inRoom;
            if (inRoom && room.Phase.Value != LobbyPhase.Waiting) roundResult = "";
            var status = busy && session != null ? session.Status : message;
            view.Status.text = string.IsNullOrEmpty(roundResult) || status == roundResult ? status : roundResult + "\n" + status;
        }

        private void RefreshRoom(LobbyRoom room, Session session)
        {
            view.RoomCode.text = "房间号：" + (session.IsRelay ? session.JoinCode : LanRoomCode());
            var catalog = LocalCatalog;
            for (int slot = 0; slot < view.Slots.Length; slot++)
            {
                view.Slots[slot].text = "空位";
                foreach (var member in room.Members)
                {
                    if (member.Slot != slot) continue;
                    var host = member.ClientId == Unity.Netcode.NetworkManager.ServerClientId;
                    var you = member.ClientId == room.LocalClientId ? "（你）" : "";
                    var outfit = catalog != null && catalog.Contains(member.Outfit) ? catalog[member.Outfit].Name : "-";
                    var state = host ? "房主" : member.Ready ? "已准备" : "未准备";
                    view.Slots[slot].text = $"玩家 {slot + 1}{you}\n{state} · {outfit}";
                }
            }
            if (room.IsHostPlayer)
            {
                view.PrimaryLabel.text = "开始游戏";
                view.PrimaryButton.interactable = room.CanStart;
                if (!room.CanStart) message = $"等待玩家：{room.Members.Count}/{LobbyRules.MaxPlayers}，需至少 {LobbyRules.MinPlayers} 人且全员准备。";
                else message = "全员已准备，可以开始游戏。";
            }
            else
            {
                bool ready = LocalMember(room).Ready;
                view.PrimaryLabel.text = ready ? "取消准备" : "准备";
                view.PrimaryButton.interactable = room.Phase.Value == LobbyPhase.Waiting;
                message = ready ? "已准备，等待房主开始。" : "准备好后点击「准备」。";
            }
        }

        // ---- Helpers ----

        // The best guess first; other candidates (second adapter, Tailscale) follow in case it is the wrong one.
        private static string LanRoomCode()
        {
            var addresses = Session.LocalAddresses();
            if (addresses.Length == 0) return "未找到局域网地址（检查网络连接）";
            return addresses[0] + "（局域网）" + (addresses.Length > 1 ? "  其他：" + string.Join("、", addresses.Skip(1)) : "");
        }

        private static LobbyRoom Room => LobbyRoom.Current;
        private static bool InRoom => LobbyRoom.Current != null;
        private static bool Busy => Session.Instance != null && Session.Instance.Busy;
        private bool AnyPanelOpen => view.OutfitPanel.activeSelf || view.LobbyPanel.activeSelf;

        private static LobbyMember LocalMember(LobbyRoom room) =>
            room.TryGetMember(room.LocalClientId, out var member) ? member : default;

        private static Movement LocalPlayer
        {
            get
            {
                var world = FindFirstObjectByType<Playground>();
                return world != null ? world.OfflinePlayer : null;
            }
        }
        private static PlayerOutfitController LocalOutfits => LocalPlayer != null ? LocalPlayer.GetComponent<PlayerOutfitController>() : null;
        private static OutfitCatalog LocalCatalog => LocalPlayer != null ? LocalPlayer.GetComponent<PlayerAppearance>().Catalog : null;

        private Session EnsureSession()
        {
            if (Session.Instance == null) Instantiate(networkPrefab).name = "Network";
            return Session.Instance;
        }

        private static void SetPlayerControl(bool enabled)
        {
            var player = LocalPlayer;
            if (player != null) player.SetControl(enabled);
            if (!enabled) PlayerInput.Unlock();
        }

        private void HidePrompts()
        {
            view.MirrorPrompt.SetActive(false);
            view.DoorPrompt.SetActive(false);
        }

        private void HideAll()
        {
            HidePrompts();
            view.OutfitPanel.SetActive(false);
            view.LobbyPanel.SetActive(false);
            view.MapPanel.SetActive(false);
            view.Blur.Set(false);
        }

        private void BuildOutfitButtons(OutfitCatalog catalog)
        {
            if (outfitButtons.Count == catalog.Count) return;
            foreach (var button in outfitButtons) Destroy(button.gameObject);
            outfitButtons.Clear();
            for (int index = 0; index < catalog.Count; index++)
            {
                var choice = index;
                var button = CloneOption(view.OutfitOptionTemplate, view.OutfitOptions, catalog[index].Name);
                var swatch = button.GetComponent<Image>();
                if (swatch != null) swatch.color = catalog[index].Color;
                button.onClick.AddListener(() => PreviewOutfit(choice));
                outfitButtons.Add(button);
            }
        }

        private void BuildMapButtons()
        {
            for (int index = 0; index < maps.Count; index++)
            {
                var choice = index;
                var button = CloneOption(view.MapOptionTemplate, view.MapOptions, maps[index].DisplayName);
                button.onClick.AddListener(() => ChooseMap(choice));
                mapButtons.Add(button);
            }
            var random = CloneOption(view.MapOptionTemplate, view.MapOptions, MapCatalog.RandomName);
            random.onClick.AddListener(() => ChooseMap(MapSelection.Random));
            mapButtons.Add(random);
        }

        private static Button CloneOption(Button template, RectTransform parent, string label)
        {
            var button = Instantiate(template, parent);
            button.gameObject.SetActive(true);
            button.name = template.name + " " + label;
            var text = button.GetComponentInChildren<Text>(true);
            if (text != null) text.text = label;
            return button;
        }
    }
}
