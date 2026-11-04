using NatureCalls.Core;
using NatureCalls.Map;
using NatureCalls.Player;
using UnityEngine;
using UnityEngine.UI;

namespace NatureCalls.UI
{
    /// <summary>Map HUD bars for the local player's Urgency and Disgust (layout in MatchHud.prefab).</summary>
    public sealed class NeedsView : MonoBehaviour
    {
        [SerializeField] private Text urgencyLabel;
        [SerializeField] private Image urgencyFill;
        [SerializeField] private Text disgustLabel;
        [SerializeField] private Image disgustFill;
        private Playground world;

        private void Update()
        {
            if (world == null) world = FindFirstObjectByType<Playground>();
            var target = world != null && world.Camera != null ? world.Camera.Target : null;
            var needs = target != null ? target.GetComponent<PlayerNeeds>() : null;
            var urgency = needs != null ? needs.Urgency : 0;
            var disgust = needs != null ? needs.Disgust : 0;
            urgencyFill.fillAmount = urgency / NeedsRules.UrgencyMax;
            disgustFill.fillAmount = disgust / NeedsRules.DisgustThreshold;
            urgencyLabel.text = $"急迫值  {Mathf.FloorToInt(urgency)}/{NeedsRules.UrgencyMax:0}";
            disgustLabel.text = $"恶心值  {Mathf.FloorToInt(disgust)}/{NeedsRules.DisgustThreshold:0}";
        }
    }
}
