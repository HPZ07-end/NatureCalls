using System;
namespace NatureCalls.Core
{
    public enum MessKind : byte { None, Dropping, Puddle, DirtyToilet }
    /// <summary>Time-weighted accuracy; caps the last sample at the actual duration.</summary>
    public sealed class Accuracy
    {
        public float Duration { get; }
        public float Elapsed { get; private set; }
        public float Inside { get; private set; }
        public float Value => Elapsed > 0 ? Inside / Elapsed : 0;
        public bool Complete => Elapsed >= Duration;
        public Accuracy(float duration) { Duration = Math.Max(0, duration); }
        public void Sample(float seconds, bool inside)
        {
            float used = Math.Min(Math.Max(0, seconds), Duration - Elapsed);
            Elapsed += used;
            if (inside) Inside += used;
        }
    }
}
