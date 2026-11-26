using NatureCalls.Map;
using NatureCalls.Player;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using PlayerInput = NatureCalls.Input.PlayerInput;

namespace NatureCalls.UI
{
    /// <summary>
    /// Minecraft-style crosshair: a small white plus at the screen centre, shown only in standing first person while
    /// the mouse is captured. It turns warm yellow when the view ray finds something E can use within reach
    /// (<see cref="PlayerInput.Focus"/>). Hidden while squatting, aiming, toileting, traversing or in menus — the
    /// toileting aim is done by feel. Builds itself on every scene with a <see cref="Playground"/>.
    /// </summary>
    public sealed class Crosshair : MonoBehaviour
    {
        public static readonly Color Idle = new(1f, 1f, 1f, .9f);
        public static readonly Color Ready = new(1f, .86f, .3f, 1f);
        private static Crosshair instance;
        private Playground world;
        private Canvas canvas;
        private RectTransform plus;
        private Image[] bars;

        /// <summary>Whether the crosshair is drawn this frame (for tests).</summary>
        public bool Visible => canvas != null && canvas.enabled;
        /// <summary>Whether the crosshair is highlighting a usable target (for tests).</summary>
        public bool OnTarget { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!Application.isEditor && Application.isBatchMode) return; // dedicated server
            if (instance != null || FindFirstObjectByType<Playground>() == null) return;
            var go = new GameObject("Crosshair");
            SceneManager.MoveGameObjectToScene(go, scene);
            instance = go.AddComponent<Crosshair>();
        }

        private void Awake()
        {
            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 45;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = .5f;
            plus = new GameObject("Plus", typeof(RectTransform)).GetComponent<RectTransform>();
            plus.SetParent(transform, false);
            plus.anchorMin = plus.anchorMax = plus.pivot = new Vector2(.5f, .5f);
            plus.sizeDelta = new Vector2(30, 30);
            bars = new[] { Bar("Horizontal", new Vector2(30, 4)), Bar("Vertical", new Vector2(4, 30)) };
            canvas.enabled = false;
        }

        private Image Bar(string name, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(plus, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = size;
            var image = rect.gameObject.AddComponent<Image>();
            image.color = Idle; image.raycastTarget = false;
            var outline = rect.gameObject.AddComponent<Outline>(); // keeps the plus readable on white tiles
            outline.effectColor = new Color(0, 0, 0, .55f); outline.effectDistance = new Vector2(1.5f, -1.5f);
            return image;
        }

        private void OnDestroy() { if (instance == this) instance = null; }

        private void LateUpdate()
        {
            if (world == null) world = FindFirstObjectByType<Playground>();
            var player = world != null && world.Camera != null ? world.Camera.Target : null;
            var input = player != null ? player.GetComponent<PlayerInput>() : null;
            var posture = player != null ? player.GetComponent<Posture>() : null;
            bool urineAim = input != null && input.Phase == PlayerInput.Stage.Urinating; // the stream goes where you look
            bool show = input != null && player.Controlled && !player.IsTraversing && (input.Captured || !input.CaptureMouse) &&
                        (urineAim || (!player.Aiming && player.Seat == null && !input.IsToileting &&
                                      (posture == null || posture.State == PostureState.Standing)));
            canvas.enabled = show;
            if (!show) { OnTarget = false; return; }
            OnTarget = input.Focus != null;
            var color = OnTarget ? Ready : Idle;
            foreach (var bar in bars) bar.color = color;
            plus.localScale = Vector3.one * Mathf.Lerp(plus.localScale.x, OnTarget ? 1.15f : 1f, 1 - Mathf.Exp(-Time.unscaledDeltaTime * 18));
        }
    }
}
