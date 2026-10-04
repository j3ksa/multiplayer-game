#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace MultiplayerGame.Editor
{
    public static class BuildHelper
    {
        [MenuItem("Tools/Multiplayer/Build Windows Standalone (.exe)")]
        public static void BuildWindowsPlayer()
        {
            string projectRoot = Directory.GetCurrentDirectory();
            string buildFolder = Path.Combine(projectRoot, "Builds");

            if (!Directory.Exists(buildFolder))
            {
                Directory.CreateDirectory(buildFolder);
            }

            string exePath = Path.Combine(buildFolder, "MultiplayerGame.exe");

            BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/SampleScene.unity" },
                locationPathName = exePath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };

            Debug.Log($"[BuildHelper] Starting Windows standalone build to '{exePath}'...");
            BuildReport report = BuildPipeline.BuildPlayer(buildPlayerOptions);
            BuildSummary summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"[BuildHelper] Build SUCCEEDED! Total size: {summary.totalSize / (1024 * 1024):F1} MB in {summary.totalTime.TotalSeconds:F1}s.");
                EditorUtility.RevealInFinder(exePath);
            }
            else
            {
                Debug.LogError($"[BuildHelper] Build finished with result: '{summary.result}'. Check console for any compiler or asset errors.");
            }
        }
    }
}
#endif
