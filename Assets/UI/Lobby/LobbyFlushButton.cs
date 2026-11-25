using System.Collections;
using UnityEngine;

namespace NatureCalls.UI
{
    /// <summary>The physical plunger on a connection toilet; no pointer-click handler or key binding.</summary>
    public sealed class LobbyFlushButton : MonoBehaviour
    {
        public Transform Head;
        public bool Pressing { get; private set; }
        public void Press() { if (!Pressing) StartCoroutine(Animate()); }
        private IEnumerator Animate()
        {
            Pressing = true;
            var rest = Head.localPosition;
            var press = Head.parent != null ? Head.parent.InverseTransformVector(Vector3.down * .004f) : Vector3.down * .004f;
            for (float t = 0; t < .3f; t += Time.unscaledDeltaTime)
            {
                Head.localPosition = rest + press * Mathf.Sin(Mathf.PI * t / .3f);
                yield return null;
            }
            Head.localPosition = rest; Pressing = false;
        }
    }
}
