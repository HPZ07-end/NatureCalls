using UnityEngine;
using UnityEngine.Rendering;

namespace NatureCalls.UI
{
    /// <summary>
    /// Blurs and dims the DressingRoom behind the Lobby UI: a URP depth-of-field Volume plus a dim image.
    /// </summary>
    public sealed class BackgroundBlur : MonoBehaviour
    {
        [SerializeField] private Volume volume;
        [SerializeField] private GameObject dim;

        public bool Active { get; private set; }

        private void Awake() => Set(false);

        public void Set(bool active)
        {
            Active = active;
            if (volume != null) volume.weight = active ? 1 : 0;
            if (dim != null) dim.SetActive(active);
        }
    }
}
