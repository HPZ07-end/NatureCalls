using UnityEngine;
using NatureCalls.Input;
namespace NatureCalls.UI
{
    /// <summary>Temporary debug presentation; reads the same interface available to the HUD.</summary>
    public sealed class AimDebug : MonoBehaviour
    {
        private PlayerInput input;
        private void Awake() => input = GetComponent<PlayerInput>();
        private void OnGUI()
        {
            if (input == null || !input.enabled || !GetComponent<Player.Movement>().Controlled) return;
            GUI.Label(new Rect(15, Screen.height - 75, 650, 65),
                $"{input.Phase}  {input.Elapsed:F1}/{input.Duration:F1}s  Accuracy {input.CurrentAccuracy:P0}\nUnwiped: {input.Unwiped}  Item: {input.HeldItem?.GetType().Name ?? "none"}  Paper: {input.PaperRemainingUses}  Clean: {input.CleaningProgress:P0}");
            if (input.Phase != PlayerInput.Stage.Aligning && !input.IsToileting) return;
            var camera = UnityEngine.Camera.main;
            if (camera == null) return;
            var center = camera.WorldToScreenPoint(input.TargetCenter);
            if (center.z <= 0) return;
            float radius = Vector3.Distance(center, camera.WorldToScreenPoint(input.TargetCenter + camera.transform.right * input.TargetRadius));
            for (int i = 0; i < 48; i++)
            {
                float angle = i * Mathf.PI * 2 / 48;
                GUI.Box(new Rect(center.x + Mathf.Cos(angle) * radius - 2, Screen.height - center.y + Mathf.Sin(angle) * radius - 2, 4, 4), "");
            }
            var landing = camera.WorldToScreenPoint(input.CurrentLanding);
            if (landing.z > 0) GUI.Label(new Rect(landing.x - 6, Screen.height - landing.y - 10, 24, 24), "+");
        }
    }
}
