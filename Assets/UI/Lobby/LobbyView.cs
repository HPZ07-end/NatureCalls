using UnityEngine;
using UnityEngine.UI;

namespace NatureCalls.UI
{
    /// <summary>
    /// Pure view of the DressingRoom overlay: references only, no behaviour.
    /// Restyle by editing LobbyCanvas.prefab; keep these references assigned. Option templates are cloned at runtime.
    /// </summary>
    public sealed class LobbyView : MonoBehaviour
    {
        [Header("Prompts")]
        public GameObject MirrorPrompt;
        public GameObject DoorPrompt;

        [Header("Outfit (mirror)")]
        public GameObject OutfitPanel;
        public RectTransform OutfitOptions;
        public Button OutfitOptionTemplate;
        public Text OutfitCurrent;
        public Button OutfitConfirm;
        public Button OutfitCancel;

        [Header("Lobby")]
        public GameObject LobbyPanel;
        public BackgroundBlur Blur;
        public Text Status;
        public GameObject EntryGroup;
        public Button OfflineButton;
        public Button CreateButton;
        public InputField CodeInput;
        public Button JoinButton;
        public Button CloseButton;
        public GameObject RoomGroup;
        public Text RoomCode;
        public Text[] Slots = new Text[4];
        public Button PrimaryButton;
        public Text PrimaryLabel;
        public Button LeaveButton;

        [Header("Map selection")]
        public GameObject MapPanel;
        public RectTransform MapOptions;
        public Button MapOptionTemplate;
        public Text MapHint;
        public Button MapBack;
    }
}
