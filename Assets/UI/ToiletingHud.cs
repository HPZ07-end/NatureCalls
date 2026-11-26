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
            var focusPrompt = input.FocusPrompt;
            var actionPrompt = input.FPrompt;
            statusPanel.SetActive(phase != PlayerInput.Stage.None || focusPrompt.Length > 0 || actionPrompt.Length > 0);
            if (phase == PlayerInput.Stage.None && (focusPrompt.Length > 0 || actionPrompt.Length > 0))
            {
                phaseLabel.text = focusPrompt.Length > 0 ? focusPrompt : actionPrompt;
                progressFill.transform.parent.gameObject.SetActive(false);
                accuracyLabel.gameObject.SetActive(false);
            }
            if (phase != PlayerInput.Stage.None)
            {
                phaseLabel.text = phase == PlayerInput.Stage.Urinating && input.UrineFlowing ? "尿中……（要尿完）" : Prompt(phase);
                bool timed = phase == PlayerInput.Stage.Urinating && input.Duration > 0;
                progressFill.transform.parent.gameObject.SetActive(timed);
                progressFill.fillAmount = phase == PlayerInput.Stage.Cleaning ? input.CleaningProgress
                    : timed ? Mathf.Clamp01(input.Elapsed / input.Duration) : 0;
                accuracyLabel.gameObject.SetActive(false);
            }

            unwipedWarning.SetActive(input.Unwiped);
            var item = input.HeldItem;
            itemLabel.gameObject.SetActive(item != null);
            if (item is PaperRoll) itemLabel.text = $"手持：卷纸（还能擦 {input.PaperRemainingUses} 次）";
            else if (item is Mop) itemLabel.text = "手持：拖把（对着污物长按 F 清理）";
            else if (item != null) itemLabel.text = "手持：" + item.name;

            DrawTarget(input, phase);
        }

        private static string Prompt(PlayerInput.Stage phase) => phase switch
        {
            PlayerInput.Stage.Squatting => "再按 C 开拉（可低头看马桶）· V 先尿　Esc 站起",
            PlayerInput.Stage.Aiming => "再按 C 开拉 · V 先尿　Esc 站起",
            PlayerInput.Stage.Urinating => "看准位置，再按 V 开尿（开始后要尿完）　Esc 退出",
            PlayerInput.Stage.Cleaning => "清理中：按住 F",
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
            // No target circle while holding: defecation is blind except for the timed look-down marker (ToiletGaze).
            overlay.gameObject.SetActive(false);
            landingMarker.gameObject.SetActive(false);
            foreach (var dot in dots) dot.gameObject.SetActive(false);
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
