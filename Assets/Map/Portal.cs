using System.Collections.Generic;
using NatureCalls.Facility;
using NatureCalls.Player;
using Unity.Cinemachine;
using Unity.Netcode.Components;
using UnityEngine;

namespace NatureCalls.Map
{
    /// <summary>Moves an actor across a map seam after it crosses the configured x plane.</summary>
    [RequireComponent(typeof(BoxCollider))]
    public sealed class Portal : MonoBehaviour
    {
        public int CrossingDirectionX = 1;
        public Vector3 Offset;
        public Vector3 ZoneSize = new(4.4f, 2.6f, 6.6f);
        private readonly HashSet<int> crossed = new();
        private BoxCollider trigger;

        private void Awake()
        {
            trigger = GetComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = ZoneSize;
        }

        private void OnTriggerEnter(Collider other)
        {
            var root = other.attachedRigidbody != null ? other.attachedRigidbody.transform.root : other.transform.root;
            if (root == null || !CanMove(root)) return;
            var id = root.GetInstanceID();
            if (crossed.Contains(id)) return;
            var local = transform.InverseTransformPoint(root.position);
            if (CrossingDirectionX * local.x < 0f) return;
            crossed.Add(id);
            Move(root);
        }

        private void OnTriggerExit(Collider other)
        {
            var root = other.attachedRigidbody != null ? other.attachedRigidbody.transform.root : other.transform.root;
            if (root != null) crossed.Remove(root.GetInstanceID());
        }

        private bool CanMove(Transform root)
        {
            var movement = root.GetComponent<Movement>();
            if (movement != null)
            {
                var network = root.GetComponent<Unity.Netcode.NetworkObject>();
                return network == null || !network.IsSpawned || network.IsOwner;
            }
            return root.GetComponent<Rigidbody>() != null;
        }

        private void Move(Transform root)
        {
            var target = root.position + Offset;
            var controller = root.GetComponent<CharacterController>();
            if (controller != null) controller.enabled = false;
            var networkTransform = root.GetComponent<NetworkTransform>();
            // A spawned owner commits through NetworkTransform; offline and unspawned practice players move locally.
            if (networkTransform != null && networkTransform.CanCommitToTransform) networkTransform.Teleport(target, root.rotation, root.localScale);
            else root.SetPositionAndRotation(target, root.rotation);

            foreach (var item in FindObjectsByType<Item>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (item.PickedUpBy == root.GetComponent<Movement>()) item.transform.position += Offset;
            if (controller != null) controller.enabled = true;
            CinemachineCore.OnTargetObjectWarped(root, Offset);
            Physics.SyncTransforms();
        }
    }
}
