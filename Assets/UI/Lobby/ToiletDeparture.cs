using System;
using System.Collections;
using NatureCalls.Camera;
using UnityEngine;
using UnityEngine.UI;

namespace NatureCalls.UI
{
    /// <summary>Local water vortex, visible character suction and camera plunge before the existing map load.</summary>
    public sealed class ToiletDeparture : MonoBehaviour
    {
        public const float Duration = 2.5f;
        private DepartureFade fade;
        private Material foam;
        public void Begin(ToiletLobby room, int mode, Action completed) => StartCoroutine(Animate(room, mode, completed));
        private IEnumerator Animate(ToiletLobby room, int mode, Action completed)
        {
            var player = room.World.OfflinePlayer;
            var camera = room.World.Camera.Output;
            var water = room.Water[mode]; var center = water.position;
            if (Vector3.Distance(player.transform.position, center) > 2 || !room.Doors[mode].IsInside(player))
            {
                player.transform.SetPositionAndRotation(room.Approach[mode].position, Quaternion.identity);
                camera.transform.SetPositionAndRotation(center + Vector3.up * 1.2f + Vector3.forward * .2f, Quaternion.Euler(90, 0, 0));
            }
            var startPlayer = player.transform.position; var startScale = player.transform.localScale;
            var startCamera = camera.transform.position; var startRotation = camera.transform.rotation;
            float startFov = camera.fieldOfView;
            room.World.Camera.enabled = false;
            camera.cullingMask |= 1 << CameraController.LocalBodyLayer;
            foam = new Material(Shader.Find("Universal Render Pipeline/Unlit")); foam.color = new Color(.71f, .94f, .89f);
            var whirl = new GameObject("Suction Water Vortex").AddComponent<LineRenderer>(); whirl.transform.SetParent(transform);
            whirl.useWorldSpace = true; whirl.positionCount = 128; whirl.widthMultiplier = .006f;
            whirl.sharedMaterial = foam; whirl.numCornerVertices = 2;
            var overlay = new GameObject("Toilet Departure Fade", typeof(RectTransform), typeof(Canvas), typeof(DepartureFade));
            overlay.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay; overlay.GetComponent<Canvas>().sortingOrder = 1000;
            fade = overlay.GetComponent<DepartureFade>();
            var black = new GameObject("Black", typeof(RectTransform), typeof(Image)).GetComponent<Image>(); black.transform.SetParent(overlay.transform, false);
            black.rectTransform.anchorMin = Vector2.zero; black.rectTransform.anchorMax = Vector2.one;
            black.rectTransform.offsetMin = black.rectTransform.offsetMax = Vector2.zero; black.raycastTarget = false;
            fade.Black = black; fade.Set(0); DontDestroyOnLoad(overlay);
            var waterScale = water.localScale;
            for (float elapsed = 0; elapsed < Duration; elapsed += Time.unscaledDeltaTime)
            {
                if (player == null || water == null) { if (fade != null) Destroy(fade.gameObject); yield break; }
                float pull = Mathf.SmoothStep(0, 1, Mathf.Clamp01((elapsed - .35f) / 1.65f));
                float lift = Mathf.SmoothStep(0, 1, elapsed / .45f);
                float angle = elapsed * 8;
                var orbit = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * (.38f * (1 - pull));
                var destination = center + orbit + Vector3.up * Mathf.Lerp(.1f, -.25f, pull);
                player.transform.position = Vector3.Lerp(startPlayer, destination, lift);
                player.transform.localScale = startScale * Mathf.Lerp(1, .035f, pull);
                player.transform.rotation = Quaternion.Euler(pull * 130, elapsed * 260, pull * 65);
                var oblique = center + new Vector3(0, 2, -1.1f);
                var rotation = Quaternion.LookRotation(center + Vector3.up * Mathf.Lerp(1, .2f, pull) - oblique);
                float plunge = Mathf.SmoothStep(0, 1, Mathf.Clamp01((elapsed - 1.8f) / .7f));
                camera.transform.position = Vector3.Lerp(Vector3.Lerp(startCamera, oblique, lift), center + Vector3.up * .12f, plunge);
                camera.transform.rotation = Quaternion.Slerp(Quaternion.Slerp(startRotation, rotation, lift), Quaternion.Euler(90, 0, 160), plunge);
                camera.fieldOfView = Mathf.Lerp(Mathf.Lerp(startFov, 85, lift), 95, plunge);
                water.localScale = new Vector3(waterScale.x * (1 - pull * .9f), waterScale.y, waterScale.z * (1 - pull * .9f));
                for (int i = 0; i < whirl.positionCount; i++)
                {
                    float fraction = i / (float)(whirl.positionCount - 1); float phase = angle + fraction * Mathf.PI * 7;
                    float radius = (.012f + .14f * fraction) * (1 - pull * .8f);
                    whirl.SetPosition(i, center + new Vector3(Mathf.Cos(phase) * radius, .008f + fraction * .006f, Mathf.Sin(phase) * radius * 1.1f));
                }
                fade.Set(plunge);
                yield return null;
            }
            fade.Set(1); fade.Committed = true;
            completed?.Invoke();
        }
        private void OnDestroy()
        {
            if (foam != null) Destroy(foam);
            if (fade != null && !fade.Committed) Destroy(fade.gameObject);
        }
    }
}
