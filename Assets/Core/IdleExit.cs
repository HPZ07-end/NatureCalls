using System;

namespace NatureCalls.Core
{
    /// <summary>Pure server-room idle timer. A negative value disables automatic exit.</summary>
    public sealed class IdleExitTimer
    {
        private readonly float limit;
        private float emptySeconds;
        public IdleExitTimer(float seconds) { limit = seconds > 0 ? seconds : -1; }
        public float EmptySeconds => emptySeconds;
        public bool Enabled => limit > 0;
        public bool Tick(float deltaSeconds, int connectedClients)
        {
            if (!Enabled) return false;
            emptySeconds = connectedClients == 0 ? emptySeconds + Math.Max(0, deltaSeconds) : 0;
            return emptySeconds >= limit;
        }
    }
}
