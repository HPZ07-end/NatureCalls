using System.Collections.Generic;
using NatureCalls.Core;
using NatureCalls.Facility;
using NatureCalls.Map;
using UnityEngine;
using UnityEngine.UI;
using PlayerInput = NatureCalls.Input.PlayerInput;

namespace NatureCalls.UI
{
    /// <summary>
    /// Map HUD for toileting and cleaning: the stage prompt, a progress bar, live accuracy, the target circle and
    /// landing point drawn over the world, the unwiped warning and the held item. Reads the local player's
    /// <see cref="PlayerInput"/> only; layout lives in MatchHud.prefab so art can restyle it.
    /// </summary>
    public sealed class ToiletingHud : MonoBehaviour
    {
        [SerializeField] private GameObject statusPanel;
        [SerializeField] private Text phaseLabel;
        [SerializeField] private Image progressFill;
        [SerializeField] private Text accuracyLabel;
        [SerializeField] private GameObject unwipedWarning;
        [SerializeField] private Text itemLabel;
        [SerializeField] private RectTransform overlay;
        [SerializeField] private Image ringDot;
        [SerializeField] private Image landingMarker;

        private const int RingDots = 40;
        private static readonly Color Good = new(.35f, .85f, .4f), Fair = new(.95f, .78f, .2f), Bad = new(.95f, .3f, .25f);
        private readonly List<Image> dots = new();
        private Playground world;

        private void Awake()
        {
            ringDot.gameObject.SetActive(false);
            for (int index = 0; index < RingDots; index++)
            {
                var dot = Instantiate(ringDot, overlay);
                dot.gameObject.SetActive(true);
                dots.Add(dot);
            }
        }

        private void Update()
        {
            if (world == null) world = FindFirstObjectByType<Playground>();
            var target = world != null && world.Camera != null ? world.Camera.Target : null;
            var input = target != null ? target.GetComponent<PlayerInput>() : null;
            if (input == null) { Hide(); return; }

            var phase = input.Phase;
            statusPanel.SetActive(phase != PlayerInput.Stage.None);
            if (phase != PlayerInput.Stage.None)
            {
                phaseLabel.text = Prompt(phase);
                bool timed = phase != PlayerInput.Stage.Aligning && input.Duration > 0;
                progressFill.transform.parent.gameObject.SetActive(timed);
                progressFill.fillAmount = phase == PlayerInput.Stage.Cleaning ? input.CleaningProgress
                    : timed ? Mathf.Clamp01(input.Elapsed / input.Duration) : 0;
                bool scored = phase == PlayerInput.Stage.Holding || phase == PlayerInput.Stage.Urinating;
                accuracyLabel.gameObject.SetActive(scored);
                if (scored)
                {
                    accuracyLabel.text = $"准度 {Mathf.RoundToInt(input.CurrentAccuracy * 100)}%";
                    accuracyLabel.color = AccuracyColor(phase, input.CurrentAccuracy);
                }
            }

            unwipedWarning.SetActive(input.Unwiped);
            var item = input.HeldItem;
            itemLabel.gameObject.SetActive(item != null);
            if (item is PaperRoll) itemLabel.text = $"手持：卷纸（还能擦 {input.PaperRemainingUses} 次）";
            else if (item is Mop) itemLabel.text = "手持：拖把（对着污物长按 E 清理）";
            else if (item != null) itemLabel.text = "手持：" + item.name;

            DrawTarget(input, phase);
        }

        private static string Prompt(PlayerInput.Stage phase) => phase switch
        {
            PlayerInput.Stage.Aligning => "对位：移动让落点进入圆圈，按 C 蹲下",
            PlayerInput.Stage.Holding => "保持：用 WASD 把落点留在圆圈里",
            PlayerInput.Stage.Urinating => "小便：用鼠标把落点留在圆圈里",
            PlayerInput.Stage.Cleaning => "清理中：按住 E",
            _ => ""
        };

        // Green / yellow / red against the thresholds that decide the outcome.
        private static Color AccuracyColor(PlayerInput.Stage phase, float accuracy)
        {
            if (phase == PlayerInput.Stage.Urinating) return accuracy >= NeedsRules.UrinalAccuracy ? Good : Bad;
            return accuracy >= NeedsRules.PerfectAccuracy ? Good : accuracy >= NeedsRules.FloorAccuracy ? Fair : Bad;
        }

        private void DrawTarget(PlayerInput input, PlayerInput.Stage phase)
        {
            bool aiming = phase == PlayerInput.Stage.Aligning || phase == PlayerInput.Stage.Holding || phase == PlayerInput.Stage.Urinating;
            var camera = world.Camera.Output;
            overlay.gameObject.SetActive(aiming && camera != null);
            if (!aiming || camera == null) return;

            var (axisX, axisY) = input.TargetAxes;
            var center = input.TargetCenter;
            for (int index = 0; index < dots.Count; index++)
            {
                float angle = index * Mathf.PI * 2 / dots.Count;
                var point = center + (axisX * Mathf.Cos(angle) + axisY * Mathf.Sin(angle)) * input.TargetRadius;
                Place(dots[index].rectTransform, camera, point);
            }
            Place(landingMarker.rectTransform, camera, input.CurrentLanding);
            bool inside = Vector3.Distance(input.CurrentLanding, center) <= input.TargetRadius;
            landingMarker.color = inside ? Good : Bad;
            foreach (var dot in dots) dot.color = inside ? Good : Color.white;
        }

        private void Place(RectTransform element, UnityEngine.Camera camera, Vector3 world)
        {
            var screen = camera.WorldToScreenPoint(world);
            element.gameObject.SetActive(screen.z > 0);
            if (screen.z > 0 && RectTransformUtility.ScreenPointToLocalPointInRectangle(overlay, screen, null, out var local))
                element.anchoredPosition = local;
        }

        private void Hide()
        {
            statusPanel.SetActive(false);
            unwipedWarning.SetActive(false);
            itemLabel.gameObject.SetActive(false);
            overlay.gameObject.SetActive(false);
        }
    }
}
