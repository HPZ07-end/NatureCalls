using System;
using NatureCalls.Facility;
using UnityEngine;

namespace NatureCalls.Network
{
    /// <summary>Connects Facility/HardStool to the server-owned item list (SharedItems); offline the hooks fall through.</summary>
    public static class StoolSync
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Init()
        {
            HardStool.IsAuthority = () => { var host = Facilities.Current; return host == null || !host.IsSpawned || host.IsServer; };
            HardStool.Publish = Publish;
            HardStool.RequestKick = (stool, player, velocity) =>
            {
                var sync = player != null ? player.GetComponent<FacilitySync>() : null;
                if (sync == null || !sync.IsSpawned) return false;
                sync.RequestKick(stool, velocity);
                return true; // online: never also simulate locally outside the shared flight
            };
        }

        private static void Publish(HardStool stool)
        {
            var host = Facilities.Current;
            if (host == null || !host.IsSpawned || !host.IsServer || host.items == null) return;
            int index = Array.IndexOf(host.items, stool);
            if (index >= 0 && index < host.SharedItems.Count)
                host.SharedItems[index] = Facilities.Snapshot(stool, stool.Stored ? ItemSnapshot.Stored : ItemSnapshot.Landed, 0);
            host.PublishShared(); // pipe load from a flush
        }
    }
}
