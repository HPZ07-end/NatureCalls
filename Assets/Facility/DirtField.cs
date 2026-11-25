using System.Collections.Generic;
using UnityEngine;

namespace NatureCalls.Facility
{
    /// <summary>Anything that dirties the room (GLOSSARY: Tidiness/DirtField): messes, vomit, backflowed or broken fixtures.</summary>
    public interface IDirtSource
    {
        Vector3 DirtPosition { get; }
        /// <summary>Current weight; 0 when clean. Saboteur droppings use a reduced weight.</summary>
        float DirtWeight { get; }
    }

    /// <summary>
    /// Shared contract: local dirt at a point and the room total. Sources register themselves (OnEnable/OnDisable).
    /// The kernel and normalisation constants live in Core rules owned by the match work.
    /// </summary>
    public static class DirtField
    {
        private static readonly List<IDirtSource> sources = new();
        public static IReadOnlyList<IDirtSource> Sources => sources;
        public static float Radius = 3f;
        public static void Register(IDirtSource source) { if (source != null && !sources.Contains(source)) sources.Add(source); }
        public static void Unregister(IDirtSource source) => sources.Remove(source);
        public static float Local(Vector3 position)
        {
            float sum = 0;
            foreach (var source in sources)
            {
                var weight = source.DirtWeight; if (weight <= 0) continue;
                var d = Vector3.Distance(position, source.DirtPosition);
                if (d < Radius) sum += weight * (1f - d / Radius);
            }
            return sum;
        }
        public static float Total() { float sum = 0; foreach (var source in sources) sum += Mathf.Max(0, source.DirtWeight); return sum; }
    }
}
