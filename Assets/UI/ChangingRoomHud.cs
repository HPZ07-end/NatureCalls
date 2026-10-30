using System;
using NatureCalls.Player;
using UnityEngine;

namespace NatureCalls.UI
{
    public sealed class ChangingRoomHud : MonoBehaviour
    {
        private bool canInteract;
        private PlayerOutfitController outfitController;
        private Action confirmAction;
        private Action cancelAction;

        public void ShowPrompt(bool visible)
        {
            canInteract = visible;
        }

        public void Open(PlayerOutfitController controller, Action onConfirm, Action onCancel)
        {
            outfitController = controller;
            confirmAction = onConfirm;
            cancelAction = onCancel;
            canInteract = false;
        }

        public void Close()
        {
            outfitController = null;
            confirmAction = null;
            cancelAction = null;
        }

        private void Update()
        {
            if (outfitController == null)
                return;
            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A))
                outfitController.PreviewNext(-1);
            if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D))
                outfitController.PreviewNext(1);
            if (Input.GetKeyDown(KeyCode.Return))
                confirmAction?.Invoke();
            if (Input.GetKeyDown(KeyCode.Escape))
                cancelAction?.Invoke();
        }

        private void OnGUI()
        {
            var titleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 28,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
            var textStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 18,
                normal = { textColor = Color.white }
            };

            if (canInteract && outfitController == null)
            {
                var prompt = new Rect(Screen.width * 0.5f - 190f, Screen.height - 105f, 380f, 55f);
                GUI.Box(prompt, string.Empty);
                GUI.Label(prompt, "Press E to enter the changing room", textStyle);
            }

            if (outfitController == null)
                return;

            var panel = new Rect(Screen.width * 0.5f - 260f, Screen.height - 230f, 520f, 190f);
            GUI.Box(panel, string.Empty);
            GUI.Label(new Rect(panel.x, panel.y + 12f, panel.width, 42f), "CHANGING ROOM", titleStyle);
            GUI.Label(new Rect(panel.x, panel.y + 58f, panel.width, 36f), outfitController.PreviewName, textStyle);
            if (GUI.Button(new Rect(panel.x + 35f, panel.y + 105f, 100f, 42f), "< Previous"))
                outfitController.PreviewNext(-1);
            if (GUI.Button(new Rect(panel.x + 150f, panel.y + 105f, 100f, 42f), "Next >"))
                outfitController.PreviewNext(1);
            if (GUI.Button(new Rect(panel.x + 270f, panel.y + 105f, 100f, 42f), "Confirm"))
                confirmAction?.Invoke();
            if (GUI.Button(new Rect(panel.x + 385f, panel.y + 105f, 100f, 42f), "Cancel"))
                cancelAction?.Invoke();
            GUI.Label(new Rect(panel.x, panel.y + 151f, panel.width, 30f), "A/D or arrows to browse   Enter confirm   Esc cancel", textStyle);
        }
    }
}
