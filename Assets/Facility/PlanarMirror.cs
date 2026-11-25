using System.Linq;
using NatureCalls.Camera;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace NatureCalls.Facility
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(1000)] // After Cinemachine updates the viewer in LateUpdate.
    public sealed class PlanarMirror : MonoBehaviour
    {
        public UnityEngine.Camera ReflectionCamera;
        public Renderer Surface;
        public UnityEngine.Camera ViewerCamera;
        public Vector2 Size = new(.65f, .75f);

        private RenderTexture runtimeTexture;
        private Material runtimeMaterial;

        private void Awake()
        {
            if (Network.Session.Dedicated)
            {
                foreach (var camera in GetComponentsInChildren<UnityEngine.Camera>(true)) camera.enabled = false;
                enabled = false;
                return;
            }
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
            if (ViewerCamera != null) ReflectionCamera.enabled = false;
        }

        private void LateUpdate()
        {
            // compat: fixed Playground mirrors keep their old cameras until their atlas UVs are migrated.
            // Remove this branch when those surfaces use the full-range mirror mesh and a ViewerCamera.
            if (ViewerCamera == null || Surface == null || !Surface.enabled || !Surface.gameObject.activeInHierarchy) return;
            if (!GeometryUtility.TestPlanesAABB(GeometryUtility.CalculateFrustumPlanes(ViewerCamera), Surface.bounds)) return;
            RenderReflection();
        }

        /// <summary>Reflects the viewer across the plane and treats its four corners as a viewing window.</summary>
        public bool UpdateView()
        {
            if (ViewerCamera == null || ReflectionCamera == null || Surface == null) return false;
            var plane = Surface.transform;
            var center = plane.position;
            var normal = -plane.forward;
            float distance = Vector3.Dot(ViewerCamera.transform.position - center, normal);
            if (distance <= .025f || Size.x <= 0 || Size.y <= 0) return false;
            var eye = ViewerCamera.transform.position - 2 * distance * normal;
            var rotation = Quaternion.LookRotation(normal, plane.up);
            ReflectionCamera.transform.SetPositionAndRotation(eye, rotation);
            var view = Matrix4x4.Scale(new Vector3(1, 1, -1)) * Matrix4x4.TRS(eye, rotation, Vector3.one).inverse;
            var bottomRight = plane.TransformPoint(new Vector3(Size.x / 2, -Size.y / 2, 0));
            var bottomLeft = plane.TransformPoint(new Vector3(-Size.x / 2, -Size.y / 2, 0));
            var topRight = plane.TransformPoint(new Vector3(Size.x / 2, Size.y / 2, 0));
            var right = -plane.right;
            // A proper camera rotation plus reversed U supplies the mirror's horizontal reversal;
            // no global invert-culling state is changed. Clip everything behind the mirror surface.
            float near = distance + .003f;
            float far = Mathf.Max(near + 1, ViewerCamera.farClipPlane);
            float scale = near / distance;
            var projection = Matrix4x4.Frustum(
                Vector3.Dot(right, bottomRight - eye) * scale,
                Vector3.Dot(right, bottomLeft - eye) * scale,
                Vector3.Dot(plane.up, bottomRight - eye) * scale,
                Vector3.Dot(plane.up, topRight - eye) * scale, near, far);
            ReflectionCamera.nearClipPlane = near;
            ReflectionCamera.farClipPlane = far;
            ReflectionCamera.worldToCameraMatrix = view;
            ReflectionCamera.projectionMatrix = projection;
            ReflectionCamera.cullingMatrix = projection * view;
            ReflectionCamera.cullingMask = (ViewerCamera.cullingMask | (1 << CameraController.LocalBodyLayer)) & ~(1 << 5);
            return true;
        }

        public bool RenderReflection()
        {
            if (runtimeTexture == null || !UpdateView()) return false;
            var request = new UniversalRenderPipeline.SingleCameraRequest { destination = runtimeTexture };
            if (!RenderPipeline.SupportsRenderRequest(ReflectionCamera, request)) return false;
            RenderPipeline.SubmitRenderRequest(ReflectionCamera, request);
            return true;
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
