using System.Threading.Tasks;
using UnityEngine;
using NatureCalls.Map;
using NatureCalls.Network;

namespace NatureCalls.UI
{
    /// <summary>
    /// In-map Esc menu. A game started from the Lobby only offers status and returning to the DressingRoom;
    /// a map opened directly keeps the development connection tools.
    /// </summary>
    public sealed class Panel : MonoBehaviour
    {
        public Map.Playground World;
        public Session Session;
        private string code = "";
        private string address = "127.0.0.1";
        private bool localTests;
        private bool returning;
        private Vector2 scroll;
        private void OnGUI()
        {
            var session = Session != null ? Session : Session.Instance;
            if (Cursor.lockState == CursorLockMode.Locked)
            {
                GUI.Label(new Rect(16, 16, 700, 30), "WASD / mouse | Space jump | C posture | E interact | R throw | Esc menu");
                return;
            }
            GUILayout.BeginArea(new Rect(16, 16, 330, Mathf.Min(680, Screen.height - 32)), "NatureCalls", GUI.skin.window);
            scroll = GUILayout.BeginScrollView(scroll);
            GUILayout.Label("Click the world to capture mouse.\nWASD move / mouse look / Space jump\nC stand or squat / E interact / R throw\nEsc unlocks mouse and opens this menu.");
            GUILayout.Space(8);
            if (Match.Mode != MatchMode.None) MatchMenu(session);
            else if (session != null) DevelopmentMenu(session);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }
        private void MatchMenu(Session session)
        {
            GUILayout.Label(Match.Mode == MatchMode.Offline ? "单人离线测试" : "联机对局");
            if (session != null && session.Active) GUILayout.Label("Status: " + session.Status);
            GUI.enabled = !returning;
            if (GUILayout.Button("返回准备房", GUILayout.Height(32))) _ = ReturnAsync(session);
            GUI.enabled = true;
        }
        private async Task ReturnAsync(Session session)
        {
            returning = true;
            if (session != null && session.Active) await session.LeaveAsync();
            Match.ReturnToDressingRoom();
        }
        private void DevelopmentMenu(Session session)
        {
            GUI.enabled = !session.Busy && !session.Active;
            if (GUILayout.Button("Offline practice", GUILayout.Height(32))) World.Practice();
            GUILayout.Space(8);
            GUILayout.Label("Online / Unity Relay");
            if (GUILayout.Button("Create Host Session", GUILayout.Height(32))) _ = session.ConnectRelayAsync(true);
            GUILayout.Label("Session join code");
            code = GUILayout.TextField(code, 12);
            if (GUILayout.Button("Join Session", GUILayout.Height(32))) _ = session.ConnectRelayAsync(false, code);
            GUI.enabled = true;
            if (!string.IsNullOrEmpty(session.JoinCode))
            {
                GUILayout.Label("Share code: " + session.JoinCode);
                if (GUILayout.Button("Copy code")) GUIUtility.systemCopyBuffer = session.JoinCode;
            }
            GUI.enabled = !session.Busy && session.Active;
            if (GUILayout.Button("Leave", GUILayout.Height(30))) _ = session.LeaveAsync();
            GUI.enabled = true;
            GUILayout.Label("Status: " + session.Status);
            localTests = GUILayout.Toggle(localTests, "Local / LAN test tools (no Relay)");
            if (localTests)
            {
                GUI.enabled = !session.Busy && !session.Active;
                GUILayout.Label("Host address (same Mac: 127.0.0.1)");
                address = GUILayout.TextField(address, 80);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Local Host", GUILayout.Height(30))) session.StartDirect(true);
                if (GUILayout.Button("Local Join", GUILayout.Height(30))) session.StartDirect(false, address);
                GUILayout.EndHorizontal();
                GUI.enabled = true;
                GUILayout.Label("Port " + session.Port + ". Direct test does not use Cloud or Session codes.");
            }
        }
    }
}
