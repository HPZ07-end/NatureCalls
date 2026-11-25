using NatureCalls.Facility;
using NatureCalls.Player;
using UnityEngine;

namespace NatureCalls.Map
{
    /// <summary>Enables only the PlanarMirror nearest to the local player.</summary>
    public sealed class NearestMirrorSelector : MonoBehaviour
    {
        public float UpdateSeconds = .25f;
        private float nextUpdate;
        private void Update()
        {
            if (Time.unscaledTime < nextUpdate) return;
            nextUpdate = Time.unscaledTime + UpdateSeconds;
            var player = FindFirstObjectByType<Movement>();
            if (player == null) return;
            var mirrors = GetComponentsInChildren<PlanarMirror>(true);
            PlanarMirror nearest = null; var distance = float.PositiveInfinity;
            foreach (var mirror in mirrors)
            {
                var d = (mirror.transform.position - player.transform.position).sqrMagnitude;
                if (d < distance) { distance = d; nearest = mirror; }
            }
            foreach (var mirror in mirrors) mirror.enabled = mirror == nearest;
        }
    }
}
