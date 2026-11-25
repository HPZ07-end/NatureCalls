using UnityEngine;

namespace NatureCalls.Map
{
    public sealed class LightFlicker : MonoBehaviour
    {
        public Light Target;
        public float Frequency = 7f;
        public float Minimum = .35f;
        private float baseIntensity;
        private void Awake() { if (Target == null) Target = GetComponentInChildren<Light>(); if (Target != null) baseIntensity = Target.intensity; }
        private void Update() { if (Target != null) Target.intensity = baseIntensity * Mathf.Lerp(Minimum, 1f, Mathf.PerlinNoise(Time.time * Frequency, transform.position.x)); }
    }
}
