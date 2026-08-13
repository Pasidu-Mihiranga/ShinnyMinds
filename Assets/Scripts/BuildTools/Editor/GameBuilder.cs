using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ShinyMinds.Config;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace ShinyMinds.BuildTools
{
    /// <summary>
    /// Produces the shippable player, from the editor menu or from the command line:
    ///
    ///   Unity.exe -batchmode -quit -projectPath &lt;project&gt; -logFile - \
    ///             -executeMethod ShinyMinds.BuildTools.GameBuilder.BuildWindows
    ///
    /// The build bakes the current .env into a Resources asset, because a player has no
    /// repository root to read its keys and API URL from. The asset is removed again
    /// afterwards so the secrets do not sit in the project between builds.
    /// </summary>
    public static class GameBuilder
    {
        const string Company = "ShinyMinds";
        const string Product = "ShinyMinds";
        const string PackageId = "com.shinyminds.game";

        const string BakedAssetPath = "Assets/Resources/" + GameConfig.BakedConfigResource + ".txt";

        static string ProjectRoot => Directory.GetParent(Application.dataPath).FullName;

        [MenuItem("ShinyMinds/Build/Windows (.exe)")]
        public static void BuildWindows()
        {
            Run(BuildTarget.StandaloneWindows64,
                BuildTargetGroup.Standalone,
                Path.Combine(ProjectRoot, "Builds", "Windows", Product + ".exe"));
        }

        [MenuItem("ShinyMinds/Build/Android (.apk)")]
        public static void BuildAndroid()
        {
            // Cleartext HTTP is blocked from Android 9 on. The backend is plain http, so
            // without this every API call fails with no useful message in the player.
            PlayerSettings.insecureHttpOption = InsecureHttpOption.AlwaysAllowed;

            Run(BuildTarget.Android,
                BuildTargetGroup.Android,
                Path.Combine(ProjectRoot, "Builds", "Android", Product + ".apk"));
        }

        static void Run(BuildTarget target, BuildTargetGroup group, string outputPath)
        {
            ApplyIdentity();

            string[] scenes = EnabledScenes();

            if (scenes.Length == 0)
            {
                Fail("No enabled scenes in Build Settings. Add MainMenu and SampleScene.");

                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));

            bool baked = BakeConfig();

            BuildReport report;

            try
            {
                report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = scenes,
                    locationPathName = outputPath,
                    target = target,
                    targetGroup = group,
                    options = BuildOptions.None,
                });
            }
            finally
            {
                // Always, even if the build threw: the baked asset holds real API keys.
                if (baked)
                {
                    UnbakeConfig();
                }
            }

            BuildSummary summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"[Build] {target} succeeded in {summary.totalTime:mm\\:ss} " +
                          $"({summary.totalSize / (1024 * 1024)} MB) -> {outputPath}");
            }
            else
            {
                Fail($"{target} build {summary.result}: {summary.totalErrors} error(s). " +
                     "See the log above.");
            }
        }

        /// <summary>Names the product. Android refuses to build without a package id.</summary>
        static void ApplyIdentity()
        {
            PlayerSettings.companyName = Company;
            PlayerSettings.productName = Product;

            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, PackageId);
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Standalone, PackageId);
        }

        static string[] EnabledScenes()
        {
            return EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();
        }

        /// <summary>
        /// Copies .env into Resources so the player can read it. Returns false when there
        /// is nothing to bake, which is not an error: a build with no keys still runs, it
        /// just falls back to subtitles and reports its missing configuration clearly.
        /// </summary>
        static bool BakeConfig()
        {
            string env = Path.Combine(ProjectRoot, ".env");

            if (!File.Exists(env))
            {
                Debug.LogWarning("[Build] No .env at the project root, so this build carries no " +
                                 "API keys or API URL. Drop a .env next to the player to supply " +
                                 "them, or add one and rebuild.");

                return false;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(BakedAssetPath));

            // Comments and blank lines are dropped rather than shipped, so the build does
            // not carry the instructions in .env.example along with the values.
            IEnumerable<string> values = File.ReadAllLines(env)
                .Select(line => line.Trim())
                .Where(line => line.Length > 0 && !line.StartsWith("#"));

            File.WriteAllLines(BakedAssetPath, values);

            AssetDatabase.ImportAsset(BakedAssetPath, ImportAssetOptions.ForceSynchronousImport);

            Debug.Log($"[Build] Baked .env into {BakedAssetPath}.");

            return true;
        }

        static void UnbakeConfig()
        {
            if (AssetDatabase.DeleteAsset(BakedAssetPath))
            {
                Debug.Log("[Build] Removed the baked config from the project.");
            }
        }

        /// <summary>
        /// Reports the failure and, in batch mode, exits non-zero. Without the explicit
        /// exit a scripted build that failed still returns success to the shell.
        /// </summary>
        static void Fail(string message)
        {
            Debug.LogError("[Build] " + message);

            if (Application.isBatchMode)
            {
                EditorApplication.Exit(1);
            }
            else
            {
                throw new Exception(message);
            }
        }
    }
}
