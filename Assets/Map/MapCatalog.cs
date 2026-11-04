using System;
using UnityEngine;

namespace NatureCalls.Map
{
    /// <summary>
    /// Maps the host can choose after a game starts. Each entry is a scene with a <see cref="Playground"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "NatureCalls/Map Catalog", fileName = "MapCatalog")]
    public sealed class MapCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            public string DisplayName;
            public string ScenePath;
            [Min(1)] public float RoundSeconds = Core.RoundClock.DefaultSeconds;
        }

        public const string RandomName = "随机地图";

        [SerializeField] private Entry[] maps = Array.Empty<Entry>();

        public int Count => maps.Length;
        public Entry this[int index] => maps[index];

        public void SetMaps(Entry[] value) => maps = value ?? Array.Empty<Entry>();
    }
}
