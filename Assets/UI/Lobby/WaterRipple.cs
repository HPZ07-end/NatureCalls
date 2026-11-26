using UnityEngine;
using UnityEngine.UI;

namespace NatureCalls.UI
{
    /// <summary>
    /// One expanding ring on the toilet-lobby water surface. Drawn as a UI Image behind the floating text, so it needs
    /// no extra shader or asset: the ring sprite is generated once at runtime.
    /// </summary>
    public sealed class WaterRipple : MonoBehaviour
    {
        public const float Duration = 1.1f;
        private const float StartSize = 24, EndSize = 300;
        private static Sprite ring;

        private Image image;
        private float age, delay, strength;

        /// <summary>Sends three staggered rings out from <paramref name="local"/> (panel units) on <paramref name="panel"/>.</summary>
        public static void Spawn(RectTransform panel, Vector2 local)
        {
            for (int index = 0; index < 3; index++) Create(panel, local, index * .14f, .65f - index * .17f);
        }

        private static void Create(RectTransform panel, Vector2 local, float delay, float strength)
        {
            var objectValue = new GameObject("Ripple", typeof(RectTransform), typeof(Image), typeof(WaterRipple));
            var rect = (RectTransform)objectValue.transform;
            rect.SetParent(panel, false);
            rect.SetAsFirstSibling();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = local;
            rect.sizeDelta = Vector2.one * StartSize;
            var ripple = objectValue.GetComponent<WaterRipple>();
            ripple.delay = delay; ripple.strength = strength;
            ripple.image = objectValue.GetComponent<Image>();
            ripple.image.sprite = Ring;
            ripple.image.raycastTarget = false;
            ripple.Apply(0);
        }

        private void Update()
        {
            age += Time.unscaledDeltaTime;
            float t = (age - delay) / Duration;
            if (t >= 1) { Destroy(gameObject); return; }
            Apply(Mathf.Max(0, t));
        }

        private void Apply(float t)
        {
            float eased = 1 - (1 - t) * (1 - t);
            ((RectTransform)transform).sizeDelta = Vector2.one * Mathf.Lerp(StartSize, EndSize, eased);
            float alpha = age < delay ? 0 : strength * Mathf.Pow(1 - t, 1.6f);
            image.color = new Color(.82f, .95f, 1f, alpha);
        }

        private static Sprite Ring
        {
            get
            {
                if (ring != null) return ring;
                const int size = 128;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "WaterRippleRing" };
                var pixels = new Color32[size * size];
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + .5f) / size * 2 - 1, dy = (y + .5f) / size * 2 - 1;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    // A bright crest with a faint trailing trough, fading to nothing at the edge.
                    float crest = Mathf.Exp(-Mathf.Pow((r - .84f) / .07f, 2));
                    float trough = .3f * Mathf.Exp(-Mathf.Pow((r - .68f) / .06f, 2));
                    float a = r > 1 ? 0 : Mathf.Clamp01(crest + trough);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
                texture.SetPixels32(pixels); texture.Apply(false, true);
                ring = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f), 100);
                return ring;
            }
        }
    }
}
