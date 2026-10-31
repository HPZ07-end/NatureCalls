using UnityEngine;
using NatureCalls.Network;

namespace NatureCalls.UI
{
    public sealed class Panel : MonoBehaviour
    {
        public Map.Playground World;
        public Session Session;
        private string code = "";
        private string address = "127.0.0.1";
        private bool localTests;
        private Vector2 scroll;
        private void OnGUI()
        {
            if (Cursor.lockState == CursorLockMode.Locked)
            {
                GUI.Label(new Rect(16, 16, 700, 30), "WASD / mouse | C posture | E toilet | Esc menu");
                return;
            }
            GUILayout.BeginArea(new Rect(16, 16, 330, Mathf.Min(680, Screen.height - 32)), "NatureCalls / M1", GUI.skin.window);
            scroll = GUILayout.BeginScrollView(scroll);
            GUILayout.Label("Click the world to capture mouse.\nWASD move / mouse look\nC stand or squat / E use nearby toilet\nEsc unlocks mouse and opens this menu.");
            GUILayout.Space(8);
            GUI.enabled = !Session.Busy && !Session.Active;
            if (GUILayout.Button("Offline practice", GUILayout.Height(32))) World.Practice();
            GUILayout.Space(8);
            GUILayout.Label("Online / Unity Relay");
            if (GUILayout.Button("Create Host Session", GUILayout.Height(32))) _ = Session.ConnectRelayAsync(true);
            GUILayout.Label("Session join code");
            code = GUILayout.TextField(code, 12);
            if (GUILayout.Button("Join Session", GUILayout.Height(32))) _ = Session.ConnectRelayAsync(false, code);
            GUI.enabled = true;
            if (!string.IsNullOrEmpty(Session.JoinCode))
            {
                GUILayout.Label("Share code: " + Session.JoinCode);
                if (GUILayout.Button("Copy code")) GUIUtility.systemCopyBuffer = Session.JoinCode;
            }
            GUI.enabled = !Session.Busy && Session.Active;
            if (GUILayout.Button("Leave", GUILayout.Height(30))) _ = Session.LeaveAsync();
            GUI.enabled = true;
            GUILayout.Label("Status: " + Session.Status);
            localTests = GUILayout.Toggle(localTests, "Local / LAN test tools (no Relay)");
            if (localTests)
            {
                GUI.enabled = !Session.Busy && !Session.Active;
                GUILayout.Label("Host address (same Mac: 127.0.0.1)");
                address = GUILayout.TextField(address, 80);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Local Host", GUILayout.Height(30))) Session.StartDirect(true);
                if (GUILayout.Button("Local Join", GUILayout.Height(30))) Session.StartDirect(false, address);
                GUILayout.EndHorizontal();
                GUI.enabled = true;
                GUILayout.Label("Port " + Session.Port + ". Direct test does not use Cloud or Session codes.");
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }
    }
}
