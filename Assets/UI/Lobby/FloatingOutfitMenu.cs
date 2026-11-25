using System.Linq;
using NatureCalls.Facility;
using NatureCalls.Map;
using NatureCalls.Player;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NatureCalls.UI
{
    /// <summary>Camera-facing world text placed beside the active mirror, with a staggered reveal.</summary>
    [RequireComponent(typeof(Canvas), typeof(CanvasGroup))]
    public sealed class FloatingOutfitMenu : MonoBehaviour
    {
        private UnityEngine.Camera viewer;
        private PlanarMirror mirror;
        private RectTransform rect;
        private CanvasGroup group;
        private CanvasGroup[] lines;
        private float openedAt;

        public void Show(Transform owner, Movement player)
        {
            var world = owner.gameObject.scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Playground>())
                .FirstOrDefault(value => value.OfflinePlayer == player);
            viewer = world != null ? world.Camera.Output : null;
            mirror = owner.gameObject.scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<PlanarMirror>())
                .Where(value => value.ViewerCamera == viewer)
                .OrderBy(value => Vector3.SqrMagnitude(value.transform.position - player.transform.position)).FirstOrDefault();
            rect = (RectTransform)transform;
            // A world-space Canvas must be a root, independent of the Lobby's screen overlay.
            transform.SetParent(null, false);
            SceneManager.MoveGameObjectToScene(gameObject, owner.gameObject.scene);
            var canvas = GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = viewer;
            group = GetComponent<CanvasGroup>();
            lines = GetComponentsInChildren<CanvasGroup>(true).Where(value => value != group && value.gameObject.activeSelf).ToArray();
            openedAt = Time.unscaledTime;
            foreach (var line in lines) line.alpha = 0;
            group.blocksRaycasts = false;
            Position();
        }

        private void LateUpdate()
        {
            if (viewer == null || group == null) return;
            Position();
            float elapsed = Time.unscaledTime - openedAt;
            for (int index = 0; index < lines.Length; index++)
                lines[index].alpha = Mathf.SmoothStep(0, 1, Mathf.Clamp01((elapsed - index * .025f) / .24f));
            group.blocksRaycasts = elapsed > .15f;
        }

        private void Position()
        {
            if (viewer == null) return;
            var screen = viewer.pixelRect;
            float pixelsPerUnit = screen.height / 1080f;
            // Keep text readable on short or narrow windows without introducing a backdrop.
            float fit = Mathf.Min(1, screen.width / (rect.rect.width * pixelsPerUnit * 2.5f));
            pixelsPerUnit *= fit;
            float width = rect.rect.width * pixelsPerUnit;
            float height = rect.rect.height * pixelsPerUnit;
            float gap = 32 * pixelsPerUnit;
            float center = screen.center.y;
            float left = screen.xMin + 32 * pixelsPerUnit;
            if (mirror != null && mirror.Surface != null)
            {
                var surface = mirror.Surface.transform;
                var a = viewer.WorldToScreenPoint(surface.position + surface.right * mirror.Size.x * .5f);
                var b = viewer.WorldToScreenPoint(surface.position - surface.right * mirror.Size.x * .5f);
                center = viewer.WorldToScreenPoint(surface.position).y;
                float before = Mathf.Min(a.x, b.x) - gap - width;
                float after = Mathf.Max(a.x, b.x) + gap;
                left = before >= screen.xMin + 16 ? before : after;
            }
            left = Mathf.Clamp(left, screen.xMin + 16, Mathf.Max(screen.xMin + 16, screen.xMax - width - 16));
            center = Mathf.Clamp(center, screen.yMin + height / 2 + 12, Mathf.Max(screen.yMin + height / 2 + 12, screen.yMax - height / 2 - 12));
            const float depth = .72f;
            transform.SetPositionAndRotation(viewer.ScreenToWorldPoint(new Vector3(left, center + Mathf.Sin(Time.unscaledTime * .9f) * 1.4f * pixelsPerUnit, depth)), viewer.transform.rotation);
            float worldPerPixel = 2 * depth * Mathf.Tan(viewer.fieldOfView * Mathf.Deg2Rad / 2) / screen.height;
            transform.localScale = Vector3.one * (worldPerPixel * pixelsPerUnit);
        }
    }
}
