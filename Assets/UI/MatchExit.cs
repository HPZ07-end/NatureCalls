using System.Threading.Tasks;
using NatureCalls.Map;
using NatureCalls.Network;
using UnityEngine;
using UnityEngine.UI;

namespace NatureCalls.UI
{
    /// <summary>
    /// Top-left exit on a map: leaves the room when connected, then returns to the DressingRoom.
    /// Layout lives in MatchHud.prefab; press Esc to free the mouse before clicking.
    /// </summary>
    public sealed class MatchExit : MonoBehaviour
    {
        [SerializeField] private Button button;
        private bool leaving;

        private void Awake() => button.onClick.AddListener(Exit);

        public void Exit()
        {
            if (!leaving) _ = ExitAsync();
        }

        private async Task ExitAsync()
        {
            leaving = true;
            button.interactable = false;
            var session = Session.Instance;
            if (session != null && session.Active) await session.LeaveAsync();
            Match.ReturnToDressingRoom();
        }
    }
}
