using UnityEngine;

namespace NatureCalls.Map
{
    /// <summary>A low passage with two sloped sections and a level passing bay.</summary>
    public sealed class VentPassage : MonoBehaviour
    {
        public Transform PassingBay;
        public Transform LowerSlope;
        public Transform Exit;

        public bool RequiresCrawling(Vector3 feet)
        {
            var local = transform.InverseTransformPoint(feet);
            var bay = PassingBay.localPosition;
            var lower = LowerSlope.localPosition;
            var exit = Exit.localPosition;
            if (local.x < -.62f || local.x > exit.x + .55f)
                return false;

            bool inBay = local.x >= bay.x && local.x <= lower.x;
            if (Mathf.Abs(local.z) > (inBay ? .94f : .67f))
                return false;

            var floor = local.x < bay.x ? Mathf.Lerp(0, bay.y, local.x / bay.x) :
                Mathf.Lerp(lower.y, exit.y, (local.x - lower.x) / (exit.x - lower.x));
            return local.y >= floor - .18f && local.y <= floor + .50f;
        }
    }
}
