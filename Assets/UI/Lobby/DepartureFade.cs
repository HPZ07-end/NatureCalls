using System.Collections;
using NatureCalls.Map;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace NatureCalls.UI
{
    public sealed class DepartureFade : MonoBehaviour
    {
        public Image Black;
        public bool Committed;
        public void Set(float alpha) { var color = Black.color; color.a = alpha; Black.color = color; }
        private void OnEnable() => SceneManager.sceneLoaded += Loaded;
        private void OnDisable() => SceneManager.sceneLoaded -= Loaded;
        private void Loaded(Scene scene, LoadSceneMode mode)
        {
            if (!Committed) return;
            if (scene.path == Match.DressingRoomScenePath) { Destroy(gameObject); return; }
            StartCoroutine(Reveal());
        }
        private IEnumerator Reveal()
        {
            float deadline = Time.unscaledTime + 8;
            NatureCalls.Camera.CameraController view;
            while (((view = FindFirstObjectByType<NatureCalls.Camera.CameraController>()) == null || view.Target == null) && Time.unscaledTime < deadline)
                yield return null;
            var player = view != null ? view.Target : null;
            if (player != null) player.SetControl(false);
            for (float t = 0; t < .45f; t += Time.unscaledDeltaTime) { Set(1 - t / .45f); yield return null; }
            if (player != null) player.SetControl(true);
            if (Application.isFocused) { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
            Destroy(gameObject);
        }
    }
}
