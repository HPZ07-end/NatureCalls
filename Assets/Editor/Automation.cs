using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NatureCalls.Editor
{
    public static class Automation
    {
        private const string SmokeScene = "Assets/Scenes/Smoke.unity";

        [MenuItem("NatureCalls/Setup")]
        public static void Setup()
        {
            EditorSettings.serializationMode = SerializationMode.ForceText;
            VersionControlSettings.mode = "Visible Meta Files";

            // Add a camera/light validation scene only when absent; preserve existing scenes.
            if (!File.Exists(SmokeScene))
            {
                if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                    throw new OperationCanceledException("Setup cancelled; scene changes were not saved.");
                if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
                    AssetDatabase.CreateFolder("Assets", "Scenes");
                var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Additive);
                try
                {
                    if (!EditorSceneManager.SaveScene(scene, SmokeScene))
                        throw new IOException("Failed to save " + SmokeScene);
                }
                finally
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }

            var scenes = EditorBuildSettings.scenes.ToList();
            if (!scenes.Any(scene => scene.path == SmokeScene))
            {
                scenes.Add(new EditorBuildSettingsScene(SmokeScene, true));
                EditorBuildSettings.scenes = scenes.ToArray();
            }
            AssetDatabase.SaveAssets();
            Validate();
        }

        public static void Playground()
        {
            NatureCalls.Editor.Playground.Create();
            Validate();
        }

        [MenuItem("NatureCalls/Validate")]
        public static void Validate()
        {
            if (EditorSettings.serializationMode != SerializationMode.ForceText)
                throw new InvalidOperationException("Serialization must be Force Text. Run setup.");
            if (VersionControlSettings.mode != "Visible Meta Files")
                throw new InvalidOperationException("Version control must use Visible Meta Files. Run setup.");
            if (EditorUtility.scriptCompilationFailed)
                throw new InvalidOperationException("Script compilation failed.");
            Debug.Log("NatureCalls validation passed: " + Application.unityVersion);
            WriteReceipt("validate");
        }

        public static void BuildMac()
        {
#if UNITY_STANDALONE_OSX
            Validate();
            var scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray();
            if (scenes.Length == 0 || scenes.Any(scene => !File.Exists(scene)))
                throw new InvalidOperationException("Build needs existing enabled scenes. Run setup.");
            var output = System.Environment.GetEnvironmentVariable("NATURECALLS_BUILD_PATH");
            if (string.IsNullOrEmpty(output))
                throw new InvalidOperationException("Use scripts/unity.py build-macos to provide a fresh output path.");
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            var previous = UnityEditor.OSXStandalone.UserBuildSettings.architecture;
            try
            {
                UnityEditor.OSXStandalone.UserBuildSettings.architecture = OSArchitecture.ARM64;
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = scenes,
                    locationPathName = output,
                    target = BuildTarget.StandaloneOSX,
                    options = BuildOptions.Development
                });
                Debug.Log($"NatureCalls build: {report.summary.result}; errors={report.summary.totalErrors}; bytes={report.summary.totalSize}");
                if (report.summary.result != BuildResult.Succeeded)
                    throw new InvalidOperationException("macOS build failed: " + report.summary.result);
                WriteReceipt("build");
            }
            finally
            {
                UnityEditor.OSXStandalone.UserBuildSettings.architecture = previous;
            }
#else
            throw new InvalidOperationException("Run with -buildTarget osxuniversal and install Mac Build Support.");
#endif
        }

        private static void WriteReceipt(string action)
        {
            var directory = System.Environment.GetEnvironmentVariable("NATURECALLS_RUN_PATH");
            if (string.IsNullOrEmpty(directory)) return;
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, action + ".ok"), Application.unityVersion + "\n");
        }
    }
}
