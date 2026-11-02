using System;
using System.IO;
using System.Linq;
using NatureCalls.Facility;
using NatureCalls.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;

namespace NatureCalls.Editor
{
    public static class PaperRollPickupSetup
    {
        private const string ScenePath = "Assets/Scenes/Playground.unity";
        private static PaperRoll probeRoll;
        private static PaperRoll probeSecondRoll;
        private static Movement probePlayer;
        private static int probeFrames;
        private static int probeStage;
        private static bool probeFirstPickupValid;
        private static bool probeSecondRejected;

        [MenuItem("NatureCalls/Configure Playground Paper Roll Pickup")]
        public static void Configure()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath) throw new InvalidOperationException($"Open {ScenePath} first.");

            var rolls = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(false))
                .Where(item => item.name.StartsWith("Paper roll", StringComparison.OrdinalIgnoreCase))
                .ToArray();
            if (rolls.Length != 3) throw new InvalidOperationException($"Expected 3 active Paper rolls, found {rolls.Length}.");
            foreach (var roll in rolls)
            {
                var pickup = roll.GetComponent<PaperRoll>();
                if (pickup == null) pickup = Undo.AddComponent<PaperRoll>(roll.gameObject);
                pickup.InteractionDistance = 1.5f;
                EditorUtility.SetDirty(pickup);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save Playground paper roll pickup setup.");
            AssetDatabase.SaveAssets();
            Debug.Log($"PAPER_ROLL_SETUP_OK|count={rolls.Length}|input=E,R|distance=1.50|capacity={PaperRoll.CarryCapacityPerPlayer}");
        }

        [MenuItem("NatureCalls/Run Paper Roll Pickup Probe")]
        public static void RunProbe()
        {
            if (!EditorApplication.isPlaying) throw new InvalidOperationException("Enter Play Mode first.");
            var map = UnityEngine.Object.FindFirstObjectByType<NatureCalls.Map.Playground>();
            if (map == null || map.OfflinePlayer == null) throw new InvalidOperationException("Offline practice player is not active.");
            if (Keyboard.current == null) throw new InvalidOperationException("The Input System has no current keyboard.");

            probePlayer = map.OfflinePlayer;
            probeRoll = UnityEngine.Object.FindObjectsByType<PaperRoll>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .FirstOrDefault(roll => !roll.IsPickedUp);
            if (probeRoll == null) throw new InvalidOperationException("No available PaperRoll was found.");

            var target = probeRoll.transform.position + Vector3.forward * .75f;
            probePlayer.SetControl(false);
            probePlayer.transform.position = new Vector3(target.x, probePlayer.transform.position.y, target.z);
            probePlayer.SetControl(true);
            Physics.SyncTransforms();

            EditorApplication.update -= TickProbe;
            probeFrames = 0;
            probeStage = 0;
            probeFirstPickupValid = false;
            probeSecondRejected = false;
            InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(Key.E));
            EditorApplication.update += TickProbe;
        }

        private static void TickProbe()
        {
            probeFrames++;
            if (probeFrames == 2 && Keyboard.current != null)
                InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
            if (probeFrames < 8) return;

            if (probeStage == 0)
            {
                probeRoll = UnityEngine.Object.FindObjectsByType<PaperRoll>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                    .FirstOrDefault(roll => roll.PickedUpBy == probePlayer);
                probeFirstPickupValid = probeRoll != null && PaperRoll.CountCarriedBy(probePlayer) == PaperRoll.CarryCapacityPerPlayer &&
                    probeRoll.GetComponentsInChildren<Renderer>(true).All(renderer => !renderer.enabled) &&
                    probeRoll.GetComponentsInChildren<Collider>(true).All(collider => !collider.enabled);
                probeSecondRoll = UnityEngine.Object.FindObjectsByType<PaperRoll>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                    .FirstOrDefault(roll => !roll.IsPickedUp);
                if (!probeFirstPickupValid || probeSecondRoll == null)
                {
                    FinishProbe(false, false);
                    return;
                }

                var target = probeSecondRoll.transform.position + Vector3.forward * .75f;
                probePlayer.SetControl(false);
                probePlayer.transform.position = new Vector3(target.x, probePlayer.transform.position.y, target.z);
                probePlayer.SetControl(true);
                Physics.SyncTransforms();
                probeStage = 1;
                probeFrames = 0;
                InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(Key.E));
                return;
            }

            var rolls = UnityEngine.Object.FindObjectsByType<PaperRoll>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            if (probeStage == 1)
            {
                var firstStillCarried = probeRoll != null && probeRoll.PickedUpBy == probePlayer;
                probeSecondRejected = probeSecondRoll != null && !probeSecondRoll.IsPickedUp &&
                    PaperRoll.CountCarriedBy(probePlayer) == PaperRoll.CarryCapacityPerPlayer &&
                    rolls.Where(roll => roll != probeRoll).All(roll => roll.PickedUpBy != probePlayer);
                if (!firstStillCarried || !probeSecondRejected)
                {
                    FinishProbe(false, false);
                    return;
                }

                probeStage = 2;
                probeFrames = 0;
                InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(Key.R));
                return;
            }

            var body = probeRoll != null ? probeRoll.GetComponent<Rigidbody>() : null;
            var throwValid = probeRoll != null && !probeRoll.IsPickedUp && probeRoll.transform.parent == null &&
                PaperRoll.CountCarriedBy(probePlayer) == 0 &&
                probeRoll.GetComponentsInChildren<Renderer>(true).Any(renderer => renderer.enabled) &&
                probeRoll.GetComponentsInChildren<Collider>(true).Any(collider => collider.enabled) &&
                body != null && !body.isKinematic && body.useGravity;
            FinishProbe(throwValid, throwValid);
        }

        private static void FinishProbe(bool success, bool throwValid)
        {
            EditorApplication.update -= TickProbe;
            var carriedCount = PaperRoll.CountCarriedBy(probePlayer);
            Debug.Log($"PAPER_ROLL_INPUT_PROBE_{(success ? "OK" : "FAILED")}|" +
                $"firstPickupValid={probeFirstPickupValid}|secondRejected={probeSecondRejected}|throwValid={throwValid}|" +
                $"carriedCount={carriedCount}|capacity={PaperRoll.CarryCapacityPerPlayer}");
            probeRoll = null;
            probeSecondRoll = null;
            probePlayer = null;
            probeFirstPickupValid = false;
            probeSecondRejected = false;
            if (!success)
                throw new InvalidOperationException("PaperRoll pickup, capacity, or R throw did not complete through InputActions.");
        }
    }
}
