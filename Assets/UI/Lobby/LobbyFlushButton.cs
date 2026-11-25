using System.Collections;
using NatureCalls.Facility;
using UnityEngine;
using NatureCalls.Player;

namespace NatureCalls.UI
{
    /// <summary>The physical plunger on a connection toilet; no pointer-click handler or key binding.</summary>
    public sealed class LobbyFlushButton : InteractableComponent
    {
        public Transform Head;
        public ToiletLobby Room;
        public int Mode;
        public bool Pressing { get; private set; }
        public void Press() { if (!Pressing) StartCoroutine(Animate()); }
        public override float Distance => 1.5f;
        public override bool CanInteract(InteractionContext context) => Room != null && Room.CanInteractButton(context.Player, Mode);
        public override string Prompt(InteractionContext context) => "E  确认";
        public override bool Perform(InteractionContext context) => Room != null && Room.ActivateButton(Mode);
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
