using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace NovaForge.Tiltwell.Editor
{
    /// <summary>
    /// Signed Google Play release build (.aab). Secrets never live in the project: the keystore path and passwords come
    /// from environment variables, so nothing sensitive is written to ProjectSettings or git.
    /// <list type="bullet">
    /// <item>NF_KEYSTORE: full path of the upload keystore (.keystore / .jks, outside the repo).</item>
    /// <item>NF_KEYSTORE_PASS, NF_KEY_ALIAS, NF_KEY_PASS: keystore password, key alias and key password.</item>
    /// <item>NF_VERSION_CODE (optional): explicit versionCode; otherwise the current one + 1.</item>
    /// <item>NF_VERSION_NAME (optional): versionName, e.g. 1.0.1; otherwise unchanged.</item>
    /// </list>
    /// Batch: <c>-executeMethod NovaForge.Tiltwell.Editor.TiltwellReleaseBuild.BuildAab</c>.
    /// </summary>
    public static class TiltwellReleaseBuild
    {
        public const string OutputDir = "Builds/Release";

        [MenuItem("NOVAFORGE/Tiltwell/Build Release AAB (Google Play)")]
        public static void BuildAab()
        {
            int code = 1;
            try { code = Run() ? 0 : 1; }
            catch (Exception e) { Debug.LogError("[Release] " + e.Message); }
            if (Application.isBatchMode) EditorApplication.Exit(code);
        }

        private static bool Run()
        {
            string keystore = Env("NF_KEYSTORE"), storePass = Env("NF_KEYSTORE_PASS"), alias = Env("NF_KEY_ALIAS"), keyPass = Env("NF_KEY_PASS");
            if (keystore == null || storePass == null || alias == null || keyPass == null)
                throw new InvalidOperationException("Set NF_KEYSTORE, NF_KEYSTORE_PASS, NF_KEY_ALIAS and NF_KEY_PASS (see docs/RELEASE.md).");
            if (!File.Exists(keystore)) throw new FileNotFoundException("Keystore not found: " + keystore);

            var android = NamedBuildTarget.Android;
            PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64 | AndroidArchitecture.ARMv7;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;

            int versionCode = int.TryParse(Env("NF_VERSION_CODE"), out var vc) ? vc : PlayerSettings.Android.bundleVersionCode + 1;
            PlayerSettings.Android.bundleVersionCode = versionCode;
            if (Env("NF_VERSION_NAME") is string vn) PlayerSettings.bundleVersion = vn;

            // Signing is set only for this build and cleared afterwards, so passwords are never saved to ProjectSettings.
            PlayerSettings.Android.useCustomKeystore = true;
            PlayerSettings.Android.keystoreName = keystore;
            PlayerSettings.Android.keystorePass = storePass;
            PlayerSettings.Android.keyaliasName = alias;
            PlayerSettings.Android.keyaliasPass = keyPass;
            bool bundle = EditorUserBuildSettings.buildAppBundle;
            EditorUserBuildSettings.buildAppBundle = true;
            try
            {
                Directory.CreateDirectory(OutputDir);
                string path = $"{OutputDir}/Tiltwell-{PlayerSettings.bundleVersion}-{versionCode}.aab";
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { "Assets/Tiltwell/Tiltwell.unity" },
                    locationPathName = path,
                    target = BuildTarget.Android,
                    options = BuildOptions.None,
                });
                bool ok = report.summary.result == BuildResult.Succeeded;
                Debug.Log($"[Release] result={report.summary.result} path={path} versionCode={versionCode} size={report.summary.totalSize}");
                if (!ok) PlayerSettings.Android.bundleVersionCode = versionCode - 1;
                AssetDatabase.SaveAssets();
                return ok;
            }
            finally
            {
                EditorUserBuildSettings.buildAppBundle = bundle;
                PlayerSettings.Android.keystorePass = "";
                PlayerSettings.Android.keyaliasPass = "";
                PlayerSettings.Android.keystoreName = "";
                PlayerSettings.Android.keyaliasName = "";
                PlayerSettings.Android.useCustomKeystore = false;
                AssetDatabase.SaveAssets();
            }
        }

        private static string Env(string name)
        {
            var v = Environment.GetEnvironmentVariable(name);
            return string.IsNullOrWhiteSpace(v) ? null : v;
        }
    }
}
