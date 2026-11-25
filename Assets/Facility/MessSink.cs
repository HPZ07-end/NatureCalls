using NatureCalls.Core;

namespace NatureCalls.Facility
{
    /// <summary>Facility-side outlet for shared messes; Map registers the implementation.</summary>
    public interface IMessSink
    {
        int AddMess(MessKind kind, UnityEngine.Vector3 position, int toiletIndex = -1);
        /// <summary>Versus: same as AddMess, but records who produced it (saboteur droppings weigh less). Map overrides this.</summary>
        int AddMess(MessKind kind, UnityEngine.Vector3 position, int toiletIndex, NatureCalls.Player.Movement source) => AddMess(kind, position, toiletIndex);
        int ToiletIndex(Toilet toilet);
    }
    public static class MessSinkRegistry
    {
        public static IMessSink Current { get; private set; }
        public static void Register(IMessSink sink) => Current = sink;
        public static void Unregister(IMessSink sink) { if (ReferenceEquals(Current, sink)) Current = null; }
    }
}
