using System;
namespace NatureCalls.Core
{
    /// <summary>Dropping is the soft stool heap; LooseStool is the slippery splat (GLOSSARY Mess/StoolKind). Hard stools are Items, not list entries.</summary>
    public enum MessKind : byte { None, Dropping, Puddle, DirtyToilet, Backflow, Accident, Vomit, LooseStool }
    /// <summary>Time-weighted accuracy; caps the last sample at the actual duration.</summary>
    public sealed class Accuracy
    {
        public float Duration { get; }
        public float Elapsed { get; private set; }
        public float Inside { get; private set; }
        public float Value => Elapsed > 0 ? Inside / Elapsed : 0;
        public bool Complete => Elapsed >= Duration;
        public Accuracy(float duration) { Duration = Math.Max(0, duration); }
        /// <summary>A result decided in one moment (the squat release): value 0–1, already complete.</summary>
        public static Accuracy Fixed(float value) { var result = new Accuracy(1); result.Elapsed = 1; result.Inside = Math.Clamp(value, 0, 1); return result; }
        public void Sample(float seconds, bool inside)
        {
            float used = Math.Min(Math.Max(0, seconds), Duration - Elapsed);
            Elapsed += used;
            if (inside) Inside += used;
        }
    }
}
