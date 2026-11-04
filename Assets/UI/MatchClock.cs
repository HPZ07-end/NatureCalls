using NatureCalls.Core;
using NatureCalls.Map;
using NatureCalls.Network;
using UnityEngine;
using UnityEngine.UI;

namespace NatureCalls.UI
{
    /// <summary>
    /// Shows the time left in the round on the map HUD. A networked round is timed by the host's
    /// <see cref="LobbyRoom"/>; an offline round by <see cref="Match"/>. Hidden when a map is opened directly.
    /// </summary>
    public sealed class MatchClock : MonoBehaviour
    {
        [SerializeField] private Text label;

        public static double Remaining => Match.Mode == MatchMode.Networked
            ? LobbyRoom.Current != null ? LobbyRoom.Current.RoundRemaining : -1
            : Match.OfflineRemaining;

        private void Update()
        {
            var remaining = Remaining;
            label.enabled = remaining >= 0;
            if (remaining >= 0) label.text = "剩余时间  " + RoundClock.Format(remaining);
        }
    }
}
