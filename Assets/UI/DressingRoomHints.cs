using NatureCalls.Map;
using UnityEngine;
using UnityEngine.UI;
using PlayerInput = NatureCalls.Input.PlayerInput;

namespace NatureCalls.UI
{
    public sealed class DressingRoomHints : MonoBehaviour
    {
        public Playground World;
        public LobbyController Lobby;
        public Text Label;
        private void LateUpdate()
        {
            var player = World != null ? World.OfflinePlayer : null;
            Label.enabled = player != null && (Lobby == null || (!Lobby.IsOutfitPanelOpen && !Lobby.IsLobbyOpen));
            if (!Label.enabled) return;
            var toilets = World.GetComponentInChildren<ToiletLobby>();
            if (toilets == null) toilets = FindFirstObjectByType<ToiletLobby>();
            if (toilets != null && toilets.Transitioning) { Label.enabled = false; return; }
            var input = player.GetComponent<PlayerInput>();
            input.RefreshFocus();
            var focus = input.FocusPrompt;
            var stool = DressingRoomStool.CarriedBy(player);
            var prompt = stool == null ? focus : "F  放下凳子";
            Label.text = string.IsNullOrEmpty(prompt)
                ? toilets != null && toilets.Inside ? "近距离看向马桶，选择联机方式 · 返回门按 E" : "WASD 移动 · 鼠标观察 · 对准物体按 E\n镜前换装 · E 搬起凳子 / F 放下 · Space 跳上凳子"
                : prompt;
        }
    }
}
