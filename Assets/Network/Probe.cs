#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using NatureCalls.Player;

namespace NatureCalls.Network
{
    // Explicit development-build probe only. No test controller exists in normal play or release builds.
    public sealed class Probe : MonoBehaviour
    {
        [Serializable]
        private sealed class Report
        {
            public string role, status, error;
            public float localDistance, remoteDistance;
            public int localColor, remoteColor;
            public Vector3 localSpawn, remoteSpawn;
            public bool twoPlayers, ownership, singleCamera, independentCamera, remoteMoved,
                remoteRotated, remoteSquatted, remoteStoodAgain, remoteVisible, distinctSpawns, cleanup;
        }
        private string reportPath;
        private Report report;
        private bool finished;
        private Session session;
        private static string Argument(string key)
        {
            var args = System.Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, key);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : "";
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Launch()
        {
            if (!System.Environment.GetCommandLineArgs().Contains("-m1-probe")) return;
            var probe = new GameObject("Explicit multiplayer verification").AddComponent<Probe>();
            probe.reportPath = Argument("-m1-report");
            probe.report = new Report { role = Argument("-m1-role"), status = "running" };
        }
        private IEnumerator Start()
        {
            yield return null;
            session = FindFirstObjectByType<Session>();
            if (session == null || string.IsNullOrEmpty(reportPath)) { Finish("Missing probe arguments or Session."); yield break; }
            if (ushort.TryParse(Argument("-m1-port"), out var port)) session.Port = port;
            if (!session.StartDirect(report.role == "host")) { Finish(session.Status); yield break; }
            float deadline = Time.realtimeSinceStartup + 40;
            Avatar[] avatars;
            do
            {
                avatars = FindObjectsByType<Avatar>(FindObjectsSortMode.None).Where(a => a.IsSpawned).ToArray();
                if (avatars.Length == 2) break;
                yield return null;
            } while (Time.realtimeSinceStartup < deadline);
            if (avatars.Length != 2) { Finish("Two network players did not spawn: " + session.Status); yield break; }
            report.twoPlayers = true;
            var own = avatars.SingleOrDefault(a => a.IsOwner);
            var peer = avatars.SingleOrDefault(a => !a.IsOwner);
            if (own == null || peer == null) { Finish("Ownership did not identify one local and one remote player."); yield break; }
            // Server-assigned NetworkVariables may arrive on the next network tick.
            deadline = Time.realtimeSinceStartup + 2;
            while ((own.ColorIndex.Value == peer.ColorIndex.Value || Vector3.Distance(own.transform.position, peer.transform.position) <= 1) &&
                Time.realtimeSinceStartup < deadline) yield return null;
            var movement = own.GetComponent<Movement>();
            var posture = own.GetComponent<Posture>();
            own.GetComponent<NatureCalls.Input.PlayerInput>().enabled = false; // Deterministic input drives the same public abilities.
            report.ownership = movement.Controlled && !peer.GetComponent<Movement>().Controlled &&
                !peer.GetComponent<NatureCalls.Input.PlayerInput>().enabled;
            report.singleCamera = UnityEngine.Camera.allCamerasCount == 1 && session.World.Camera.Target == movement;
            Vector3 ownStart = own.transform.position;
            Vector3 peerStart = peer.transform.position;
            report.localColor = own.ColorIndex.Value;
            report.remoteColor = peer.ColorIndex.Value;
            report.localSpawn = ownStart;
            report.remoteSpawn = peerStart;
            Quaternion peerRotation = peer.transform.rotation;
            report.distinctSpawns = Vector3.Distance(own.transform.position, peer.transform.position) > 1 && own.ColorIndex.Value != peer.ColorIndex.Value;
            report.remoteVisible = true;
            bool turned = false;
            float started = Time.realtimeSinceStartup;
            bool captured = false;
            while (Time.realtimeSinceStartup - started < (report.role == "host" ? 26 : 18))
            {
                float elapsed = Time.realtimeSinceStartup - started;
                if (!turned && elapsed > 3.2f)
                {
                    movement.Look(new Vector2(report.role == "host" ? 250 : -350, 0));
                    turned = true;
                }
                // Allow both processes to finish initial scene/shader work before motion.
                if (elapsed >= 3 && elapsed < 6) movement.Move(Vector2.right * .25f, Time.unscaledDeltaTime);
                else movement.Move(Vector2.zero, Time.unscaledDeltaTime);
                float squatAt = report.role == "host" ? 7 : 10;
                posture.Set(elapsed >= squatAt && elapsed < squatAt + 2 ? PostureState.Squatting : PostureState.Standing);
                if (peer != null && peer.IsSpawned)
                {
                    report.localDistance = Mathf.Max(report.localDistance, Vector3.Distance(ownStart, own.transform.position));
                    report.remoteDistance = Mathf.Max(report.remoteDistance, Vector3.Distance(peerStart, peer.transform.position));
                    report.remoteMoved |= report.remoteDistance > .25f;
                    report.remoteRotated |= Quaternion.Angle(peerRotation, peer.transform.rotation) > 5;
                    report.remoteSquatted |= peer.GetComponent<Posture>().IsSquatting;
                    report.remoteStoodAgain |= report.remoteSquatted && !peer.GetComponent<Posture>().IsSquatting;
                    if (peer.GetComponent<Posture>().IsSquatting && !posture.IsSquatting)
                        report.independentCamera |= session.World.Camera.Target == movement &&
                            session.World.Camera.FirstPerson.Priority.Value > session.World.Camera.ThirdPerson.Priority.Value;
                    report.ownership &= !peer.GetComponent<Movement>().Controlled;
                    report.remoteVisible &= peer.GetComponent<Movement>().Body.GetComponentsInChildren<Renderer>().All(r => !r.forceRenderingOff);
                    report.singleCamera &= UnityEngine.Camera.allCamerasCount == 1 && session.World.Camera.Target == movement;
                    if (!captured && elapsed > 16)
                    {
                        var direction = peer.transform.position - own.transform.position;
                        float yaw = Vector3.SignedAngle(own.transform.forward, direction, Vector3.up);
                        movement.Look(new Vector2(yaw / movement.Sensitivity, 0));
                        captured = true;
                    }
                    if (elapsed > 17 && !File.Exists(reportPath + ".png")) Capture(session.World.Camera.Output, reportPath + ".png");
                }
                else if (report.role == "host")
                {
                    report.cleanup = session.Manager.ConnectedClients.Count == 1 &&
                        FindObjectsByType<Avatar>(FindObjectsSortMode.None).Count(a => a.IsSpawned) == 1;
                    if (report.cleanup) break;
                }
                yield return null;
            }
            if (report.role == "client")
            {
                var leaving = session.LeaveAsync();
                while (!leaving.IsCompleted) yield return null;
                float cleanupDeadline = Time.realtimeSinceStartup + 5;
                while (FindObjectsByType<Avatar>(FindObjectsSortMode.None).Length != 0 && Time.realtimeSinceStartup < cleanupDeadline) yield return null;
                report.cleanup = !session.Manager.IsListening && session.World.Camera.Target == null &&
                    FindObjectsByType<Avatar>(FindObjectsSortMode.None).Length == 0;
            }
            bool passed = report.localDistance > .25f && report.twoPlayers && report.ownership && report.singleCamera && report.independentCamera &&
                report.remoteMoved && report.remoteRotated && report.remoteSquatted && report.remoteStoodAgain && report.remoteVisible && report.distinctSpawns && report.cleanup;
            Finish(passed ? "" : "One or more multiplayer assertions failed; inspect report fields.");
        }
        private void Finish(string error)
        {
            if (finished) return;
            finished = true;
            report.error = error;
            report.status = string.IsNullOrEmpty(error) ? "passed" : "failed";
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
            File.WriteAllText(reportPath, JsonUtility.ToJson(report, true));
            Debug.Log("NatureCalls multiplayer probe: " + report.status);
            Application.Quit(string.IsNullOrEmpty(error) ? 0 : 1);
        }
        private void Update()
        {
            if (!finished && Time.realtimeSinceStartup > 70) Finish("Probe timed out.");
        }
        private static void Capture(UnityEngine.Camera camera, string path)
        {
            var texture = new RenderTexture(1100, 760, 24);
            texture.Create();
            var previous = RenderTexture.active;
            var image = new Texture2D(1100, 760, TextureFormat.RGB24, false);
            try
            {
                RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = texture });
                RenderTexture.active = texture;
                image.ReadPixels(new Rect(0, 0, 1100, 760), 0, 0);
                image.Apply();
                File.WriteAllBytes(path, image.EncodeToPNG());
            }
            finally { RenderTexture.active = previous; texture.Release(); Destroy(texture); Destroy(image); }
        }
    }
}
#endif
