using System;
using UnityEngine;

namespace NatureCalls.Player
{
    public enum PostureState : byte { Standing, Squatting }

    public sealed class Posture : MonoBehaviour
    {
        public PostureState State { get; private set; }
        public bool IsSquatting => State == PostureState.Squatting;
        public event Action<PostureState> Changed;

        public void Set(PostureState state)
        {
            if (State == state) return;
            State = state;
            Changed?.Invoke(state);
        }
        public void Toggle() => Set(IsSquatting ? PostureState.Standing : PostureState.Squatting);
    }
}
