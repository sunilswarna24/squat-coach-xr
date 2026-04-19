#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine.XR.Management;

namespace SquatCoach.EditorTools
{
    /// <summary>
    /// Headless build pipeline for the Quest APK. Designed to be invoked via
    /// Unity in -batchmode:
    ///
    ///   Unity -batchmode -nographics -quit \
    ///       -projectPath /path/to/quest \
    ///       -executeMethod SquatCoach.EditorTools.QuestBuildPipeline.ConfigureAndBuild \
    ///       -logFile -
    ///
    /// Individual steps are also exposed so we can run them independently while
    /// debugging.
    /// </summary>
    public static class QuestBuildPipeline
    {
        private const string ScenePath      = "Assets/SquatCoach/Scenes/Main.unity";
        private const string ApkOutputPath  = "build/ExerciseRight.apk";
        private const string CompanyName    = "ExerciseRight";
        private const string ProductName    = "ExerciseRight";
        private const string BundleId       = "com.exerciseright.squatcoach";
        private const string OculusLoaderId = "Unity.XR.Oculus.OculusLoader";

        [MenuItem("SquatCoach/Configure Project For Quest")]
        public static void ConfigureProject()
        {
            SwitchToAndroid();
            ApplyPlayerSettings();
            EnsureXrOculusAndroid();
            EnsureSceneInBuildSettings();
            AssetDatabase.SaveAssets();
            Debug.Log("[QuestBuildPipeline] Project configured for Quest.");
        }

        [MenuItem("SquatCoach/Build Quest APK")]
        public static void BuildApk()
        {
            // Always regenerate the scene from BuildMainScene so layout changes
            // in the editor script (HUD parenting, camera setup, etc.) actually
            // make it into the APK without requiring a manual menu click.
            Debug.Log("[QuestBuildPipeline] Regenerating main scene before build.");
            BuildMainScene.BuildSilent();
            AssetDatabase.SaveAssets();

            EnsureSceneInBuildSettings();

            var dir = Path.GetDirectoryName(ApkOutputPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var options = new BuildPlayerOptions
            {
                scenes           = new[] { ScenePath },
                locationPathName = ApkOutputPath,
                target           = BuildTarget.Android,
                targetGroup      = BuildTargetGroup.Android,
                options          = BuildOptions.None,
            };

            Debug.Log($"[QuestBuildPipeline] Building APK -> {ApkOutputPath}");
            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;
            Debug.Log($"[QuestBuildPipeline] Build result: {summary.result} " +
                      $"(errors={summary.totalErrors}, warnings={summary.totalWarnings}, " +
                      $"size={summary.totalSize}, time={summary.totalTime})");

            if (summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
            {
                throw new Exception("APK build failed: " + summary.result);
            }
        }

        /// <summary>Full pipeline: configure then build. This is what -executeMethod targets.</summary>
        public static void ConfigureAndBuild()
        {
            ConfigureProject();
            BuildMainSceneIfMissing();
            BuildApk();
        }

        // ------------------------------------------------------------------
        // Individual steps
        // ------------------------------------------------------------------

        private static void SwitchToAndroid()
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                Debug.Log("[QuestBuildPipeline] Switching active build target to Android.");
                EditorUserBuildSettings.SwitchActiveBuildTarget(
                    BuildTargetGroup.Android, BuildTarget.Android);
            }
        }

