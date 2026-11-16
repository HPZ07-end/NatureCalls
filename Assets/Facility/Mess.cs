using NatureCalls.Core;
using NatureCalls.Player;
using UnityEngine;
namespace NatureCalls.Facility
{
    public sealed class Mess : MonoBehaviour
    {
        public int Index { get; private set; }
        public MessKind Kind { get; private set; }
        public int ToiletIndex { get; private set; }
        public void Configure(int index, MessKind kind, Vector3 position, int toiletIndex)
        {
            Index = index; Kind = kind; ToiletIndex = toiletIndex;
            name = kind.ToString();
            transform.position = position + Vector3.up * (kind == MessKind.Puddle ? .015f : .11f);
            transform.localScale = kind == MessKind.Puddle ? new Vector3(.8f, .025f, .8f) : Vector3.one * .22f;
            GetComponent<Renderer>().material.color = kind == MessKind.Dropping ? Color.black : kind == MessKind.Puddle ? Color.yellow : new Color(.4f, .2f, .02f);
        }
        public bool CanClean(Movement player)
        {
            if (player == null || Item.FindCarriedBy(player) is not Mop) return false;
            var delta = Vector3.ProjectOnPlane(transform.position - player.transform.position, Vector3.up);
            return delta.magnitude <= NeedsRules.InteractionDistance && (delta.magnitude < .15f || Vector3.Dot(player.transform.forward, delta.normalized) >= NeedsRules.CleaningFacingCosine);
        }
    }
}
