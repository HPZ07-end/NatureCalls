using UnityEngine;
using NatureCalls.Player;

namespace NatureCalls.Facility
{
    /// <summary>Shows whether a player is inside this toilet's stall.</summary>
    public sealed class ToiletOccupancySign : MonoBehaviour
    {
        public Toilet Toilet;
        public Renderer Indicator;
        public Renderer OutsideIndicator;
        public Color AvailableColor = new(.12f, .82f, .24f);
        public Color OccupiedColor = new(.92f, .08f, .06f);

        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
        private MaterialPropertyBlock block;
        private bool lastOccupied;
        private bool initialized;

        private void OnEnable()
        {
            initialized = false;
            Refresh();
        }

        private void Update() => Refresh();

        private void Refresh()
        {
            if (Toilet == null || (Indicator == null && OutsideIndicator == null)) return;
            var stall = Toilet.GetComponentInParent<StallDoor>();
            if (stall == null) return;
            var occupied = false;
            foreach (var player in FindObjectsByType<Movement>(FindObjectsSortMode.None))
                if (stall.Contains(player)) { occupied = true; break; }
            if (initialized && occupied == lastOccupied) return;
            initialized = true;
            lastOccupied = occupied;

            var color = occupied ? OccupiedColor : AvailableColor;
            block ??= new MaterialPropertyBlock();
            if (Indicator != null)
            {
                Indicator.GetPropertyBlock(block);
                block.SetColor(BaseColor, color);
                block.SetColor(ColorId, color);
                block.SetColor(EmissionColor, color * 1.6f);
                Indicator.SetPropertyBlock(block);
            }
            if (OutsideIndicator != null)
            {
                OutsideIndicator.GetPropertyBlock(block);
                block.SetColor(BaseColor, color);
                block.SetColor(ColorId, color);
                block.SetColor(EmissionColor, color * 1.6f);
                OutsideIndicator.SetPropertyBlock(block);
            }
        }
    }
}