        private static void ApplyPlayerSettings()
        {
            Debug.Log("[QuestBuildPipeline] Applying player settings.");

            PlayerSettings.companyName = CompanyName;
            PlayerSettings.productName = ProductName;
            PlayerSettings.SetApplicationIdentifier(
                NamedBuildTarget.Android, BundleId);

            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.allowUnsafeCode = true;

            // Scripting: IL2CPP + ARM64 + .NET Standard 2.1
            PlayerSettings.SetScriptingBackend(
                NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.SetApiCompatibilityLevel(
                NamedBuildTarget.Android, ApiCompatibilityLevel.NET_Standard);

            // Android SDK levels: min 32 (Android 12), target automatic.
            PlayerSettings.Android.minSdkVersion    = AndroidSdkVersions.AndroidApiLevel32;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;

            // Stereo/XR: Multiview is selected via Oculus settings, but the
            // stereoRenderingPath on PlayerSettings is still respected by some
            // runtimes. "Instancing" maps to Multi-view on Oculus.
            PlayerSettings.stereoRenderingPath = StereoRenderingPath.Instancing;

            // Android options commonly needed for Quest:
            PlayerSettings.Android.forceSDCardPermission = false;
            PlayerSettings.Android.forceInternetPermission = true;
            PlayerSettings.Android.ARCoreEnabled = false;
            PlayerSettings.Android.androidTVCompatibility = false;
            PlayerSettings.Android.androidIsGame = true;
            PlayerSettings.Android.renderOutsideSafeArea = true;
            PlayerSettings.MTRendering = true;
            PlayerSettings.graphicsJobs = true;
        }

        private static void EnsureXrOculusAndroid()
        {
            Debug.Log("[QuestBuildPipeline] Ensuring XR Plug-in Management + Oculus (Android).");

            var androidSettings = XRGeneralSettingsPerBuildTarget
                .XRGeneralSettingsForBuildTarget(BuildTargetGroup.Android);

            if (androidSettings == null)
            {
                // Create a fresh XRGeneralSettings asset for Android.
                var perBuild = GetOrCreatePerBuildTargetSettings();
                androidSettings = ScriptableObject.CreateInstance<XRGeneralSettings>();
                androidSettings.Manager = ScriptableObject.CreateInstance<XRManagerSettings>();

                // Parent the settings to the per-build-target asset so they persist.
                AssetDatabase.AddObjectToAsset(androidSettings, perBuild);
                AssetDatabase.AddObjectToAsset(androidSettings.Manager, perBuild);

                perBuild.SetSettingsForBuildTarget(BuildTargetGroup.Android, androidSettings);
                EditorUtility.SetDirty(perBuild);
            }

            if (androidSettings.Manager == null)
            {
                androidSettings.Manager = ScriptableObject.CreateInstance<XRManagerSettings>();
                EditorUtility.SetDirty(androidSettings);
            }

            androidSettings.InitManagerOnStart = true;

            // Attach Oculus loader via the XR Management metadata API.
            if (!androidSettings.Manager.activeLoaders
                    .Any(l => l != null && l.GetType().FullName == OculusLoaderId))
            {
                var ok = XRPackageMetadataStore.AssignLoader(
                    androidSettings.Manager, OculusLoaderId, BuildTargetGroup.Android);
                Debug.Log($"[QuestBuildPipeline] AssignLoader Oculus -> {ok}");
            }

            EditorUtility.SetDirty(androidSettings);
            EditorUtility.SetDirty(androidSettings.Manager);
            AssetDatabase.SaveAssets();

            TryConfigureOculusSettings();
        }

        private static void TryConfigureOculusSettings()
        {
            // Find (or create) the Oculus Settings asset. Oculus XR 4.x stores
            // targetDevices etc in an asset of type Unity.XR.Oculus.OculusSettings.
            var oculusSettingsType = FindTypeByName(
                "Unity.XR.Oculus.OculusSettings, Unity.XR.Oculus.Editor")
                ?? FindTypeByName("Unity.XR.Oculus.OculusSettings, Unity.XR.Oculus");

            if (oculusSettingsType == null)
            {
                Debug.LogWarning("[QuestBuildPipeline] OculusSettings type not found. Skipping Oculus-specific settings.");
                return;
            }

            UnityEngine.Object settings = null;
            var guids = AssetDatabase.FindAssets("t:" + oculusSettingsType.Name);
            if (guids.Length > 0)
            {
                settings = AssetDatabase.LoadAssetAtPath(
                    AssetDatabase.GUIDToAssetPath(guids[0]), oculusSettingsType);
            }

            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance(oculusSettingsType);
                const string path = "Assets/XR/Settings/Oculus Settings.asset";
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                AssetDatabase.CreateAsset(settings, path);

                // Register with EditorBuildSettings so XR plug-in sees it.
                EditorBuildSettings.AddConfigObject(
                    "Unity.XR.Oculus.Settings", settings, true);
            }

            // OVR Android stereo rendering enum: 0 = Multipass, 1 = Multiview.
            // We want Multiview to match PlayerSettings.stereoRenderingPath = Instancing
            // and to keep parity with Quest's recommended path. The previous value
            // (0 / Multipass) was a documented mismatch — under some Horizon OS
            // versions it produced an apparently-black render until the user
            // recentered, which was the symptom we kept chasing.
            TrySetField(settings, "m_StereoRenderingModeAndroid", 1); // Multiview
            TrySetField(settings, "stereoRenderingModeAndroid",    1);
            // Enable every Quest target we care about. Quest 3S in particular
            // was missing from the original list, which can cause the Oculus
            // loader to refuse XR display init on that headset while the rest
            // of the app keeps running (audio plays, screen stays dark).
            TrySetField(settings, "targetQuest2",  true);
            TrySetField(settings, "targetQuest3",  true);
            TrySetField(settings, "TargetQuest3",  true);
            TrySetField(settings, "targetQuest3S", true);
            TrySetField(settings, "TargetQuest3S", true);
            TrySetField(settings, "targetQuestPro", true);
            TrySetField(settings, "TargetQuestPro", true);
            TrySetField(settings, "TargetQuest2",   true);
            TrySetField(settings, "SymmetricProjection", true);

            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            Debug.Log("[QuestBuildPipeline] Oculus settings applied (Multiview, Quest 2/3/3S/Pro).");
        }

