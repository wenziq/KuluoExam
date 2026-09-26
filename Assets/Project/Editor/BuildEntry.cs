using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Sokoban.Editor
{
    public static class BuildEntry
    {
        public static string LastReportPath { get; private set; }

        [MenuItem("Sokoban/Build/Windows x64 Mono")]
        public static void BuildWindowsMono()
        {
            Build(BuildTarget.StandaloneWindows64, "Builds/Windows/Sokoban.exe");
        }

        [MenuItem("Sokoban/Build/Mac Standalone")]
        public static void BuildMacStandalone()
        {
            Build(BuildTarget.StandaloneOSX, "Builds/Mac/Sokoban.app");
        }

        public static void BuildWindowsDevelopment()
        {
            Build(BuildTarget.StandaloneWindows64, "Builds/WindowsDevelopment/Sokoban.exe", BuildOptions.Development);
        }

        public static void BuildMacDevelopment()
        {
            Build(BuildTarget.StandaloneOSX, "Builds/MacDevelopment/Sokoban.app", BuildOptions.Development);
        }

        private static void Build(BuildTarget target, string output, BuildOptions extraOptions = BuildOptions.None)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || BuildPipeline.isBuildingPlayer)
                throw new InvalidOperationException("Build requires an idle editor outside Play Mode.");
            var reportDirectory = Path.GetFullPath(Environment.GetEnvironmentVariable("SOKOBAN_BUILD_EVIDENCE_DIR")
                ?? "Artifacts/Verification/Builds");
            Directory.CreateDirectory(reportDirectory);
            LastReportPath = Path.Combine(reportDirectory,
                "build-" + target + "-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + ".txt");
            var text = new StringBuilder();
            text.AppendLine("UTC: " + DateTime.UtcNow.ToString("O"));
            text.AppendLine("Unity: " + Application.unityVersion);
            text.AppendLine("Host: " + SystemInfo.operatingSystem);
            text.AppendLine("Target: " + target);
            text.AppendLine("Backend: Mono");
            text.AppendLine("Options: " + extraOptions);
            text.AppendLine("Output: " + Path.GetFullPath(output));
            text.AppendLine("Report: " + LastReportPath);
            var oldBackend = PlayerSettings.GetScriptingBackend(NamedBuildTarget.Standalone);
            try
            {
                if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, target))
                    throw new InvalidOperationException("Missing build support for " + target);
                ProjectScaffold.EnsureBootstrap();
                PlayerSettings.productName = "推箱子";
                Directory.CreateDirectory(Path.GetDirectoryName(output));
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { ProjectScaffold.BootScenePath },
                    locationPathName = output,
                    target = target,
                    extraScriptingDefines = extraOptions == BuildOptions.Development ? new[]{"SOKOBAN_VERIFICATION_BUILD"} : Array.Empty<string>(),
                    options = BuildOptions.DetailedBuildReport | extraOptions
                });
                var summary = report.summary;
                text.AppendLine("Result: " + summary.result);
                text.AppendLine("Duration: " + summary.totalTime);
                text.AppendLine("Bytes: " + summary.totalSize);
                text.AppendLine("Errors: " + summary.totalErrors + "; Warnings: " + summary.totalWarnings);
                foreach (var step in report.steps)
                {
                    text.AppendLine("Step: " + step.name + " [" + step.duration + "]");
                    foreach (var message in step.messages)
                        text.AppendLine("  " + message.type + ": " + message.content);
                }
                foreach (var file in report.GetFiles())
                    text.AppendLine("File: " + file.path + " | " + file.role + " | " + file.size);
                if (summary.result != BuildResult.Succeeded)
                    throw new InvalidOperationException("Build ended with " + summary.result + ". See " + LastReportPath);
            }
            catch (Exception exception)
            {
                text.AppendLine("Exception: " + exception);
                throw;
            }
            finally
            {
                // Persist evidence even if restoring project settings throws.
                File.WriteAllText(LastReportPath, text.ToString(), new UTF8Encoding(false));
                Debug.Log("Sokoban build report: " + LastReportPath);
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, oldBackend);
            }
        }
    }
}
