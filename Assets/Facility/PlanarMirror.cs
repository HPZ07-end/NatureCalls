using System.Linq;
using NatureCalls.Camera;
using UnityEngine;

namespace NatureCalls.Facility
{
    [DisallowMultipleComponent]
    public sealed class PlanarMirror : MonoBehaviour
    {
        public UnityEngine.Camera ReflectionCamera;
        public Renderer Surface;

        private RenderTexture runtimeTexture;
        private Material runtimeMaterial;

        private void Awake()
        {
            if (ReflectionCamera == null)
                ReflectionCamera = GetComponentInChildren<UnityEngine.Camera>(true);
            if (ReflectionCamera == null || ReflectionCamera.targetTexture == null) return;

            var sourceTexture = ReflectionCamera.targetTexture;
            if (Surface == null)
                Surface = GetComponentsInChildren<Renderer>(true).FirstOrDefault(renderer =>
                    renderer.sharedMaterials.Any(material => material != null &&
                        ((material.HasProperty("_BaseMap") && material.GetTexture("_BaseMap") == sourceTexture) ||
                         (material.HasProperty("_MainTex") && material.GetTexture("_MainTex") == sourceTexture))));
            if (Surface == null) return;

            runtimeTexture = new RenderTexture(sourceTexture.descriptor)
            {
                name = $"{name} reflection"
            };
            runtimeTexture.Create();
            ReflectionCamera.targetTexture = runtimeTexture;
            ReflectionCamera.cullingMask |= 1 << CameraController.LocalBodyLayer;

            var listener = ReflectionCamera.GetComponent<AudioListener>();
            if (listener != null) listener.enabled = false;

            runtimeMaterial = new Material(Surface.sharedMaterial)
            {
                name = $"{Surface.sharedMaterial.name} ({name})"
            };
            if (runtimeMaterial.HasProperty("_BaseMap")) runtimeMaterial.SetTexture("_BaseMap", runtimeTexture);
            if (runtimeMaterial.HasProperty("_MainTex")) runtimeMaterial.SetTexture("_MainTex", runtimeTexture);
            Surface.material = runtimeMaterial;
        }

        private void OnDestroy()
        {
            if (runtimeMaterial != null) Destroy(runtimeMaterial);
            if (runtimeTexture == null) return;
            runtimeTexture.Release();
            Destroy(runtimeTexture);
        }
    }
}
