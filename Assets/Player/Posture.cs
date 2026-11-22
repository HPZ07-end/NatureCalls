using System;
using UnityEngine;

namespace NatureCalls.Player
{
    public enum PostureState : byte { Standing = 0, Squatting = 1, Crawling = 2 }

    public sealed class Posture : MonoBehaviour
    {
        public PostureState State { get; private set; }
        public bool IsSquatting => State == PostureState.Squatting;
        public bool IsCrawling => State == PostureState.Crawling;
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
