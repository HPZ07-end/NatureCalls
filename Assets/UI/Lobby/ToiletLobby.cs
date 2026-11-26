using System.Collections;
using NatureCalls.Map;
using NatureCalls.Network;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using NatureCalls.Facility;
using NatureCalls.Player;

namespace NatureCalls.UI
{
    /// <summary>Local lobby presentation. Connections and room rules remain owned by LobbyController.</summary>
    [DefaultExecutionOrder(2000)]
    public sealed class ToiletLobby : MonoBehaviour
    {
        public Playground World;
        public LobbyController Lobby;
        public LobbyView View;
        public Transform Entry, ReturnPoint;
        public Transform[] Water = new Transform[2];
        public Transform[] Approach = new Transform[2];
        public LobbyFlushButton[] FlushButtons = new LobbyFlushButton[2];
        public StallDoor[] Doors = new StallDoor[2];
        public Renderer[] RoomLettering;
        public bool Inside { get; private set; }
        public bool Focused { get; private set; }
        public bool Transitioning { get; private set; }
        public bool Departing { get; internal set; }
        public int Mode { get; private set; }
        /// <summary>The 隔间大厅 page: existing rooms listed on the water, with a code/address field below.</summary>
        public bool JoinPage { get; private set; }
        private float dwell, suppressUntil;
        private Vector3 originalPosition;
        private Quaternion originalRotation;
        private float originalFov;
        private int candidate = -1;
        private int blockedMode = -1;
        private UnityEngine.Camera Camera => World.Camera.Output;
        private void Awake() => SetLettering(false);
        private void SetLettering(bool visible) { if (RoomLettering != null) foreach (var item in RoomLettering) if (item != null) item.enabled = visible; }
        public void Enter()
        {
            if (World.OfflinePlayer == null || Transitioning) return;
            Teleport(Entry); Inside = true; SetLettering(true); suppressUntil = Time.unscaledTime + .5f;
        }
        public void Return()
        {
            if (Focused || Transitioning || World.OfflinePlayer == null) return;
            Teleport(ReturnPoint); Inside = false; SetLettering(false);
        }
        private void Teleport(Transform point)
        {
            var player = World.OfflinePlayer;
            var collider = player.GetComponent<CharacterController>(); collider.enabled = false;
            player.transform.SetPositionAndRotation(point.position, point.rotation);
            collider.enabled = player.Controlled; Physics.SyncTransforms();
            Camera.transform.SetPositionAndRotation(player.transform.position + Vector3.up * World.Camera.EyeHeight, player.ViewRotation);
        }
        private void Update()
        {
            if (!Inside || Focused || Transitioning || Time.unscaledTime < suppressUntil || World.OfflinePlayer == null) return;
            var player = World.OfflinePlayer;
            int found = -1;
            for (int index = 0; index < Water.Length; index++)
            {
                if (Doors[index] != null && Doors[index].State != StallDoorState.Open && !Doors[index].IsInside(player)) continue;
                var delta = Water[index].position - Camera.transform.position;
                if (Vector3.Distance(player.transform.position, Approach[index].position) < 1.25f &&
                    Vector3.Dot(Camera.transform.forward, delta.normalized) > .96f) found = index;
            }
            if (found != candidate) { candidate = found; dwell = 0; }
            if (found == blockedMode && found >= 0) return;
            if (found != blockedMode) blockedMode = -1;
            if (found >= 0 && (dwell += Time.unscaledDeltaTime) >= .3f) Focus(found);
        }
        public void OpenDefault()
        {
            if (!Inside) Enter();
            Focus(Session.Instance != null && Session.Instance.Active && Session.Instance.IsRelay ? 1 : 0);
        }
        public void Focus(int mode)
        {
            if (Focused || Transitioning || World.OfflinePlayer == null || mode < 0 || mode >= Water.Length) return;
            Mode = mode; JoinPage = false; Focused = true;
            originalPosition = Camera.transform.position; originalRotation = Camera.transform.rotation; originalFov = Camera.fieldOfView;
            World.OfflinePlayer.SetControl(false); NatureCalls.Input.PlayerInput.Unlock();
            World.Camera.Brain.enabled = false;
            StartCoroutine(Blend(true));
        }
        private IEnumerator Blend(bool opening)
        {
            Transitioning = true;
            var start = Camera.transform.position; var rotation = Camera.transform.rotation; float fov = Camera.fieldOfView;
            var target = opening ? Water[Mode].position + Vector3.up * 1.2f + Vector3.forward * .2f : originalPosition;
            var targetRotation = opening ? Quaternion.Euler(90, 0, 0) : originalRotation;
            for (float time = 0; time < .8f; time += Time.unscaledDeltaTime)
            {
                float t = Mathf.SmoothStep(0, 1, time / .8f);
                Camera.transform.SetPositionAndRotation(Vector3.Lerp(start, target, t), Quaternion.Slerp(rotation, targetRotation, t));
                Camera.fieldOfView = Mathf.Lerp(fov, opening ? 35 : originalFov, t);
                yield return null;
            }
            Camera.transform.SetPositionAndRotation(target, targetRotation); Camera.fieldOfView = opening ? 35 : originalFov;
            Transitioning = false;
            if (opening) { Lobby.PreferLan = Mode == 0; View.CodeInput.text = ""; Lobby.OpenLobby(); }
            else
            {
                Focused = false; World.Camera.Brain.enabled = true; World.OfflinePlayer.SetControl(true);
                blockedMode = Mode; candidate = -1; dwell = 0; suppressUntil = Time.unscaledTime + .3f;
                if (Application.isFocused && World.OfflinePlayer.GetComponent<NatureCalls.Input.PlayerInput>().CaptureMouse)
                { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
            }
        }
        public void CloseView()
        {
            if (Departing) return;
            if (!Focused) { World.OfflinePlayer.SetControl(true); World.Camera.Brain.enabled = true; return; }
            StopAllCoroutines();
            World.OfflinePlayer.SetControl(false); StartCoroutine(Blend(false));
        }
        public void PlayDeparture(System.Action completed)
        {
            if (Departing) return;
            StopAllCoroutines();
            if (Session.Instance != null && Session.Instance.Active) Mode = Session.Instance.IsRelay ? 1 : 0;
            Inside = true; Focused = true; Transitioning = true; Departing = true;
            World.OfflinePlayer.SetControl(false); World.Camera.Brain.enabled = false;
            View.LobbyPanel.SetActive(false); View.MapPanel.SetActive(false);
            var animation = gameObject.AddComponent<ToiletDeparture>();
            animation.Begin(this, Mode, completed);
        }
        /// <summary>创建房间 creates right away; the room page (room code and players) follows once connected.</summary>
        public void SelectCreate()
        {
            if (!Focused || Transitioning) return;
            JoinPage = false;
            Lobby.SubmitConnection(true, Mode == 0);
        }
        /// <summary>加入房间 opens the 隔间大厅 page.</summary>
        public void SelectJoin()
        {
            if (!Focused || Transitioning) return;
            JoinPage = true; View.CodeInput.text = "";
            View.CodeInput.gameObject.SetActive(true); View.CodeInput.ActivateInputField();
        }
        public void ShowHome() => JoinPage = false;
        /// <summary>Joins with the typed room code or address (Enter or the physical flush button).</summary>
        public void Submit()
        {
            if (!Focused || Transitioning || !View.EntryGroup.activeSelf || !JoinPage) return;
            Lobby.SubmitConnection(false, Mode == 0);
        }
        private bool CanSubmitByFlush => (View.EntryGroup.activeSelf && JoinPage) || Lobby.CanConfirmMap;
        public bool TryFlushFromPointer() => Mouse.current != null && TryFlush(Camera.ScreenPointToRay(Mouse.current.position.ReadValue()));
        public bool TryFlush(Ray ray)
        {
            if (!Focused || Transitioning || View.CodeInput.isFocused ||
                !CanSubmitByFlush ||
                (Session.Instance != null && (Session.Instance.Busy || (Session.Instance.Active && !Lobby.CanConfirmMap)))) return false;
            if (!Physics.Raycast(ray, out var hit, 3, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return false;
            var button = hit.collider.GetComponentInParent<LobbyFlushButton>();
            if (button == null || button != FlushButtons[Mode] || button.Pressing) return false;
            button.Press();
            if (Lobby.CanConfirmMap) Lobby.ConfirmMapSelection(); else Submit();
            return true;
        }
        public bool CanInteractButton(Movement player, int mode) => Focused && !Transitioning && mode == Mode &&
            player == World.OfflinePlayer && (View.CodeInput == null || !View.CodeInput.isFocused) &&
            CanSubmitByFlush;
        public bool ActivateButton(int mode)
        {
            if (!CanInteractButton(World.OfflinePlayer, mode)) return false;
            var button = FlushButtons[mode];
            if (button == null || button.Pressing) return false;
            button.Press();
            if (Lobby.CanConfirmMap) Lobby.ConfirmMapSelection(); else Submit();
            return true;
        }
        private void LateUpdate()
        {
            if (!Focused || Departing) return;
            Place(View.LobbyPanel); Place(View.MapPanel);
            UnderwaterText.ClipCenter = Water[Mode].position; UnderwaterText.ClipRadius = WaterRadius;
            if (Time.unscaledTime >= nextSubmerge) { Submerge(View.LobbyPanel); Submerge(View.MapPanel); nextSubmerge = Time.unscaledTime + .25f; }
            bool joining = JoinPage && View.EntryGroup.activeSelf;
            View.LobbyPanel.transform.Find("Title").GetComponent<Text>().text = joining ? "隔间大厅" : Mode == 0 ? "局域网联机" : "远程房间号联机";
            View.CodeInput.placeholder.GetComponent<Text>().text = Mode == 0 ? "输入房主 IP:端口，回车加入" : "输入房间号，回车加入";
            View.CodeInput.gameObject.SetActive(joining);
            ArrangePage(joining);
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame && !Transitioning)
                RippleAt(Camera.ScreenPointToRay(Mouse.current.position.ReadValue()));
            var keyboard = Keyboard.current;
            if (keyboard != null && joining && (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame) &&
                View.CodeInput.text.Trim().Length > 0) Submit();
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) { if (joining) ShowHome(); else Lobby.CloseLobby(); }
        }
        // Everything on the water is laid out from the water's centre so it stays inside the bowl opening
        // (radius ~WaterRadius panel units). Two entry pages share EntryGroup. Home: 单人离线测试 / 创建房间 /
        // 加入房间 / 返回准备房. 隔间大厅: the room list on top, the code field below, 返回 back to Home. The legacy
        // "创建服务器房间" button has no slot: the remote stall's 创建房间 already creates a course-server room.
        private void ArrangePage(bool joining)
        {
            if (View.CreateServerButton != null) View.CreateServerButton.gameObject.SetActive(false);
            Centre(View.EntryGroup, 560, 560); Centre(View.RoomGroup, 560, 560);
            Label(View.LobbyPanel.transform.Find("Title").GetComponent<Text>(), 0, 178, 360, 56, 38);
            Label(View.Status, 0, -186, 300, 52, 20);

            View.OfflineButton.gameObject.SetActive(!joining);
            View.CreateButton.gameObject.SetActive(!joining);
            View.JoinButton.gameObject.SetActive(!joining);
            Item(View.OfflineButton, 0, 100, 300, 50, 26);
            Item(View.CreateButton, -108, 25, 200, 60, 34);
            Item(View.JoinButton, 108, 25, 200, 60, 34);
            At(View.LanRooms, 0, 50, 430, 190);
            At(Lobby.ServerRooms, 0, 50, 430, 190);
            At((RectTransform)View.CodeInput.transform, 0, -78, 380, 60);
            View.CodeInput.textComponent.fontSize = 30; View.CodeInput.placeholder.GetComponent<Text>().fontSize = 26;
            Item(View.CloseButton, 0, joining ? -138 : -58, 260, 50, 26);
            View.CloseButton.GetComponentInChildren<Text>().text = joining ? "返回" : "返回准备房";

            Label(View.RoomCode, 0, 142, 400, 40, 22);
            for (int slot = 0; slot < View.Slots.Length; slot++)
            {
                At((RectTransform)View.Slots[slot].transform.parent, slot % 2 == 0 ? -105 : 105, slot < 2 ? 70 : -8, 190, 70);
                View.Slots[slot].fontSize = 23;
            }
            Item(View.PrimaryButton, 0, -82, 260, 54, 32);
            Item(View.LeaveButton, 0, -136, 220, 44, 23);

            var map = View.MapPanel.transform;
            Label(map.Find("Title").GetComponent<Text>(), 0, 182, 320, 52, 34);
            bool confirming = Lobby.AwaitingMapConfirmation;
            Label(View.MapHint, 0, confirming ? 10 : 136, confirming ? 400 : 360, confirming ? 200 : 40, confirming ? 34 : 24);
            At(View.MapOptions, 0, -8, 320, 250);
            if (View.MapOptions.TryGetComponent<VerticalLayoutGroup>(out var layout)) layout.spacing = 6;
            foreach (Transform option in View.MapOptions)
            {
                if (option.TryGetComponent<LayoutElement>(out var element)) element.preferredHeight = 44;
                var text = option.GetComponentInChildren<Text>(true); if (text != null) text.fontSize = 28;
            }
            Item(View.MapBack, 0, -176, 220, 46, 24);
        }
        private static void Centre(GameObject group, float width, float height) => At((RectTransform)group.transform, 0, 0, width, height);
        private static void At(RectTransform rect, float x, float y, float width, float height)
        {
            if (rect == null) return;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = new Vector2(x, y); rect.sizeDelta = new Vector2(width, height);
        }
        private static void Label(Text text, float x, float y, float width, float height, int size)
        {
            At(text.rectTransform, x, y, width, height); text.fontSize = size; text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Truncate;
        }
        private static void Item(Button button, float x, float y, float width, float height, int size)
        {
            At((RectTransform)button.transform, x, y, width, height);
            var text = button.GetComponentInChildren<Text>(true); if (text != null) text.fontSize = size;
        }

        // Every text on the water reads as under the water; rows cloned at runtime are picked up on the next pass.
        private float nextSubmerge;
        private static void Submerge(GameObject panel)
        {
            foreach (var text in panel.GetComponentsInChildren<Text>(true))
                if (!text.TryGetComponent<UnderwaterText>(out _)) text.gameObject.AddComponent<UnderwaterText>();
        }

        /// <summary>World radius of the visible water in the bowl opening; text fades out before it.</summary>
        public float WaterRadius => Water[Mode].TryGetComponent<Renderer>(out var surface) ? surface.bounds.extents.x * WaterOpening : .15f;
        // The water disc runs a little under the bowl rim; this is the share of it that is actually visible.
        private const float WaterOpening = 1f;

        // ---- Water ripples: every click on the water surface sends rings out from the click point. ----
        private float rippleKick = -10;
        /// <summary>Spawns ripples where <paramref name="ray"/> meets the visible water panel; false when it misses.</summary>
        public bool RippleAt(Ray ray)
        {
            if (!Focused) return false;
            var panel = View.MapPanel.activeSelf ? View.MapPanel : View.LobbyPanel;
            if (!panel.activeInHierarchy) return false;
            var rect = (RectTransform)panel.transform;
            var plane = new Plane(rect.forward, rect.position);
            if (!plane.Raycast(ray, out var distance)) return false;
            Vector2 local = rect.InverseTransformPoint(ray.GetPoint(distance));
            var fromCentre = local - rect.rect.center;
            float radius = WaterRadius / rect.lossyScale.x, room = radius - fromCentre.magnitude;
            if (room < 20) return false;
            WaterRipple.Spawn(rect, fromCentre, Mathf.Min(300, room * 2));
            UnderwaterText.Kick();
            rippleKick = Time.unscaledTime;
            return true;
        }
        private void Place(GameObject panel)
        {
            panel.transform.SetParent(null, false);
            var canvas = panel.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace; canvas.worldCamera = Camera;
            panel.transform.SetPositionAndRotation(Water[Mode].position + Vector3.up * .11f, Quaternion.Euler(90, 0, 0));
            float ripple = Mathf.Sin(Time.unscaledTime * .9f) * .002f;
            // A click makes the floating text bob and tilt briefly, as if the water under it moved.
            float since = Time.unscaledTime - rippleKick;
            float wobble = since < 1.2f ? Mathf.Exp(-3.5f * since) * Mathf.Sin(since * 16f) : 0;
            panel.transform.position += Vector3.up * (ripple + wobble * .0025f);
            panel.transform.rotation = Quaternion.Euler(90 + wobble * 1.2f, 0, wobble * .8f);
            panel.transform.localScale = Vector3.one * .00052f;
            var group = panel.GetComponent<CanvasGroup>(); if (group != null) group.blocksRaycasts = !Transitioning;
        }
        private void OnDestroy()
        {
            if (View == null) return;
            if (View.LobbyPanel != null && View.LobbyPanel.transform.parent == null) Destroy(View.LobbyPanel);
            if (View.MapPanel != null && View.MapPanel.transform.parent == null) Destroy(View.MapPanel);
        }
    }
}
