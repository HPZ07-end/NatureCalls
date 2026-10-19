using UnityEngine;

namespace NatureCalls.UI
{
    public sealed class Panel : MonoBehaviour
    {
        public Map.Playground World;
        private void OnGUI()
        {
            if (Cursor.lockState == CursorLockMode.Locked)
            {
                GUI.Label(new Rect(16, 16, 500, 30), "WASD / mouse | C posture | E toilet | Esc menu");
                return;
            }
            GUILayout.BeginArea(new Rect(16, 16, 330, 230), "NatureCalls / M1", GUI.skin.window);
            GUILayout.Label("Click the world to capture mouse.\nWASD move / mouse look\nC stand or squat / E use nearby toilet\nEsc unlocks mouse and opens this menu.");
            if (GUILayout.Button("Restart offline practice", GUILayout.Height(36))) World.Practice();
            GUILayout.Label("Walk through the entrance toward the toilet, then press E.\nThe brown object is a visibility placeholder.");
            GUILayout.EndArea();
        }
    }
}
