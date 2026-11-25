using NatureCalls.Core;
using NatureCalls.Player;
using UnityEngine;
namespace NatureCalls.Facility
{
    public sealed class Mess : MonoBehaviour, IDirtSource
    {
        public int Index { get; private set; }
        public MessKind Kind { get; private set; }
        public int ToiletIndex { get; private set; }
        public bool SaboteurSource { get; private set; }
        public Vector3 DirtPosition => transform.position;
        public float DirtWeight => MatchWeight(Kind) * (SaboteurSource ? .3f : 1f);
        private static float MatchWeight(MessKind kind) => kind switch { MessKind.Accident => 2.5f, MessKind.Vomit => 1.8f, MessKind.Dropping => 1f, MessKind.DirtyToilet => .8f, MessKind.Backflow => 1.2f, MessKind.Puddle => .5f, _ => 0f };
        public void Configure(int index, MessKind kind, Vector3 position, int toiletIndex, bool saboteurSource = false)
        {
            Index = index; Kind = kind; ToiletIndex = toiletIndex; SaboteurSource = saboteurSource;
            name = kind.ToString();
            transform.position = position + Vector3.up * (kind == MessKind.Puddle ? .015f : .11f);
            transform.localScale = kind == MessKind.Puddle || kind == MessKind.Backflow ? new Vector3(.8f, .025f, .8f) : Vector3.one * .22f;
            GetComponent<Renderer>().material.color = kind == MessKind.Dropping ? Color.black : kind == MessKind.Puddle ? Color.yellow : kind == MessKind.Backflow ? Color.magenta : new Color(.4f, .2f, .02f);
            DirtField.Register(this);
        }
        private void OnEnable() => DirtField.Register(this);
        private void OnDisable() => DirtField.Unregister(this);

        public Movement Source { get; private set; }
        public void SetSource(Movement source) { Source = source; SaboteurSource = source != null && RoleRegistry.IsSaboteur(source); }
        public bool CanClean(Movement player)
        {
            if (player == null || Item.FindCarriedBy(player) is not Mop) return false;
            var delta = Vector3.ProjectOnPlane(transform.position - player.transform.position, Vector3.up);
            return delta.magnitude <= NeedsRules.InteractionDistance && (delta.magnitude < .15f || Vector3.Dot(player.transform.forward, delta.normalized) >= NeedsRules.CleaningFacingCosine);
        }
    }
}
