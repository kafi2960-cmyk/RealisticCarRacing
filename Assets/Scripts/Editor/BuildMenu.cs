using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace RealisticCarRacing.EditorTools
{
    /// <summary>
    /// One-click player builds into the git-ignored Builds/ folder.
    /// Command line: -executeMethod RealisticCarRacing.EditorTools.BuildMenu.BuildFromCommandLine -buildPlatform macOS|Windows|Android|iOS
    /// </summary>
    public static class BuildMenu
    {
        [MenuItem("RealisticCarRacing/Build/macOS")]
        public static void BuildMacOS() => Build(BuildTarget.StandaloneOSX, "Builds/macOS/RealisticCarRacing.app");

        [MenuItem("RealisticCarRacing/Build/Windows (64-bit)")]
        public static void BuildWindows() => Build(BuildTarget.StandaloneWindows64, "Builds/Windows/RealisticCarRacing.exe");

        [MenuItem("RealisticCarRacing/Build/Android (APK)")]
        public static void BuildAndroid() => Build(BuildTarget.Android, "Builds/Android/RealisticCarRacing.apk");

        [MenuItem("RealisticCarRacing/Build/iOS (Xcode project)")]
        public static void BuildIOS() => Build(BuildTarget.iOS, "Builds/iOS");

        /// <summary>Add <c>-development</c> for a development build (needed for render/memory counters in benchmarks).</summary>
        public static void BuildFromCommandLine()
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-buildPlatform");
            string platform = index >= 0 && index + 1 < args.Length ? args[index + 1] : "";
            developmentBuild = Array.IndexOf(args, "-development") >= 0;
            bool succeeded = platform switch
            {
                "macOS" => Build(BuildTarget.StandaloneOSX, "Builds/macOS/RealisticCarRacing.app"),
                "Windows" => Build(BuildTarget.StandaloneWindows64, "Builds/Windows/RealisticCarRacing.exe"),
                "Android" => Build(BuildTarget.Android, "Builds/Android/RealisticCarRacing.apk"),
                "iOS" => Build(BuildTarget.iOS, "Builds/iOS"),
                _ => LogUnknownPlatform(platform)
            };
            EditorApplication.Exit(succeeded ? 0 : 1);
        }

        static bool LogUnknownPlatform(string platform)
        {
            Debug.LogError($"Unknown -buildPlatform '{platform}'. Use macOS, Windows, Android or iOS.");
            return false;
        }

        static bool developmentBuild;

        static bool Build(BuildTarget target, string locationPath)
        {
            var group = BuildPipeline.GetBuildTargetGroup(target);
            if (!BuildPipeline.IsBuildTargetSupported(group, target))
            {
                Debug.LogError($"{target} build support is not installed. Add it in Unity Hub > Installs > (editor) > Add modules.");
                return false;
            }

            var options = new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray(),
                target = target,
                targetGroup = group,
                locationPathName = locationPath,
                options = developmentBuild ? BuildOptions.Development : BuildOptions.None
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;
            Debug.Log($"{target} build {summary.result}: {summary.outputPath} ({summary.totalSize / (1024 * 1024)} MB, {summary.totalErrors} errors)");
            return summary.result == BuildResult.Succeeded;
        }
    }
}