        private static XRGeneralSettingsPerBuildTarget GetOrCreatePerBuildTargetSettings()
        {
            const string path = "Assets/XR/XRGeneralSettingsPerBuildTarget.asset";
            var asset = AssetDatabase.LoadAssetAtPath<XRGeneralSettingsPerBuildTarget>(path);
            if (asset != null) return asset;

            Directory.CreateDirectory(Path.GetDirectoryName(path));
            asset = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
            AssetDatabase.CreateAsset(asset, path);
            EditorBuildSettings.AddConfigObject(
                XRGeneralSettings.k_SettingsKey, asset, true);
            return asset;
        }

        private static void EnsureSceneInBuildSettings()
        {
            if (!File.Exists(ScenePath))
            {
                Debug.LogWarning("[QuestBuildPipeline] Main scene missing at " + ScenePath);
                return;
            }
            var existing = EditorBuildSettings.scenes;
            if (existing.Any(s => s.path == ScenePath && s.enabled)) return;

            var list = existing.Where(s => s.path != ScenePath).ToList();
            list.Insert(0, new EditorBuildSettingsScene(ScenePath, enabled: true));
            EditorBuildSettings.scenes = list.ToArray();
            Debug.Log("[QuestBuildPipeline] Added Main scene to Build Settings.");
        }

        private static void BuildMainSceneIfMissing()
        {
            if (File.Exists(ScenePath)) return;

            Debug.Log("[QuestBuildPipeline] Main scene missing, regenerating silently.");
            BuildMainScene.BuildSilent();
        }

        /// <summary>
        /// Force-regenerate the scene file. Needed whenever BuildMainScene
        /// is updated (e.g. passthrough wiring added) so the APK actually
        /// picks up the new layout.
        /// </summary>
        public static void RegenerateMainScene()
        {
            Debug.Log("[QuestBuildPipeline] Regenerating main scene from BuildMainScene.BuildSilent().");
            BuildMainScene.BuildSilent();
            AssetDatabase.SaveAssets();
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------

        private static Type FindTypeByName(string typeName)
        {
            var t = Type.GetType(typeName, false);
            if (t != null) return t;
            var raw = typeName.Split(',')[0].Trim();
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                t = asm.GetType(raw, false);
                if (t != null) return t;
            }
            return null;
        }

        private static void TrySetField(object target, string fieldName, object value)
        {
            if (target == null) return;
            var type = target.GetType();

            var f = type.GetField(fieldName,
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance);
            if (f != null)
            {
                try { f.SetValue(target, ConvertValue(value, f.FieldType)); return; }
                catch { /* ignore */ }
            }

            var p = type.GetProperty(fieldName,
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance);
            if (p != null && p.CanWrite)
            {
                try { p.SetValue(target, ConvertValue(value, p.PropertyType)); }
                catch { /* ignore */ }
            }
        }

        private static object ConvertValue(object value, Type target)
        {
            if (value == null) return null;
            if (target.IsInstanceOfType(value)) return value;
            if (target.IsEnum) return Enum.ToObject(target, (int)Convert.ChangeType(value, typeof(int)));
            return Convert.ChangeType(value, target);
        }
    }
}
#endif
